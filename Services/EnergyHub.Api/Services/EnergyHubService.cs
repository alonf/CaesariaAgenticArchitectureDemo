using Caesarea.Contracts;

namespace EnergyHub.Api.Services;

public sealed class EnergyHubService
{
    private readonly object _gate = new();
    private readonly ISmartPoleGateway _smartpoleGateway;
    private readonly TimeProvider _timeProvider;
    private readonly List<ActivityRecord> _activity = [];
    private EnergyOperationalTwin _twin;

    public EnergyHubService(ISmartPoleGateway smartpoleGateway, TimeProvider timeProvider)
    {
        _smartpoleGateway = smartpoleGateway;
        _timeProvider = timeProvider;
        _twin = CreateBaselineTwin(_timeProvider.GetUtcNow());
    }

    public EnergyOperationalTwin GetState(string assetId)
    {
        EnsureAsset(assetId);

        lock (_gate)
        {
            return _twin;
        }
    }

    public IReadOnlyList<ActivityRecord> GetRecentActivity(string assetId, int limit)
    {
        EnsureAsset(assetId);

        lock (_gate)
        {
            return _activity
                .OrderByDescending(record => record.OccurredAt)
                .Take(Math.Max(1, limit))
                .ToArray();
        }
    }

    public async Task<EnergyOperationalTwin> ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        var physicalState = await _smartpoleGateway.GetStateAsync(DemoAssets.StreetlightAssetId, correlationId, cancellationToken);
        var desiredIsOn = ComputeScheduledTarget(physicalState);

        lock (_gate)
        {
            _activity.Clear();
            _twin = CreateTwinFromPhysical(physicalState, desiredIsOn, null, null);
            AddActivity("Energy Hub reset to the current deterministic SmartPole state.", correlationId, ActivityKind.Synchronization, true, null);
            return _twin;
        }
    }

    public async Task<EnergyOperationalTwin> ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken)
    {
        var physicalState = await _smartpoleGateway.GetStateAsync(DemoAssets.StreetlightAssetId, correlationId, cancellationToken);

        lock (_gate)
        {
            _activity.Clear();
            _twin = CreateTwinFromPhysical(physicalState, request.DesiredIsOn, request.OpenIncidentId, null);
            AddActivity(request.Summary, correlationId, ActivityKind.Scenario, true, null);
            return _twin;
        }
    }

    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        EnsureAsset(assetId);

        var physicalState = await _smartpoleGateway.GetStateAsync(assetId, correlationId, cancellationToken);
        var scheduledTarget = ComputeScheduledTarget(physicalState);
        var requestedAt = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            _twin = CreateTwinFromPhysical(
                physicalState,
                scheduledTarget,
                _twin.OpenIncidentId,
                new CommandRecord(
                    "Restore scheduled mode",
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

        var commandResult = await _smartpoleGateway.SetLampStateAsync(new SetLampStateCommand(assetId, scheduledTarget), correlationId, cancellationToken);

        lock (_gate)
        {
            if (commandResult.Status == CommandExecutionStatus.Succeeded)
            {
                _twin = _twin with
                {
                    DesiredIsOn = scheduledTarget,
                    ReportedIsOn = commandResult.ActualIsOn ?? scheduledTarget,
                    ManualOverride = false,
                    LastReportedAt = commandResult.CompletedAt,
                    LastCommand = new CommandRecord(
                        "Restore scheduled mode",
                        scheduledTarget,
                        CommandExecutionStatus.Succeeded,
                        correlationId,
                        requestedAt,
                        commandResult.CompletedAt,
                        "SmartPole confirmed the scheduled state.")
                };

                AddActivity(
                    $"SmartPole confirmed the reported state as {(commandResult.ActualIsOn ?? scheduledTarget ? "On" : "Off")}.",
                    correlationId,
                    ActivityKind.Command,
                    true,
                    CommandExecutionStatus.Succeeded);
            }
            else
            {
                _twin = _twin with
                {
                    DesiredIsOn = scheduledTarget,
                    ReportedIsOn = physicalState.IsOn,
                    ManualOverride = physicalState.ManualOverride,
                    ControllerHealth = physicalState.ControllerHealth,
                    LastReportedAt = physicalState.LastReportedAt,
                    LastCommand = new CommandRecord(
                        "Restore scheduled mode",
                        scheduledTarget,
                        commandResult.Status,
                        correlationId,
                        requestedAt,
                        commandResult.CompletedAt,
                        commandResult.Summary)
                };

                AddActivity(
                    commandResult.Summary,
                    correlationId,
                    ActivityKind.Command,
                    false,
                    commandResult.Status);
            }

            return new RestoreScheduledModeResult(
                assetId,
                scheduledTarget,
                _twin.ReportedIsOn,
                commandResult.Status,
                correlationId,
                commandResult.Status == CommandExecutionStatus.Succeeded
                    ? "Scheduled mode restored after SmartPole confirmation."
                    : commandResult.Summary,
                commandResult.CompletedAt);
        }
    }

    public static bool ComputeScheduledTarget(SmartPolePhysicalState physicalState) =>
        physicalState.OperationContext.RequiresLighting || physicalState.ExpectedScheduledState;

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

    private EnergyOperationalTwin CreateTwinFromPhysical(
        SmartPolePhysicalState physicalState,
        bool desiredIsOn,
        string? openIncidentId,
        CommandRecord? lastCommand) =>
        new(
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

    private void AddActivity(
        string message,
        string correlationId,
        ActivityKind kind,
        bool isSuccess,
        CommandExecutionStatus? commandStatus)
    {
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
        if (!string.Equals(assetId, DemoAssets.StreetlightAssetId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The Energy Hub only exposes asset {DemoAssets.StreetlightAssetId}.", nameof(assetId));
        }
    }
}
