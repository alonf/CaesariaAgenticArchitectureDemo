using System.ComponentModel;

namespace OperationsAgent.Api.Services;

/// <summary>
/// The demo's sensitive administrative capability: filing a maintenance work item. Unlike the
/// physical correction - which the workflow owns from the Workflow stage on - this is an action
/// the model may reasonably decide to take by itself after an investigation, which is exactly why
/// it is the one wrapped for supervisor approval.
/// </summary>
public sealed partial class MaintenanceTools(
    IWorkItemGateway workItems,
    DemoStageGate stageGate,
    string correlationId,
    ILogger<MaintenanceTools> logger)
{
    private const int MaxSummaryLength = 500;

    private readonly IWorkItemGateway _workItems = workItems ?? throw new ArgumentNullException(nameof(workItems));
    private readonly DemoStageGate _stageGate = stageGate ?? throw new ArgumentNullException(nameof(stageGate));
    private readonly string _correlationId = string.IsNullOrWhiteSpace(correlationId)
        ? throw new ArgumentException("A correlation identifier is required.", nameof(correlationId))
        : correlationId;
    private readonly ILogger<MaintenanceTools> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Files a maintenance work item for an asset.
    /// </summary>
    /// <param name="assetId">The asset needing maintenance.</param>
    /// <param name="summary">What a technician needs to know.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A short confirmation naming the work item that was filed.</returns>
    [Description("Files a maintenance work item so a technician is dispatched to an asset. Use this when an investigation concludes that physical maintenance is needed. Filing a work item commits city resources, so it requires supervisor approval before it takes effect.")]
    public async Task<string> CreateMaintenanceWorkItemAsync(
        [Description("The asset needing maintenance, for example L-417.")] string assetId,
        [Description("A short description of what a technician needs to know.")] string summary,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        // The approval was granted at a stage that may since have been left behind. The capability
        // is rechecked here, immediately before the side effect, because the operator answered
        // some seconds ago and the model called this some seconds later.
        if (_stageGate.GetCurrent().Id < DemoStage.ToolApproval)
        {
            MaintenanceToolsLog.WithdrawnByStage(_logger, assetId, _correlationId);
            return $"The demo stage no longer allows filing work items; nothing was filed for {assetId}.";
        }

        // The model supplies both arguments, so both are validated: an unrecognized asset must not
        // enter the work-item store, and an unbounded summary must not enter the logs.
        if (!CloseCaseValidation.IsKnownAssetId(assetId))
        {
            return $"'{assetId}' is not a recognized Caesarea asset identifier; nothing was filed.";
        }

        var trimmedSummary = summary.Length <= MaxSummaryLength ? summary.Trim() : summary[..MaxSummaryLength].Trim();
        var workItem = await _workItems.CreateAsync(assetId.ToUpperInvariant(), trimmedSummary, _correlationId, cancellationToken);
        MaintenanceToolsLog.WorkItemFiled(_logger, workItem.WorkItemId, assetId, _correlationId);

        return $"Maintenance work item {workItem.WorkItemId} filed for {assetId}.";
    }
}

internal static partial class MaintenanceToolsLog
{
    [LoggerMessage(
        EventId = 2660,
        Level = LogLevel.Information,
        Message = "Operations Agent filed maintenance work item {WorkItemId} for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void WorkItemFiled(ILogger logger, string workItemId, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2661,
        Level = LogLevel.Warning,
        Message = "Maintenance work item for asset {AssetId} was not filed: the demo stage left ToolApproval after the approval was given. CorrelationId: {CorrelationId}.")]
    internal static partial void WithdrawnByStage(ILogger logger, string assetId, string correlationId);
}
