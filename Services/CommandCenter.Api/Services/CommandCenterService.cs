using Caesarea.Contracts;

namespace CommandCenter.Api.Services;

public sealed class CommandCenterService
{
    private readonly IEnergyHubGateway _energyHubGateway;
    private readonly IncidentModule _incidentModule;
    private readonly ActivityTimelineModule _activityTimelineModule;
    private readonly ScenarioContextModule _scenarioContextModule;
    private readonly SpatialContextModule _spatialContextModule;
    private readonly TimeProvider _timeProvider;

    public CommandCenterService(
        IEnergyHubGateway energyHubGateway,
        IncidentModule incidentModule,
        ActivityTimelineModule activityTimelineModule,
        ScenarioContextModule scenarioContextModule,
        SpatialContextModule spatialContextModule,
        TimeProvider timeProvider)
    {
        _energyHubGateway = energyHubGateway;
        _incidentModule = incidentModule;
        _activityTimelineModule = activityTimelineModule;
        _scenarioContextModule = scenarioContextModule;
        _spatialContextModule = spatialContextModule;
        _timeProvider = timeProvider;
    }

    public async Task<CommandCenterSnapshot> GetSnapshotAsync(string assetId, int activityLimit, string correlationId, CancellationToken cancellationToken)
    {
        _spatialContextModule.EnsureAsset(assetId);

        var operationalState = await _energyHubGateway.GetStateAsync(assetId, correlationId, cancellationToken);
        var activity = await GetRecentActivityAsync(assetId, activityLimit, correlationId, cancellationToken);
        var incident = ResolveOpenIncident(assetId, operationalState.OpenIncidentId);

        return new CommandCenterSnapshot(
            assetId,
            DemoAssets.NorthPromenadeArea,
            _spatialContextModule.GetMapLabel(assetId),
            operationalState,
            _scenarioContextModule.GetCurrent(),
            incident,
            activity);
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken)
    {
        _spatialContextModule.EnsureAsset(assetId);

        var localActivity = _activityTimelineModule.GetRecent(limit);
        var energyActivity = await _energyHubGateway.GetRecentActivityAsync(assetId, limit, correlationId, cancellationToken);

        return localActivity
            .Concat(energyActivity)
            .OrderByDescending(record => record.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToArray();
    }

    public IncidentRecord? GetIncident(string incidentId) => _incidentModule.Get(incidentId);

    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        _spatialContextModule.EnsureAsset(assetId);

        _activityTimelineModule.Add(new ActivityRecord(
            Guid.NewGuid().ToString("N"),
            assetId,
            correlationId,
            ActivitySource.CommandCenter,
            ActivityKind.Command,
            "Operator requested Restore Scheduled Mode from the Command Center UI.",
            _timeProvider.GetUtcNow(),
            true,
            CommandExecutionStatus.Pending));

        var result = await _energyHubGateway.RestoreScheduledModeAsync(assetId, correlationId, cancellationToken);

        _activityTimelineModule.Add(new ActivityRecord(
            Guid.NewGuid().ToString("N"),
            assetId,
            correlationId,
            ActivitySource.CommandCenter,
            ActivityKind.Command,
            result.Summary,
            _timeProvider.GetUtcNow(),
            result.Status == CommandExecutionStatus.Succeeded,
            result.Status));

        return result;
    }

    public ScenarioStatus Reset(string correlationId)
    {
        _incidentModule.Reset();
        _activityTimelineModule.Reset();
        return _scenarioContextModule.Reset(correlationId);
    }

    public ScenarioStatus ApplyScenarioContext(CommandCenterScenarioContext scenarioContext, string correlationId)
    {
        _incidentModule.Replace(scenarioContext.OpenIncident);
        _activityTimelineModule.Replace(scenarioContext.Activity);
        _activityTimelineModule.Add(new ActivityRecord(
            Guid.NewGuid().ToString("N"),
            DemoAssets.StreetlightAssetId,
            correlationId,
            ActivitySource.CommandCenter,
            ActivityKind.Scenario,
            $"Scenario set to {scenarioContext.Scenario.Name}.",
            _timeProvider.GetUtcNow(),
            true,
            null));

        return _scenarioContextModule.SetCurrent(scenarioContext.Scenario);
    }

    private IncidentRecord? ResolveOpenIncident(string assetId, string? incidentId)
    {
        if (!string.IsNullOrWhiteSpace(incidentId))
        {
            return _incidentModule.Get(incidentId);
        }

        return _incidentModule.GetOpenIncidentForAsset(assetId);
    }
}
