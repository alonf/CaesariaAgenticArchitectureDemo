using System.Collections.Concurrent;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Creates maintenance work items for corrections the automated workflow could not complete.
/// </summary>
public interface IWorkItemGateway
{
    /// <summary>
    /// Raises a maintenance work item for an asset.
    /// </summary>
    /// <param name="assetId">The asset needing maintenance.</param>
    /// <param name="summary">Why the automated correction did not resolve the anomaly.</param>
    /// <param name="correlationId">The correlation identifier of the workflow run.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The created work item.</returns>
    public Task<MaintenanceWorkItem> CreateAsync(string assetId, string summary, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the work items raised so far, newest first.
    /// </summary>
    /// <returns>The recorded work items.</returns>
    public IReadOnlyList<MaintenanceWorkItem> GetAll();
}

/// <summary>
/// The demo's work-management emulator: an in-memory, bounded stand-in for the city's real
/// work-management system, mirroring how the simulated work-knowledge search stands in for the
/// real one. A deployment would implement this interface against the actual service.
/// </summary>
public sealed partial class SimulatedWorkItemGateway(TimeProvider timeProvider, ILogger<SimulatedWorkItemGateway> logger) : IWorkItemGateway
{
    private const int MaxRetainedWorkItems = 50;

    private readonly ConcurrentQueue<MaintenanceWorkItem> _workItems = new();
    private int _sequence;

    /// <inheritdoc />
    public Task<MaintenanceWorkItem> CreateAsync(string assetId, string summary, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var workItem = new MaintenanceWorkItem(
            $"WI-{Interlocked.Increment(ref _sequence)}",
            assetId,
            summary,
            timeProvider.GetUtcNow(),
            correlationId);
        _workItems.Enqueue(workItem);

        while (_workItems.Count > MaxRetainedWorkItems && _workItems.TryDequeue(out _))
        {
            // A presenter console keeps only the recent tail; the emulator is not a system of record.
        }

        WorkItemLog.WorkItemCreated(logger, workItem.WorkItemId, assetId, correlationId);
        return Task.FromResult(workItem);
    }

    /// <inheritdoc />
    public IReadOnlyList<MaintenanceWorkItem> GetAll() =>
        [.. _workItems.Reverse()];
}

internal static partial class WorkItemLog
{
    [LoggerMessage(
        EventId = 2650,
        Level = LogLevel.Warning,
        Message = "Maintenance work item {WorkItemId} raised for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void WorkItemCreated(ILogger logger, string workItemId, string assetId, string correlationId);
}
