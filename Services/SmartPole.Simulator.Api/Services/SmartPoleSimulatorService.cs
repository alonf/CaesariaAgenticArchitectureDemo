namespace SmartPole.Simulator.Api.Services;

/// <summary>
/// Simulates the vendor-facing physical streetlight system behind the authoritative Energy Hub.
/// </summary>
public sealed partial class SmartPoleSimulatorService
{
    private const string SetLampStateOperation = "Set lamp state";
    private const string SupersededSummary =
        "SmartPole command was superseded by a scenario change or reset; the new state was preserved.";
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SmartPoleSimulatorService> _logger;
    private SmartPolePhysicalState _state;
    private long _revision;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartPoleSimulatorService"/> class.
    /// </summary>
    /// <param name="timeProvider">The clock used for deterministic timestamps.</param>
    /// <param name="logger">The logger used for simulator state changes.</param>
    public SmartPoleSimulatorService(TimeProvider timeProvider, ILogger<SmartPoleSimulatorService> logger)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _state = CreateBaselineState(_timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Gets the current authoritative simulator state for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to read.</param>
    /// <returns>The current physical state.</returns>
    public SmartPolePhysicalState GetState(string assetId)
    {
        EnsureAsset(assetId);

        lock (_gate)
        {
            return _state;
        }
    }

    /// <summary>
    /// Resets the simulator back to its deterministic baseline state.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the reset.</param>
    /// <returns>The reset physical state.</returns>
    public SmartPolePhysicalState Reset(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_gate)
        {
            _revision++;
            _state = CreateBaselineState(_timeProvider.GetUtcNow());
        }

        SmartPoleSimulatorServiceLog.ResetCompleted(_logger, DemoAssets.StreetlightAssetId, correlationId);
        return GetState(DemoAssets.StreetlightAssetId);
    }

    /// <summary>
    /// Applies the supplied deterministic scenario state to the simulator.
    /// </summary>
    /// <param name="scenarioState">The scenario state to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the scenario application.</param>
    /// <returns>The resulting physical state.</returns>
    public SmartPolePhysicalState ApplyScenario(SmartPoleScenarioState scenarioState, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(scenarioState);
        ArgumentNullException.ThrowIfNull(scenarioState.ControllerHealth);
        ArgumentNullException.ThrowIfNull(scenarioState.OperationContext);
        ArgumentNullException.ThrowIfNull(scenarioState.Configuration);
        ValidateConfiguration(scenarioState.Configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_gate)
        {
            _revision++;
            var now = _timeProvider.GetUtcNow();
            _state = new SmartPolePhysicalState(
                DemoAssets.StreetlightAssetId,
                DemoAssets.NorthPromenadeArea,
                scenarioState.IsOn,
                scenarioState.IsDaylight,
                scenarioState.ExpectedScheduledState,
                scenarioState.ManualOverride,
                scenarioState.ControllerHealth,
                CreateCommand("Apply scenario", scenarioState.IsOn, CommandExecutionStatus.Succeeded, correlationId, now, now, "Scenario state applied to the SmartPole simulator."),
                scenarioState.LastMaintenanceTime,
                scenarioState.HasRecentMaintenance,
                scenarioState.OperationContext,
                now,
                scenarioState.Configuration);
        }

        SmartPoleSimulatorServiceLog.ScenarioApplied(_logger, DemoAssets.StreetlightAssetId, scenarioState.IsOn, correlationId);
        return GetState(DemoAssets.StreetlightAssetId);
    }

    /// <summary>
    /// Updates the simulator behavior configuration used for subsequent commands.
    /// </summary>
    /// <param name="configuration">The configuration to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the configuration change.</param>
    /// <returns>The resulting physical state.</returns>
    public SmartPolePhysicalState UpdateConfiguration(SmartPoleBehaviorConfiguration configuration, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateConfiguration(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            _state = _state with
            {
                Configuration = configuration,
                LastCommand = CreateCommand("Configure simulator", _state.IsOn, CommandExecutionStatus.Succeeded, correlationId, now, now, "SmartPole simulator behavior updated.")
            };
        }

        SmartPoleSimulatorServiceLog.ConfigurationUpdated(_logger, DemoAssets.StreetlightAssetId, configuration.CommandDelayMs, configuration.SimulateTimeout, configuration.SimulateFailure, correlationId);
        return GetState(DemoAssets.StreetlightAssetId);
    }

