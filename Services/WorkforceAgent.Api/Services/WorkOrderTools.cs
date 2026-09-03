using System.ComponentModel;

namespace WorkforceAgent.Api.Services;

/// <summary>
/// The workforce agent's own tools, and the reason this stage is safe.
/// <para>
/// The agent has exactly two: one that says which work orders exist for an asset, and one that
/// opens a work order in its shareable projection. Neither can return a technician's name, badge,
/// labour cost or contracted rate, because the hub never selects those fields for this caller.
/// </para>
/// <para>
/// That is the whole mechanism. The agent is not instructed to keep a secret and it is not trusted
/// to redact one - the commercial and personal detail never enters its context, so there is no
/// instruction, jailbreak or clever framing from the asking domain that can extract it. The agent
/// may then reason and write freely, because everything it holds is already shareable.
/// </para>
/// </summary>
public sealed partial class WorkOrderTools(
    IWorkforceHubGateway hub,
    Func<string> correlationIdProvider,
    ILogger<WorkOrderTools> logger)
{
    /// <summary>The stable name of the work order search tool.</summary>
    public const string FindToolName = "find_work_orders_for_asset";

    /// <summary>The stable name of the shareable-details tool.</summary>
    public const string DetailsToolName = "get_shareable_work_order_details";

    private readonly IWorkforceHubGateway _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    private readonly Func<string> _correlationIdProvider = correlationIdProvider ?? throw new ArgumentNullException(nameof(correlationIdProvider));
    private readonly ILogger<WorkOrderTools> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Lists the work orders raised for an asset, newest first.
    /// </summary>
    /// <param name="assetId">The asset to search for, for example L-417.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The matching work orders, described well enough to choose between them.</returns>
    [Description("Finds the work orders raised for an asset. Returns identifiers, titles and status only - use the details tool to open one.")]
    public async Task<IReadOnlyList<WorkOrderSummary>> FindWorkOrdersForAssetAsync(
        [Description("The asset the caller asked about, for example L-417.")] string assetId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        var correlationId = _correlationIdProvider();
        var matches = await _hub.FindForAssetAsync(assetId, correlationId, cancellationToken);
        WorkOrderToolsLog.SearchRan(_logger, assetId, matches.Count, correlationId);
        return matches;
    }

    /// <summary>
    /// Opens one work order, in the projection this domain shares with other domains.
    /// </summary>
    /// <param name="workOrderId">The work order to open, as returned by the search.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The shareable details of the work order.</returns>
    [Description("Opens one work order and returns the details this domain shares with other domains: the reason, the status, the expected clearance time and the operational summary. Commercial and personal details are not part of this record and cannot be requested.")]
    public async Task<ShareableWorkOrderDetails> GetShareableWorkOrderDetailsAsync(
        [Description("The work order identifier, for example WO-8732.")] string workOrderId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workOrderId);

        var correlationId = _correlationIdProvider();
        var details = await _hub.GetShareableDetailsAsync(workOrderId, correlationId, cancellationToken)
            ?? throw new ArgumentException($"No work order {workOrderId} exists.", nameof(workOrderId));

        WorkOrderToolsLog.DetailsRead(_logger, workOrderId, correlationId);
        return details;
    }
}

internal static partial class WorkOrderToolsLog
{
    [LoggerMessage(
        EventId = 1820,
        Level = LogLevel.Information,
        Message = "Workforce Agent searched work orders for {AssetId}: {MatchCount} match(es). CorrelationId: {CorrelationId}.")]
    internal static partial void SearchRan(ILogger logger, string assetId, int matchCount, string correlationId);

    [LoggerMessage(
        EventId = 1821,
        Level = LogLevel.Information,
        Message = "Workforce Agent opened the shareable projection of {WorkOrderId}; no commercial or personal field entered its context. CorrelationId: {CorrelationId}.")]
    internal static partial void DetailsRead(ILogger logger, string workOrderId, string correlationId);
}
