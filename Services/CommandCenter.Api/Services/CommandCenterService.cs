namespace CommandCenter.Api.Services;

/// <summary>
/// Aggregates deterministic Command Center state without bypassing the authoritative Energy Hub boundary.
/// </summary>
public sealed partial class CommandCenterService
{
    private readonly IEnergyHubGateway _energyHubGateway;
    private readonly IncidentModule _incidentModule;
    private readonly ActivityTimelineModule _activityTimelineModule;
    private readonly CustomerReportModule _customerReportModule;
    private readonly ScenarioContextModule _scenarioContextModule;
    private readonly SpatialContextModule _spatialContextModule;
    private readonly StageContextModule _stageContextModule;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CommandCenterService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandCenterService"/> class.
    /// </summary>
    /// <param name="energyHubGateway">The gateway used to read and command the authoritative Energy Hub.</param>
    /// <param name="incidentModule">The incident store used by the Command Center.</param>
    /// <param name="activityTimelineModule">The local activity timeline store.</param>
    /// <param name="customerReportModule">The inbound customer-report store.</param>
    /// <param name="scenarioContextModule">The module tracking the current deterministic scenario.</param>
    /// <param name="spatialContextModule">The module that validates the projected map asset.</param>
    /// <param name="stageContextModule">The module tracking the current demo stage propagated from the presenter switchboard.</param>
    /// <param name="timeProvider">The clock used for correlated activity timestamps.</param>
    /// <param name="logger">The logger used for scenario and command events.</param>
    public CommandCenterService(
        IEnergyHubGateway energyHubGateway,
        IncidentModule incidentModule,
        ActivityTimelineModule activityTimelineModule,
        CustomerReportModule customerReportModule,
        ScenarioContextModule scenarioContextModule,
        SpatialContextModule spatialContextModule,
        StageContextModule stageContextModule,
        TimeProvider timeProvider,
        ILogger<CommandCenterService> logger)
    {
        _energyHubGateway = energyHubGateway ?? throw new ArgumentNullException(nameof(energyHubGateway));
        _incidentModule = incidentModule ?? throw new ArgumentNullException(nameof(incidentModule));
        _activityTimelineModule = activityTimelineModule ?? throw new ArgumentNullException(nameof(activityTimelineModule));
        _customerReportModule = customerReportModule ?? throw new ArgumentNullException(nameof(customerReportModule));
        _scenarioContextModule = scenarioContextModule ?? throw new ArgumentNullException(nameof(scenarioContextModule));
        _spatialContextModule = spatialContextModule ?? throw new ArgumentNullException(nameof(spatialContextModule));
        _stageContextModule = stageContextModule ?? throw new ArgumentNullException(nameof(stageContextModule));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds the projector-friendly operational snapshot for the Command Center web experience.
    /// </summary>
    /// <param name="assetId">The asset identifier to project.</param>
    /// <param name="activityLimit">The maximum number of activity records to include.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The aggregated Command Center snapshot.</returns>
    public async Task<CommandCenterSnapshot> GetSnapshotAsync(string assetId, int activityLimit, string correlationId, CancellationToken cancellationToken)
    {
        _spatialContextModule.EnsureAsset(assetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activityLimit);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var operationalState = await _energyHubGateway.GetStateAsync(assetId, correlationId, cancellationToken);
        var activity = await GetRecentActivityAsync(assetId, activityLimit, correlationId, cancellationToken);
        var incident = ResolveOpenIncident(assetId, operationalState.OpenIncidentId);

        return new CommandCenterSnapshot(
            assetId,
            DemoAssets.NorthPromenadeArea,
            _spatialContextModule.GetMapLabel(assetId),
            operationalState,
            _scenarioContextModule.GetCurrent(),
            _customerReportModule.GetCurrent(),
            incident,
            activity,
            _stageContextModule.GetCurrent());
    }

    /// <summary>
    /// Gets the demo stage currently propagated from the presenter switchboard.
    /// </summary>
    /// <returns>The current demo stage.</returns>
    public DemoStageStatus GetCurrentStage() => _stageContextModule.GetCurrent();

    /// <summary>
    /// Applies the presenter-controlled demo stage propagated from the switchboard.
    /// </summary>
    /// <param name="stage">The demo stage to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the stage change.</param>
    /// <returns>The stage now current in the Command Center.</returns>
    public DemoStageStatus ApplyStage(DemoStageStatus stage, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var applied = _stageContextModule.SetCurrent(stage);
        CommandCenterServiceLog.StageApplied(_logger, applied.Name, correlationId);
        return applied;
    }

    /// <summary>
    /// Gets recent local and authoritative activity for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to project.</param>
    /// <param name="limit">The maximum number of activity records to include.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The merged recent activity ordered from newest to oldest.</returns>
    public async Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken)
    {
        _spatialContextModule.EnsureAsset(assetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var localActivity = _activityTimelineModule.GetRecent(limit);
        var energyActivity = await _energyHubGateway.GetRecentActivityAsync(assetId, limit, correlationId, cancellationToken);

        return localActivity
            .Concat(energyActivity)
            .OrderByDescending(record => record.OccurredAt)
            .Take(limit)
            .ToArray();
    }

    /// <summary>
    /// Gets a tracked incident by identifier.
    /// </summary>
    /// <param name="incidentId">The incident identifier to resolve.</param>
    /// <returns>The matching incident, or <see langword="null"/> when it does not exist.</returns>
    public IncidentRecord? GetIncident(string incidentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        return _incidentModule.Get(incidentId);
    }

    /// <summary>
    /// Marks the current customer report resolved: the operator closes the loop after the reported
    /// condition has been handled, and the resolution lands in the activity timeline.
    /// </summary>
    /// <param name="reportId">The customer report identifier to resolve.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns><see langword="false"/> when no matching report is currently open.</returns>
    public bool ResolveCustomerReport(string reportId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var report = _customerReportModule.GetCurrent();

        if (report is null || !string.Equals(report.Id, reportId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _customerReportModule.Reset();
        _activityTimelineModule.Add(CreateActivity(
            report.AssetId,
            correlationId,
            ActivityKind.Incident,
            $"Customer report {report.Id} marked resolved by the operator.",
            true,
            null));
        CommandCenterServiceLog.CustomerReportResolved(_logger, report.Id, correlationId);
        return true;
    }

    /// <summary>
    /// Requests the narrow deterministic Energy Hub operation that restores the asset to scheduled mode.
    /// </summary>
    /// <param name="assetId">The asset identifier to command.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The authoritative Energy Hub command result.</returns>
    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        _spatialContextModule.EnsureAsset(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        _activityTimelineModule.Add(CreateActivity(
            assetId,
            correlationId,
            ActivityKind.Command,
            "Operator requested Restore Scheduled Mode from the Command Center UI.",
            true,
            CommandExecutionStatus.Pending));

        CommandCenterServiceLog.RestoreRequested(_logger, assetId, correlationId);

        try
        {
            var result = await _energyHubGateway.RestoreScheduledModeAsync(assetId, correlationId, cancellationToken);

            _activityTimelineModule.Add(CreateActivity(
                assetId,
                correlationId,
                ActivityKind.Command,
                result.Summary,
                result.Status == CommandExecutionStatus.Succeeded,
                result.Status));

            if (result.Status == CommandExecutionStatus.Succeeded)
            {
                CommandCenterServiceLog.RestoreSucceeded(_logger, assetId, correlationId);
            }
            else if (result.Status == CommandExecutionStatus.TimedOut)
            {
                CommandCenterServiceLog.RestoreTimedOut(_logger, assetId, correlationId, result.Summary);
            }
            else
            {
                CommandCenterServiceLog.RestoreFailed(_logger, assetId, correlationId, result.Summary);
            }

            return result;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            const string summary = "Restore Scheduled Mode was canceled before the Energy Hub confirmed completion.";
            _activityTimelineModule.Add(CreateActivity(assetId, correlationId, ActivityKind.Command, summary, false, CommandExecutionStatus.Failed));
            CommandCenterServiceLog.RestoreCanceled(_logger, assetId, correlationId, exception);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            const string summary = "Command Center timed out waiting for the Energy Hub to complete Restore Scheduled Mode.";
            _activityTimelineModule.Add(CreateActivity(assetId, correlationId, ActivityKind.Command, summary, false, CommandExecutionStatus.TimedOut));
            CommandCenterServiceLog.RestoreGatewayTimedOut(_logger, assetId, correlationId, exception);
            return CreateFailureResult(assetId, correlationId, summary, CommandExecutionStatus.TimedOut);
        }
        catch (HttpRequestException exception)
        {
            const string summary = "Command Center could not reach the Energy Hub to complete Restore Scheduled Mode.";
            _activityTimelineModule.Add(CreateActivity(assetId, correlationId, ActivityKind.Command, summary, false, CommandExecutionStatus.Failed));
            CommandCenterServiceLog.RestoreGatewayFailed(_logger, assetId, correlationId, exception);
            return CreateFailureResult(assetId, correlationId, summary, CommandExecutionStatus.Failed);
        }
        catch (InvalidOperationException exception)
        {
            const string summary = "Command Center received an invalid Energy Hub response while restoring scheduled mode.";
            _activityTimelineModule.Add(CreateActivity(assetId, correlationId, ActivityKind.Command, summary, false, CommandExecutionStatus.Failed));
            CommandCenterServiceLog.RestoreGatewayInvalid(_logger, assetId, correlationId, exception);
            return CreateFailureResult(assetId, correlationId, summary, CommandExecutionStatus.Failed);
        }
    }

    /// <summary>
    /// Resets the local Command Center modules to their deterministic baseline state.
    /// The currently selected demo stage is intentionally preserved; presenters switch stages
    /// through the switchboard independently from resetting the L-417 scenario data.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the reset.</param>
    /// <returns>The resulting current scenario.</returns>
    public ScenarioStatus Reset(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        _incidentModule.Reset();
        _activityTimelineModule.Reset();
        _customerReportModule.Reset();

        CommandCenterServiceLog.ResetCompleted(_logger, correlationId);
        return _scenarioContextModule.Reset(correlationId);
    }

    /// <summary>
    /// Applies the presenter-controlled scenario context to the Command Center modules.
    /// </summary>
    /// <param name="scenarioContext">The scenario context to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the scenario application.</param>
    /// <returns>The resulting current scenario.</returns>
    public ScenarioStatus ApplyScenarioContext(CommandCenterScenarioContext scenarioContext, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(scenarioContext);
        ArgumentNullException.ThrowIfNull(scenarioContext.Scenario);
        ArgumentNullException.ThrowIfNull(scenarioContext.Activity);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        _incidentModule.Replace(scenarioContext.OpenIncident);
        _customerReportModule.Replace(scenarioContext.CustomerReport);
        _activityTimelineModule.Replace(scenarioContext.Activity);
        _activityTimelineModule.Add(CreateActivity(
            DemoAssets.StreetlightAssetId,
            correlationId,
            ActivityKind.Scenario,
            $"Scenario set to {scenarioContext.Scenario.Name}.",
            true,
            null));

        var status = _scenarioContextModule.SetCurrent(scenarioContext.Scenario);
        CommandCenterServiceLog.ScenarioApplied(_logger, status.Name, scenarioContext.OpenIncident?.Id ?? "none", correlationId);
        return status;
    }

    private RestoreScheduledModeResult CreateFailureResult(
        string assetId,
        string correlationId,
        string summary,
        CommandExecutionStatus status) =>
        new(
            assetId,
            null,
            null,
            status,
            correlationId,
            summary,
            _timeProvider.GetUtcNow());

    private ActivityRecord CreateActivity(
        string assetId,
        string correlationId,
        ActivityKind kind,
        string message,
        bool isSuccess,
        CommandExecutionStatus? commandStatus) =>
        new(
            Guid.NewGuid().ToString("N"),
            assetId,
            correlationId,
            ActivitySource.CommandCenter,
            kind,
            message,
            _timeProvider.GetUtcNow(),
            isSuccess,
            commandStatus);

    private IncidentRecord? ResolveOpenIncident(string assetId, string? incidentId)
    {
        if (!string.IsNullOrWhiteSpace(incidentId))
        {
            return _incidentModule.Get(incidentId);
        }

        return _incidentModule.GetOpenIncidentForAsset(assetId);
    }
}

internal static partial class CommandCenterServiceLog
{
    [LoggerMessage(
        EventId = 1800,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode requested from the Command Center for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreRequested(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 1811,
        Level = LogLevel.Information,
        Message = "Customer report {ReportId} marked resolved by the operator. CorrelationId: {CorrelationId}.")]
    internal static partial void CustomerReportResolved(ILogger logger, string reportId, string correlationId);

    [LoggerMessage(
        EventId = 1801,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode succeeded in the Command Center for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreSucceeded(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 1802,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode timed out in the Command Center for asset {AssetId}. CorrelationId: {CorrelationId}. Summary: {Summary}")]
    internal static partial void RestoreTimedOut(ILogger logger, string assetId, string correlationId, string summary);

    [LoggerMessage(
        EventId = 1803,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode failed in the Command Center for asset {AssetId}. CorrelationId: {CorrelationId}. Summary: {Summary}")]
    internal static partial void RestoreFailed(ILogger logger, string assetId, string correlationId, string summary);

    [LoggerMessage(
        EventId = 1804,
        Level = LogLevel.Information,
        Message = "Restore Scheduled Mode was canceled in the Command Center for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreCanceled(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1805,
        Level = LogLevel.Warning,
        Message = "Restore Scheduled Mode timed out while waiting for the Energy Hub for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreGatewayTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1806,
        Level = LogLevel.Error,
        Message = "Restore Scheduled Mode could not reach the Energy Hub for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreGatewayFailed(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1807,
        Level = LogLevel.Error,
        Message = "Restore Scheduled Mode received an invalid Energy Hub response for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void RestoreGatewayInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1808,
        Level = LogLevel.Information,
        Message = "Command Center local state reset to the deterministic baseline. CorrelationId: {CorrelationId}.")]
    internal static partial void ResetCompleted(ILogger logger, string correlationId);

    [LoggerMessage(
        EventId = 1809,
        Level = LogLevel.Information,
        Message = "Scenario {ScenarioName} applied to the Command Center. OpenIncidentId: {IncidentId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioApplied(ILogger logger, string scenarioName, string incidentId, string correlationId);

    [LoggerMessage(
        EventId = 1810,
        Level = LogLevel.Information,
        Message = "Demo stage {StageName} applied to the Command Center. CorrelationId: {CorrelationId}.")]
    internal static partial void StageApplied(ILogger logger, string stageName, string correlationId);
}