    /// <summary>
    /// Applies a lamp-state command to the simulator and returns the deterministic command result.
    /// </summary>
    /// <param name="command">The command to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the command request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The deterministic command result.</returns>
    public async Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureAsset(command.AssetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var desiredIsOn = command.DesiredIsOn
            ?? throw new ArgumentException("Desired lamp state is required.", nameof(command));

        var requestedAt = _timeProvider.GetUtcNow();
        SmartPoleBehaviorConfiguration configuration;
        ControllerHealthInfo controllerHealth;
        long commandRevision;

        lock (_gate)
        {
            commandRevision = _revision;
            configuration = _state.Configuration;
            controllerHealth = _state.ControllerHealth;
            _state = _state with
            {
                LastCommand = CreateCommand(
                    SetLampStateOperation,
                    desiredIsOn,
                    CommandExecutionStatus.Pending,
                    correlationId,
                    requestedAt,
                    null,
                    "SmartPole command accepted and waiting for deterministic execution.")
            };
        }

        SmartPoleSimulatorServiceLog.CommandAccepted(_logger, command.AssetId, desiredIsOn, correlationId);

        try
        {
            if (configuration.CommandDelayMs > 0)
            {
                await Task.Delay(configuration.CommandDelayMs, cancellationToken);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            var canceledAt = _timeProvider.GetUtcNow();

            if (!TryCommitCommand(commandRevision, state => state with
            {
                LastCommand = CreateCommand(
                    SetLampStateOperation,
                    desiredIsOn,
                    CommandExecutionStatus.Failed,
                    correlationId,
                    requestedAt,
                    canceledAt,
                    "SmartPole command was canceled before device acknowledgement.")
            }))
            {
                SmartPoleSimulatorServiceLog.CommandSuperseded(_logger, command.AssetId, correlationId);
            }

            SmartPoleSimulatorServiceLog.CommandCanceled(_logger, command.AssetId, correlationId, exception);
            throw;
        }

        var completedAt = _timeProvider.GetUtcNow();

        if (configuration.SimulateTimeout)
        {
            if (!TryCommitCommand(commandRevision, state => state with
            {
                LastCommand = CreateCommand(
                    SetLampStateOperation,
                    desiredIsOn,
                    CommandExecutionStatus.TimedOut,
                    correlationId,
                    requestedAt,
                    completedAt,
                    "SmartPole command timed out before device acknowledgement.")
            }))
            {
                SmartPoleSimulatorServiceLog.CommandSuperseded(_logger, command.AssetId, correlationId);
                return CreateSupersededResult(command.AssetId, correlationId);
            }

            SmartPoleSimulatorServiceLog.CommandTimedOut(_logger, command.AssetId, correlationId);

            return new SmartPoleCommandResult(
                command.AssetId,
                null,
                CommandExecutionStatus.TimedOut,
                correlationId,
                "SmartPole command timed out before device acknowledgement.",
                completedAt);
        }

        if (controllerHealth.Status == ControllerHealthStatus.Faulted || configuration.SimulateFailure)
        {
            if (!TryCommitCommand(commandRevision, state => state with
            {
                LastCommand = CreateCommand(
                    SetLampStateOperation,
                    desiredIsOn,
                    CommandExecutionStatus.Failed,
                    correlationId,
                    requestedAt,
                    completedAt,
                    "SmartPole controller rejected the command.")
            }))
            {
                SmartPoleSimulatorServiceLog.CommandSuperseded(_logger, command.AssetId, correlationId);
                return CreateSupersededResult(command.AssetId, correlationId);
            }

            SmartPoleSimulatorServiceLog.CommandFailed(_logger, command.AssetId, correlationId);

            return new SmartPoleCommandResult(
                command.AssetId,
                null,
                CommandExecutionStatus.Failed,
                correlationId,
                "SmartPole controller rejected the command.",
                completedAt);
        }

        if (!TryCommitCommand(commandRevision, state => state with
        {
            IsOn = desiredIsOn,
            ManualOverride = false,
            LastReportedAt = completedAt,
            LastCommand = CreateCommand(
                SetLampStateOperation,
                desiredIsOn,
                CommandExecutionStatus.Succeeded,
                correlationId,
                requestedAt,
                completedAt,
                "SmartPole confirmed the requested lamp state.")
        }))
        {
            SmartPoleSimulatorServiceLog.CommandSuperseded(_logger, command.AssetId, correlationId);
            return CreateSupersededResult(command.AssetId, correlationId);
        }

        SmartPoleSimulatorServiceLog.CommandSucceeded(_logger, command.AssetId, desiredIsOn, correlationId);

        return new SmartPoleCommandResult(
            command.AssetId,
            desiredIsOn,
            CommandExecutionStatus.Succeeded,
            correlationId,
            "SmartPole confirmed the requested lamp state.",
            completedAt);
    }

    /// <summary>
    /// Creates the deterministic baseline simulator state used at service startup and reset.
    /// </summary>
    /// <param name="now">The timestamp to stamp onto the baseline state.</param>
    /// <returns>The baseline simulator state.</returns>
    public static SmartPolePhysicalState CreateBaselineState(DateTimeOffset now) =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            false,
            true,
            false,
            false,
            ControllerHealthInfo.Healthy,
            null,
            null,
            false,
            OperationalContext.None,
            now,
            SmartPoleBehaviorConfiguration.Default);

