using Caesarea.Contracts;

namespace SmartPole.Simulator.Api.Services;

public sealed class SmartPoleSimulatorService
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private SmartPolePhysicalState _state;

    public SmartPoleSimulatorService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _state = CreateBaselineState(_timeProvider.GetUtcNow());
    }

    public SmartPolePhysicalState GetState(string assetId)
    {
        EnsureAsset(assetId);

        lock (_gate)
        {
            return _state;
        }
    }

    public SmartPolePhysicalState Reset(string correlationId)
    {
        lock (_gate)
        {
            _state = CreateBaselineState(_timeProvider.GetUtcNow());
            return _state;
        }
    }

    public SmartPolePhysicalState ApplyScenario(SmartPoleScenarioState scenarioState, string correlationId)
    {
        lock (_gate)
        {
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

            return _state;
        }
    }

    public SmartPolePhysicalState UpdateConfiguration(SmartPoleBehaviorConfiguration configuration, string correlationId)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            _state = _state with
            {
                Configuration = configuration,
                LastCommand = CreateCommand("Configure simulator", _state.IsOn, CommandExecutionStatus.Succeeded, correlationId, now, now, "SmartPole simulator behavior updated.")
            };

            return _state;
        }
    }

    public async Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAsset(command.AssetId);

        var requestedAt = _timeProvider.GetUtcNow();
        SmartPoleBehaviorConfiguration configuration;
        ControllerHealthInfo controllerHealth;

        lock (_gate)
        {
            configuration = _state.Configuration;
            controllerHealth = _state.ControllerHealth;
            _state = _state with
            {
                LastCommand = CreateCommand(
                    "Set lamp state",
                    command.DesiredIsOn,
                    CommandExecutionStatus.Pending,
                    correlationId,
                    requestedAt,
                    null,
                    "SmartPole command accepted and waiting for deterministic execution.")
            };
        }

        if (configuration.CommandDelayMs > 0)
        {
            await Task.Delay(configuration.CommandDelayMs, cancellationToken);
        }

        var completedAt = _timeProvider.GetUtcNow();

        if (configuration.SimulateTimeout)
        {
            lock (_gate)
            {
                _state = _state with
                {
                    LastCommand = CreateCommand(
                        "Set lamp state",
                        command.DesiredIsOn,
                        CommandExecutionStatus.TimedOut,
                        correlationId,
                        requestedAt,
                        completedAt,
                        "SmartPole command timed out before device acknowledgement.")
                };
            }

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
            lock (_gate)
            {
                _state = _state with
                {
                    LastCommand = CreateCommand(
                        "Set lamp state",
                        command.DesiredIsOn,
                        CommandExecutionStatus.Failed,
                        correlationId,
                        requestedAt,
                        completedAt,
                        "SmartPole controller rejected the command.")
                };
            }

            return new SmartPoleCommandResult(
                command.AssetId,
                null,
                CommandExecutionStatus.Failed,
                correlationId,
                "SmartPole controller rejected the command.",
                completedAt);
        }

        lock (_gate)
        {
            _state = _state with
            {
                IsOn = command.DesiredIsOn,
                ManualOverride = false,
                LastReportedAt = completedAt,
                LastCommand = CreateCommand(
                    "Set lamp state",
                    command.DesiredIsOn,
                    CommandExecutionStatus.Succeeded,
                    correlationId,
                    requestedAt,
                    completedAt,
                    "SmartPole confirmed the requested lamp state.")
            };
        }

        return new SmartPoleCommandResult(
            command.AssetId,
            command.DesiredIsOn,
            CommandExecutionStatus.Succeeded,
            correlationId,
            "SmartPole confirmed the requested lamp state.",
            completedAt);
    }

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
        if (!string.Equals(assetId, DemoAssets.StreetlightAssetId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The SmartPole simulator only exposes asset {DemoAssets.StreetlightAssetId}.", nameof(assetId));
        }
    }
}
