using System.Collections.Concurrent;

namespace WorkforceHub.Api.Services;

/// <summary>
/// The workforce domain's system of record: work orders in full, including the commercial and
/// personal detail no other domain may see. Only this domain's own agent may read it, and even
/// that agent never receives the whole record - it receives what the extraction below selects.
/// </summary>
public sealed partial class WorkforceHubService(TimeProvider timeProvider, ILogger<WorkforceHubService> logger)
{
    private readonly ConcurrentDictionary<string, WorkOrderRecord> _workOrders = new(StringComparer.OrdinalIgnoreCase);
    private int _seeded;

    /// <summary>
    /// Finds the work orders raised for an asset, newest first, described only well enough to
    /// choose between them. A search result is itself a disclosure surface, so it carries no
    /// commercial or personal field.
    /// </summary>
    /// <param name="assetId">The asset to search for.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns>The matching work order summaries.</returns>
    public IReadOnlyList<WorkOrderSummary> FindForAsset(string assetId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        EnsureSeeded();

        var matches = _workOrders.Values
            .Where(record => string.Equals(record.AssetId, assetId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.RaisedAt)
            .Select(record => new WorkOrderSummary(
                record.WorkOrderId, record.AssetId, record.Title, record.Status, record.RaisedAt))
            .ToArray();

        WorkforceHubLog.SearchServed(logger, assetId, matches.Length, correlationId);
        return matches;
    }

    /// <summary>
    /// Returns the shareable projection of one work order.
    /// <para>
    /// This is the boundary. The commercial and personal fields are not redacted, masked or
    /// filtered out of a larger payload - they are never selected, so they never enter the caller's
    /// process at all. The domain's own agent calls this, which is why that agent cannot be talked
    /// into disclosing a technician's rate: it has never held one.
    /// </para>
    /// </summary>
    /// <param name="workOrderId">The work order to open.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns>The shareable details, or <see langword="null"/> when no such work order exists.</returns>
    public ShareableWorkOrderDetails? GetShareableDetails(string workOrderId, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workOrderId);

        EnsureSeeded();

        if (!_workOrders.TryGetValue(workOrderId, out var record))
        {
            WorkforceHubLog.DetailsMissing(logger, workOrderId, correlationId);
            return null;
        }

        WorkforceHubLog.DetailsServed(logger, workOrderId, correlationId);

        return new ShareableWorkOrderDetails(
            record.WorkOrderId,
            record.AssetId,
            record.Title,
            record.Reason,
            record.Status,
            record.RaisedAt,
            record.ExpectedClearanceAt,
            record.OperationalSummary);
    }

    /// <summary>
    /// Gets the complete records, for the presenter's own view of what the domain withheld. This is
    /// never reachable from another domain's agent - it exists so the lecture can show the two
    /// halves side by side.
    /// </summary>
    /// <returns>Every work order in full.</returns>
    public IReadOnlyList<WorkOrderRecord> GetAllInFull()
    {
        EnsureSeeded();
        return [.. _workOrders.Values.OrderByDescending(record => record.RaisedAt)];
    }

    /// <summary>
    /// Restores the default work orders.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns>The work orders now held.</returns>
    public IReadOnlyList<WorkOrderRecord> Reset(string correlationId)
    {
        _workOrders.Clear();
        Interlocked.Exchange(ref _seeded, 0);
        EnsureSeeded();
        WorkforceHubLog.Reset(logger, correlationId);
        return GetAllInFull();
    }

    // The default fixture: the visit that explains L-417's current state, and an older closed visit
    // so that finding the right one is a real step rather than a foregone conclusion.
    private void EnsureSeeded()
    {
        if (Interlocked.Exchange(ref _seeded, 1) == 1)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        WorkOrderRecord[] defaults =
        [
            new(
                "WO-8732",
                DemoAssets.StreetlightAssetId,
                "Luminaire maintenance",
                "Scheduled luminaire maintenance following a flicker report.",
                "Open - awaiting afternoon inspection",
                now.AddHours(-4),
                now.AddHours(2),
                "J. Cohen",
                "4471",
                1_240.00m,
                "Premium call-out (out-of-contract hours)",
                "Left the light ON for post-maintenance verification; override to be cleared once the afternoon "
                + "inspection confirms the luminaire is stable. Took three hours beyond the quote, so this one bills "
                + "at the premium call-out rate agreed for J. Cohen (badge 4471) - flag it to finance before invoicing.",
                "Left the light ON for post-maintenance verification. The manual override is to be cleared once the "
                + "afternoon inspection confirms the luminaire is stable."),
            new(
                "WO-8610",
                DemoAssets.StreetlightAssetId,
                "Lamp replacement",
                "End-of-life lamp replacement during the scheduled maintenance window.",
                "Closed",
                now.AddDays(-34),
                now.AddDays(-34).AddHours(3),
                "R. Mizrahi",
                "3902",
                310.00m,
                "Standard contract rate",
                "Swapped the lamp and confirmed the schedule. Routine, billed at the standard rate.",
                "Lamp replaced and the schedule confirmed. No override left in place.")
        ];

        foreach (var record in defaults)
        {
            _workOrders[record.WorkOrderId] = record;
        }
    }
}

internal static partial class WorkforceHubLog
{
    [LoggerMessage(
        EventId = 1810,
        Level = LogLevel.Information,
        Message = "Workforce Hub served a work order search for {AssetId}: {MatchCount} match(es). CorrelationId: {CorrelationId}.")]
    internal static partial void SearchServed(ILogger logger, string assetId, int matchCount, string correlationId);

    [LoggerMessage(
        EventId = 1811,
        Level = LogLevel.Information,
        Message = "Workforce Hub served the shareable projection of {WorkOrderId}; commercial and personal fields were not selected. CorrelationId: {CorrelationId}.")]
    internal static partial void DetailsServed(ILogger logger, string workOrderId, string correlationId);

    [LoggerMessage(
        EventId = 1812,
        Level = LogLevel.Warning,
        Message = "Workforce Hub has no work order {WorkOrderId}. CorrelationId: {CorrelationId}.")]
    internal static partial void DetailsMissing(ILogger logger, string workOrderId, string correlationId);

    [LoggerMessage(
        EventId = 1814,
        Level = LogLevel.Information,
        Message = "Workforce Hub reset to the default work orders. CorrelationId: {CorrelationId}.")]
    internal static partial void Reset(ILogger logger, string correlationId);
}
