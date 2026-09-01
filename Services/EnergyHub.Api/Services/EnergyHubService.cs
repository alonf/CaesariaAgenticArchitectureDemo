namespace EnergyHub.Api.Services;

/// <summary>
/// Owns the authoritative Energy Hub operational twin and deterministic Restore Scheduled Mode workflow.
/// </summary>
public sealed partial class EnergyHubService
{
    private const string RestoreScheduledModeOperation = "Restore scheduled mode";
    private const string SupersededSummary =
        "Restore Scheduled Mode was superseded by a newer operation, scenario change, or reset; the newer state was preserved.";

    /// <summary>
    /// Prefix of the summary returned when a command's state precondition no longer holds. Callers
    /// match on it to re-validate rather than treating the refusal as a downstream failure.
    /// </summary>
    public const string PreconditionFailedSummaryPrefix = "Precondition failed:";

    private readonly object _gate = new();
    private readonly ISmartPoleGateway _smartpoleGateway;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EnergyHubService> _logger;
    private readonly List<ActivityRecord> _activity = [];
    private EnergyOperationalTwin _twin;
    private long _revision;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnergyHubService"/> class.
    /// </summary>
    /// <param name="smartpoleGateway">The correlated gateway to the vendor-facing SmartPole simulator.</param>
    /// <param name="timeProvider">The clock used for deterministic timestamps.</param>
    /// <param name="logger">The logger used for command and synchronization events.</param>
    public EnergyHubService(ISmartPoleGateway smartpoleGateway, TimeProvider timeProvider, ILogger<EnergyHubService> logger)
    {
        _smartpoleGateway = smartpoleGateway ?? throw new ArgumentNullException(nameof(smartpoleGateway));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _twin = CreateBaselineTwin(_timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Gets the current authoritative Energy Hub twin for the configured streetlight asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to retrieve.</param>
    /// <returns>The current operational twin.</returns>
    public EnergyOperationalTwin GetState(string assetId)
    {
        EnsureAsset(assetId);

        if (IsFixtureAsset(assetId))
        {
            return CreateSecondStreetlightFixtureTwin(_timeProvider.GetUtcNow());
        }

        lock (_gate)
        {
            return CurrentTwin();
        }
    }

    /// <summary>
    /// Gets the most recent correlated Energy Hub activity for the configured streetlight asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to retrieve.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <returns>The recent activity ordered from newest to oldest.</returns>
    public IReadOnlyList<ActivityRecord> GetRecentActivity(string assetId, int limit)
    {
        EnsureAsset(assetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        if (IsFixtureAsset(assetId))
        {
            return [];
        }

        lock (_gate)
        {
            return _activity
                .OrderByDescending(record => record.OccurredAt)
                .Take(limit)
                .ToArray();
        }
    }

    /// <summary>
    /// Rereads the current authoritative SmartPole state and resets the Energy Hub twin to the deterministic baseline projected from it.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the reset request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The refreshed operational twin.</returns>
    public async Task<EnergyOperationalTwin> ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        EnergyHubServiceLog.ResetStarted(_logger, DemoAssets.StreetlightAssetId, correlationId);

        try
        {
            var physicalState = await _smartpoleGateway.GetStateAsync(DemoAssets.StreetlightAssetId, correlationId, cancellationToken);
            var desiredIsOn = ComputeScheduledTarget(physicalState);

            lock (_gate)
            {
                _revision++;
                _activity.Clear();
                _twin = CreateTwinFromPhysical(physicalState, desiredIsOn, null, null);
                AddActivity("Energy Hub reset to the current deterministic SmartPole state.", correlationId, ActivityKind.Synchronization, true, null);
                EnergyHubServiceLog.ResetCompleted(_logger, DemoAssets.StreetlightAssetId, correlationId, desiredIsOn);
                return CurrentTwin();
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            EnergyHubServiceLog.ResetCanceled(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            EnergyHubServiceLog.ResetTimedOut(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception)
        {
            EnergyHubServiceLog.ResetReadFailed(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            EnergyHubServiceLog.ResetReadInvalid(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
    }

    /// <summary>
    /// Synchronizes the Energy Hub twin after the SmartPole simulator has already applied a deterministic scenario.
    /// </summary>
    /// <param name="request">The synchronization request projected from the scenario recipe.</param>
    /// <param name="correlationId">The correlation identifier spanning the scenario application.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The refreshed operational twin.</returns>
    public async Task<EnergyOperationalTwin> ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        EnergyHubServiceLog.ScenarioSynchronizationStarted(_logger, DemoAssets.StreetlightAssetId, correlationId, request.DesiredIsOn);

        try
        {
            var physicalState = await _smartpoleGateway.GetStateAsync(DemoAssets.StreetlightAssetId, correlationId, cancellationToken);

            lock (_gate)
            {
                _revision++;
                _activity.Clear();
                _twin = CreateTwinFromPhysical(physicalState, request.DesiredIsOn, request.OpenIncidentId, null);
                AddActivity(request.Summary, correlationId, ActivityKind.Scenario, true, null);
                EnergyHubServiceLog.ScenarioSynchronizationCompleted(_logger, DemoAssets.StreetlightAssetId, correlationId, request.OpenIncidentId ?? "none");
                return CurrentTwin();
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            EnergyHubServiceLog.ScenarioSynchronizationCanceled(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            EnergyHubServiceLog.ScenarioSynchronizationTimedOut(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception)
        {
            EnergyHubServiceLog.ScenarioSynchronizationFailed(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            EnergyHubServiceLog.ScenarioSynchronizationInvalid(_logger, DemoAssets.StreetlightAssetId, correlationId, exception);
            throw;
        }
    }

    /// <summary>
    /// Restores the asset to its deterministic scheduled mode after rereading authoritative SmartPole state.
    /// </summary>
    /// <param name="assetId">The asset identifier to restore.</param>
    /// <param name="correlationId">The correlation identifier spanning the command request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <param name="expectedStateRevision">
    /// The state revision the caller validated its decision against. When supplied and no longer
    /// current, the command is refused without any side effect - the caller must re-validate.
    /// </param>
    /// <returns>The authoritative command outcome.</returns>
    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(
        string assetId,
        string correlationId,
        CancellationToken cancellationToken,
        long? expectedStateRevision = null)
    {
        EnsureAsset(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (IsFixtureAsset(assetId))
        {
            return new RestoreScheduledModeResult(
                assetId,
                null,
                null,
                CommandExecutionStatus.Failed,
                correlationId,
                $"Asset {assetId} is a read-only demo fixture; commands target {DemoAssets.StreetlightAssetId}.",
                _timeProvider.GetUtcNow());
        }

        var requestedAt = _timeProvider.GetUtcNow();
        long commandRevision;

        lock (_gate)
        {
            // The precondition and the claim share one lock: a caller that decided against
            // revision N cannot have the state change between the check and the accept.
            if (expectedStateRevision is { } expected && _revision != expected)
            {
                EnergyHubServiceLog.RestorePreconditionFailed(_logger, assetId, correlationId, expected, _revision);
                return new RestoreScheduledModeResult(
                    assetId,
                    _twin.DesiredIsOn,
                    _twin.ReportedIsOn,
                    CommandExecutionStatus.Failed,
                    correlationId,
                    $"{PreconditionFailedSummaryPrefix} the caller validated state revision {expected}, but the authoritative revision is now {_revision}. No change was made.",
                    _timeProvider.GetUtcNow());
            }

            // Accepting a restore claims a fresh revision, so of two concurrent restores only the
            // most recently accepted one can commit; the older one reports superseded even when it
            // completes last.
            commandRevision = ++_revision;
        }

        EnergyHubServiceLog.RestoreRequested(_logger, assetId, correlationId);

        SmartPolePhysicalState physicalState;

        try
        {
            physicalState = await _smartpoleGateway.GetStateAsync(assetId, correlationId, cancellationToken);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            EnergyHubServiceLog.RestoreCanceledBeforeRead(_logger, assetId, correlationId, exception);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            EnergyHubServiceLog.RestoreReadTimedOut(_logger, assetId, correlationId, exception);
            return CompleteReadFailure(
                commandRevision,
                correlationId,
                requestedAt,
                CommandExecutionStatus.TimedOut,
                "Energy Hub timed out rereading the authoritative SmartPole state before issuing the command.");
        }
        catch (HttpRequestException exception)
        {
            EnergyHubServiceLog.RestoreReadFailed(_logger, assetId, correlationId, exception);
            return CompleteReadFailure(
                commandRevision,
                correlationId,
                requestedAt,
                CommandExecutionStatus.Failed,
                "Energy Hub could not reach SmartPole to reread the authoritative state before issuing the command.");
        }
        catch (InvalidOperationException exception)
        {
            EnergyHubServiceLog.RestoreReadInvalid(_logger, assetId, correlationId, exception);
            return CompleteReadFailure(
                commandRevision,
                correlationId,
                requestedAt,
                CommandExecutionStatus.Failed,
                "Energy Hub received an invalid SmartPole state response before issuing the command.");
        }

        var scheduledTarget = ComputeScheduledTarget(physicalState);

        lock (_gate)
        {
            if (_revision != commandRevision)
            {
                EnergyHubServiceLog.RestoreSuperseded(_logger, assetId, correlationId);
                return CreateSupersededResult(assetId, scheduledTarget, correlationId);
            }

            _twin = CreateTwinFromPhysical(
                physicalState,
                scheduledTarget,
                _twin.OpenIncidentId,
                new CommandRecord(
                    RestoreScheduledModeOperation,
                    scheduledTarget,
                    CommandExecutionStatus.Pending,
                    correlationId,
                    requestedAt,
                    null,
                    "Desired state updated and awaiting SmartPole confirmation."));

            AddActivity(
                $"Desired state set to {(scheduledTarget ? "On" : "Off")} before SmartPole confirmation.",
                correlationId,
                ActivityKind.Command,
                true,
                CommandExecutionStatus.Pending);
        }

        EnergyHubServiceLog.RestoreCommandSent(_logger, assetId, scheduledTarget, correlationId);

        try
        {
            var commandResult = await _smartpoleGateway.SetLampStateAsync(new SetLampStateCommand(assetId, scheduledTarget), correlationId, cancellationToken);

            if (commandResult is { Status: CommandExecutionStatus.Succeeded, ActualIsOn: { } confirmedIsOn })
            {
                lock (_gate)
                {
                    if (_revision != commandRevision)
                    {
                        EnergyHubServiceLog.RestoreSuperseded(_logger, assetId, correlationId);
                        return CreateSupersededResult(assetId, scheduledTarget, correlationId);
                    }

                    _twin = _twin with
                    {
                        DesiredIsOn = scheduledTarget,
                        ReportedIsOn = confirmedIsOn,
                        ManualOverride = false,
                        LastReportedAt = commandResult.CompletedAt,
                        LastCommand = new CommandRecord(
                            RestoreScheduledModeOperation,
                            scheduledTarget,
                            CommandExecutionStatus.Succeeded,
                            correlationId,
                            requestedAt,
                            commandResult.CompletedAt,
                            "SmartPole confirmed the scheduled state.")
                    };

                    AddActivity(
                        $"SmartPole confirmed the reported state as {(confirmedIsOn ? "On" : "Off")}.",
                        correlationId,
                        ActivityKind.Command,
                        true,
                        CommandExecutionStatus.Succeeded);
                }

                EnergyHubServiceLog.RestoreSucceeded(_logger, assetId, scheduledTarget, correlationId);

                // Report the physical state SmartPole confirmed for THIS command; reading the live
                // twin here could pick up a concurrent operation's state.
                return new RestoreScheduledModeResult(
                    assetId,
                    scheduledTarget,
                    confirmedIsOn,
                    CommandExecutionStatus.Succeeded,
                    correlationId,
                    "Scheduled mode restored after SmartPole confirmation.",
                    commandResult.CompletedAt);
            }

            if (commandResult.Status == CommandExecutionStatus.Succeeded)
            {
                // A success without a confirmed physical state violates the SmartPole contract: the
                // reported state may only change after physical confirmation, so keep the previous state.
                EnergyHubServiceLog.RestoreReturnedFailure(_logger, assetId, commandResult.Status, correlationId, commandResult.Summary);
                return CompleteCommandFailure(
                    commandRevision,
                    physicalState,
                    scheduledTarget,
                    correlationId,
                    requestedAt,
                    CommandExecutionStatus.Failed,
                    "SmartPole reported success without a confirmed physical state; the reported state is unchanged.",
                    commandResult.CompletedAt);
            }

            EnergyHubServiceLog.RestoreReturnedFailure(_logger, assetId, commandResult.Status, correlationId, commandResult.Summary);
            return CompleteCommandFailure(
                commandRevision,
                physicalState,
                scheduledTarget,
                correlationId,
                requestedAt,
                commandResult.Status,
                commandResult.Summary,
                commandResult.CompletedAt);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            EnergyHubServiceLog.RestoreCanceledAfterSend(_logger, assetId, correlationId, exception);
            CompleteCommandFailure(
                commandRevision,
                physicalState,
                scheduledTarget,
                correlationId,
                requestedAt,
                CommandExecutionStatus.Failed,
                "Restore Scheduled Mode was canceled before SmartPole confirmation.");
            throw;
        }
        catch (OperationCanceledException exception)
        {
            EnergyHubServiceLog.RestoreCommandTimedOut(_logger, assetId, correlationId, exception);
            return CompleteCommandFailure(
                commandRevision,
                physicalState,
                scheduledTarget,
                correlationId,
                requestedAt,
                CommandExecutionStatus.TimedOut,
                "SmartPole did not acknowledge the Restore Scheduled Mode command before the timeout window closed.");
        }
        catch (HttpRequestException exception)
        {
            EnergyHubServiceLog.RestoreCommandFailed(_logger, assetId, correlationId, exception);
            return CompleteCommandFailure(
                commandRevision,
                physicalState,
                scheduledTarget,
                correlationId,
                requestedAt,
                CommandExecutionStatus.Failed,
                "Energy Hub could not reach SmartPole to confirm the Restore Scheduled Mode command.");
        }
        catch (InvalidOperationException exception)
        {
            EnergyHubServiceLog.RestoreCommandInvalid(_logger, assetId, correlationId, exception);
            return CompleteCommandFailure(
                commandRevision,
                physicalState,
                scheduledTarget,
                correlationId,
                requestedAt,
                CommandExecutionStatus.Failed,
                "Energy Hub received an invalid SmartPole command response while restoring scheduled mode.");
        }
    }

    /// <summary>
    /// Computes the schedule-driven target that the Energy Hub should pursue after rereading SmartPole state.
    /// </summary>
    /// <param name="physicalState">The authoritative SmartPole physical state.</param>
    /// <returns><see langword="true"/> when the lamp should be on; otherwise <see langword="false"/>.</returns>
    public static bool ComputeScheduledTarget(SmartPolePhysicalState physicalState)
    {
        ArgumentNullException.ThrowIfNull(physicalState);
        return LightingTarget.Resolve(physicalState.ExpectedScheduledState, physicalState.OperationContext);
    }

    /// <summary>
    /// Creates the deterministic baseline Energy Hub twin used when the service starts.
    /// </summary>
    /// <param name="now">The timestamp to stamp onto the baseline twin.</param>
    /// <returns>The baseline operational twin.</returns>
    public static EnergyOperationalTwin CreateBaselineTwin(DateTimeOffset now) =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            false,
            false,
            true,
            false,
            false,
            ControllerHealthInfo.Healthy,
            null,
            null,
            false,
            null,
            now,
            OperationalContext.None);

    private RestoreScheduledModeResult CompleteReadFailure(
        long commandRevision,
        string correlationId,
        DateTimeOffset requestedAt,
        CommandExecutionStatus status,
        string summary)
    {
        var completedAt = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (_revision != commandRevision)
            {
                EnergyHubServiceLog.RestoreSuperseded(_logger, _twin.AssetId, correlationId);
                return CreateSupersededResult(_twin.AssetId, null, correlationId);
            }

            _twin = _twin with
            {
                LastCommand = new CommandRecord(
                    RestoreScheduledModeOperation,
                    _twin.DesiredIsOn,
                    status,
                    correlationId,
                    requestedAt,
                    completedAt,
                    summary)
            };

            AddActivity(summary, correlationId, ActivityKind.Command, false, status);

            return new RestoreScheduledModeResult(
                _twin.AssetId,
                null,
                null,
                status,
                correlationId,
                summary,
                completedAt);
        }
    }

    private RestoreScheduledModeResult CompleteCommandFailure(
        long commandRevision,
        SmartPolePhysicalState physicalState,
        bool scheduledTarget,
        string correlationId,
        DateTimeOffset requestedAt,
        CommandExecutionStatus status,
        string summary,
        DateTimeOffset? completedAt = null)
    {
        ArgumentNullException.ThrowIfNull(physicalState);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        var effectiveCompletedAt = completedAt ?? _timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (_revision != commandRevision)
            {
                EnergyHubServiceLog.RestoreSuperseded(_logger, physicalState.AssetId, correlationId);
                return CreateSupersededResult(physicalState.AssetId, scheduledTarget, correlationId);
            }

            _twin = _twin with
            {
                DesiredIsOn = scheduledTarget,
                ReportedIsOn = physicalState.IsOn,
                ManualOverride = physicalState.ManualOverride,
                ControllerHealth = physicalState.ControllerHealth,
                LastReportedAt = physicalState.LastReportedAt,
                LastCommand = new CommandRecord(
                    RestoreScheduledModeOperation,
                    scheduledTarget,
                    status,
                    correlationId,
                    requestedAt,
                    effectiveCompletedAt,
                    summary)
            };

            AddActivity(summary, correlationId, ActivityKind.Command, false, status);

            return new RestoreScheduledModeResult(
                physicalState.AssetId,
                scheduledTarget,
                _twin.ReportedIsOn,
                status,
                correlationId,
                summary,
                effectiveCompletedAt);
        }
    }

    private RestoreScheduledModeResult CreateSupersededResult(string assetId, bool? scheduledTarget, string correlationId) =>
        new(
            assetId,
            scheduledTarget,
            null,
            CommandExecutionStatus.Failed,
            correlationId,
            SupersededSummary,
            _timeProvider.GetUtcNow());

    // Stamps the authoritative revision onto the twin handed out. Must be called while holding
    // the gate, so the state and the revision a caller later quotes back cannot disagree.
    private EnergyOperationalTwin CurrentTwin() => _twin with { StateRevision = _revision };

    private static EnergyOperationalTwin CreateTwinFromPhysical(
        SmartPolePhysicalState physicalState,
        bool desiredIsOn,
        string? openIncidentId,
        CommandRecord? lastCommand)
    {
        ArgumentNullException.ThrowIfNull(physicalState);

        return new EnergyOperationalTwin(
            physicalState.AssetId,
            physicalState.Area,
            physicalState.IsOn,
            desiredIsOn,
            physicalState.IsDaylight,
            physicalState.ExpectedScheduledState,
            physicalState.ManualOverride,
            physicalState.ControllerHealth,
            lastCommand ?? physicalState.LastCommand,
            physicalState.LastMaintenanceTime,
            physicalState.HasRecentMaintenance,
            openIncidentId,
            physicalState.LastReportedAt,
            physicalState.OperationContext);
    }

    private void AddActivity(
        string message,
        string correlationId,
        ActivityKind kind,
        bool isSuccess,
        CommandExecutionStatus? commandStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        _activity.Add(new ActivityRecord(
            Guid.NewGuid().ToString("N"),
            DemoAssets.StreetlightAssetId,
            correlationId,
            ActivitySource.EnergyHub,
            kind,
            message,
            _timeProvider.GetUtcNow(),
            isSuccess,
            commandStatus));
    }

    private static void EnsureAsset(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (!string.Equals(assetId, DemoAssets.StreetlightAssetId, StringComparison.OrdinalIgnoreCase)
            && !IsFixtureAsset(assetId))
        {
            throw new ArgumentException(
                $"The Energy Hub only exposes assets {DemoAssets.StreetlightAssetId} and {DemoAssets.SecondStreetlightAssetId}.",
                nameof(assetId));
        }
    }

    private static bool IsFixtureAsset(string assetId) =>
        string.Equals(assetId, DemoAssets.SecondStreetlightAssetId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates the deterministic read-only twin for the second streetlight fixture: on during
    /// daylight under a manual override, mirroring the primary asset's anomaly - but with no
    /// maintenance history and no work evidence anywhere, which is what the case-memory demo needs.
    /// </summary>
    /// <param name="now">The timestamp to stamp onto the fixture twin.</param>
    /// <returns>The fixture operational twin.</returns>
    public static EnergyOperationalTwin CreateSecondStreetlightFixtureTwin(DateTimeOffset now) =>
        new(
            DemoAssets.SecondStreetlightAssetId,
            DemoAssets.SouthPromenadeArea,
            ReportedIsOn: true,
            DesiredIsOn: true,
            IsDaylight: true,
            ExpectedScheduledState: false,
            ManualOverride: true,
            ControllerHealthInfo.Healthy,
            LastCommand: null,
            LastMaintenanceTime: null,
            HasRecentMaintenance: false,
            OpenIncidentId: null,
            now,
            OperationalContext.None);
}

internal static partial class EnergyHubServiceLog
{
    [LoggerMessage(
        EventId = 1500,
        Level = LogLevel.Information,
        Message = "Resetting Energy Hub state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetStarted(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Information,
        Message = "Energy Hub reset completed for asset {AssetId}. DesiredIsOn: {DesiredIsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetCompleted(ILogger logger, string assetId, string correlationId, bool desiredIsOn);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Information,
        Message = "Energy Hub reset was canceled for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetCanceled(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1503,
        Level = LogLevel.Warning,
        Message = "Energy Hub reset timed out while reading SmartPole state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1504,
        Level = LogLevel.Error,
        Message = "Energy Hub reset could not read SmartPole state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetReadFailed(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1505,
        Level = LogLevel.Error,
        Message = "Energy Hub reset received an invalid SmartPole state response for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetReadInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1506,
        Level = LogLevel.Information,
        Message = "Synchronizing Energy Hub scenario for asset {AssetId}. DesiredIsOn: {DesiredIsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioSynchronizationStarted(ILogger logger, string assetId, string correlationId, bool desiredIsOn);

    [LoggerMessage(
        EventId = 1507,
        Level = LogLevel.Information,
        Message = "Energy Hub scenario synchronized for asset {AssetId}. OpenIncidentId: {OpenIncidentId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioSynchronizationCompleted(ILogger logger, string assetId, string correlationId, string openIncidentId);

    [LoggerMessage(
        EventId = 1508,
        Level = LogLevel.Information,
        Message = "Energy Hub scenario synchronization was canceled for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioSynchronizationCanceled(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1509,
        Level = LogLevel.Warning,
        Message = "Energy Hub scenario synchronization timed out for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioSynchronizationTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1510,
        Level = LogLevel.Error,
        Message = "Energy Hub scenario synchronization could not read SmartPole state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioSynchronizationFailed(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1511,
        Level = LogLevel.Error,
        Message = "Energy Hub scenario synchronization received an invalid SmartPole state response for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioSynchronizationInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1512,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode requested for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreRequested(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 1513,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode was canceled before Energy Hub reread SmartPole state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCanceledBeforeRead(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1514,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode timed out while rereading SmartPole state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreReadTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1515,
        Level = LogLevel.Error,
        Message = "Restore Scheduled Mode could not reread SmartPole state for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreReadFailed(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1516,
        Level = LogLevel.Error,
        Message = "Restore Scheduled Mode received an invalid SmartPole state response for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreReadInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1517,
        Level = LogLevel.Information,
        Message = "Energy Hub sent Restore Scheduled Mode to SmartPole for asset {AssetId}. DesiredIsOn: {DesiredIsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCommandSent(ILogger logger, string assetId, bool desiredIsOn, string correlationId);

    [LoggerMessage(
        EventId = 1518,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode succeeded for asset {AssetId}. DesiredIsOn: {DesiredIsOn}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreSucceeded(ILogger logger, string assetId, bool desiredIsOn, string correlationId);

    [LoggerMessage(
        EventId = 1519,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode returned status {Status} for asset {AssetId}. CorrelationId: {CorrelationId}. Summary: {Summary}")]
    internal static partial void RestoreReturnedFailure(ILogger logger, string assetId, CommandExecutionStatus status, string correlationId, string summary);

    [LoggerMessage(
        EventId = 1520,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode was canceled after the SmartPole command was sent for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCanceledAfterSend(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1521,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode command timed out for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCommandTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1522,
        Level = LogLevel.Error,
        Message = "Restore Scheduled Mode command failed before confirmation for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCommandFailed(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1523,
        Level = LogLevel.Error,
        Message = "Restore Scheduled Mode received an invalid SmartPole command response for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCommandInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1524,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode was superseded by a scenario change or reset for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreSuperseded(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 1525,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode refused for asset {AssetId}: the caller validated revision {ExpectedRevision} but the authoritative revision is {CurrentRevision}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestorePreconditionFailed(ILogger logger, string assetId, string correlationId, long expectedRevision, long currentRevision);
}