    private bool TryCommitCommand(long commandRevision, Func<SmartPolePhysicalState, SmartPolePhysicalState> mutation)
    {
        lock (_gate)
        {
            if (_revision != commandRevision)
            {
                return false;
            }

            _state = mutation(_state);
            return true;
        }
    }

    private SmartPoleCommandResult CreateSupersededResult(string assetId, string correlationId) =>
        new(
            assetId,
            null,
            CommandExecutionStatus.Failed,
            correlationId,
            SupersededSummary,
            _timeProvider.GetUtcNow());

    private static CommandRecord CreateCommand(
        string operation,
        bool desiredIsOn,
        CommandExecutionStatus status,
        string correlationId,
        DateTimeOffset requestedAt,
        DateTimeOffset? completedAt,
        string summary) =>
        new(
            operation,
            desiredIsOn,
            status,
            correlationId,
            requestedAt,
            completedAt,
            summary);

    private static void EnsureAsset(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (!string.Equals(assetId, DemoAssets.StreetlightAssetId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The SmartPole simulator only exposes asset {DemoAssets.StreetlightAssetId}.", nameof(assetId));
        }
    }

    private static void ValidateConfiguration(SmartPoleBehaviorConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.CommandDelayMs < 0 || configuration.CommandDelayMs > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), configuration.CommandDelayMs, "CommandDelayMs must be between 0 and 30000 milliseconds.");
        }
    }
}

internal static partial class SmartPoleSimulatorServiceLog
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "SmartPole simulator reset to baseline for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetCompleted(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "SmartPole scenario applied for asset {AssetId}. IsOn: {IsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioApplied(ILogger logger, string assetId, bool isOn, string correlationId);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Information,
        Message = "SmartPole simulator configuration updated for asset {AssetId}. CommandDelayMs: {CommandDelayMs}, SimulateTimeout: {SimulateTimeout}, SimulateFailure: {SimulateFailure}. CorrelationId: {CorrelationId}.")]
    internal static partial void ConfigurationUpdated(ILogger logger, string assetId, int commandDelayMs, bool simulateTimeout, bool simulateFailure, string correlationId);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "SmartPole accepted Set Lamp State for asset {AssetId}. DesiredIsOn: {DesiredIsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandAccepted(ILogger logger, string assetId, bool desiredIsOn, string correlationId);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Information,
        Message = "SmartPole Set Lamp State was canceled for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandCanceled(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Warning,
        Message = "SmartPole Set Lamp State timed out for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandTimedOut(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2106,
        Level = LogLevel.Warning,
        Message = "SmartPole Set Lamp State failed for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandFailed(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2107,
        Level = LogLevel.Information,
        Message = "SmartPole Set Lamp State succeeded for asset {AssetId}. DesiredIsOn: {DesiredIsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandSucceeded(ILogger logger, string assetId, bool desiredIsOn, string correlationId);

    [LoggerMessage(
        EventId = 2108,
        Level = LogLevel.Warning,
        Message = "SmartPole Set Lamp State was superseded by a scenario change or reset for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandSuperseded(ILogger logger, string assetId, string correlationId);
}
