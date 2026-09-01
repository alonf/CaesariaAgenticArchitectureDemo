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
    string correlationId,
    ILogger<MaintenanceTools> logger)
{
    private readonly IWorkItemGateway _workItems = workItems ?? throw new ArgumentNullException(nameof(workItems));
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

        var workItem = await _workItems.CreateAsync(assetId, summary, _correlationId, cancellationToken);
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
}
