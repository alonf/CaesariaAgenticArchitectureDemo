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
    /// <returns>A short confirmation naming the work item that was filed.</returns>
    [Description("Files a maintenance work item so a technician is dispatched to an asset. Use this when an investigation concludes that physical maintenance is needed. Filing a work item commits city resources, so it requires supervisor approval before it takes effect.")]
    public string CreateMaintenanceWorkItem(
        [Description("The asset needing maintenance, for example L-417.")] string assetId,
        [Description("A short description of what a technician needs to know.")] string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        // The model supplies both arguments, so both are validated. An over-long summary is
        // refused rather than silently shortened: the operator approved a specific call, and
        // quietly rewriting its arguments would make that approval mean something else.
        if (!CloseCaseValidation.IsCanonicalAssetId(assetId))
        {
            return $"'{assetId}' is not a canonical Caesarea asset identifier; nothing was filed.";
        }

        var trimmedSummary = summary.Trim();

        if (trimmedSummary.Length is 0 or > MaxSummaryLength)
        {
            return $"The summary must be between 1 and {MaxSummaryLength} characters; nothing was filed for {assetId}.";
        }

        // The stage check and the write share one critical section. The operator answered seconds
        // ago and the model called seconds later, so a downgrade can land in between - and a
        // withdrawn capability that writes anyway is exactly the failure this stage is about.
        if (!_stageGate.TryExecuteAtLeast(
                DemoStage.ToolApproval,
                () => _workItems.Create(assetId.ToUpperInvariant(), trimmedSummary, _correlationId),
                out var workItem))
        {
            MaintenanceToolsLog.WithdrawnByStage(_logger, assetId, _correlationId);
            return $"The demo stage no longer allows filing work items; nothing was filed for {assetId}.";
        }

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
