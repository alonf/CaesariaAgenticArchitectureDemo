namespace OperationsAgent.Api.Services;

/// <summary>
/// Bridges interactive-input requests (MCP MRTR and workflow approval gates) to the operator.
/// When a remote tool or workflow node pauses for input, the question parks here; the Command
/// Center lists it and posts the operator's decision, which releases the paused call. If the
/// agent run is cancelled or times out - or the demo stage is downgraded - pending entries are
/// cancelled and removed, and the paused work never executes.
/// </summary>
public sealed partial class PendingApprovalStore(TimeProvider timeProvider, ILogger<PendingApprovalStore> logger)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PendingEntry> _pending = new(StringComparer.Ordinal);

    /// <summary>
    /// Parks one interactive-input question and returns the task that completes with the
    /// operator's decision.
    /// </summary>
    /// <param name="message">The question the paused tool asked.</param>
    /// <param name="correlationId">The correlation identifier of the agent run.</param>
    /// <param name="cancellationToken">Cancels the wait when the agent run is abandoned.</param>
    /// <param name="controlPoint">Which control point raised the request.</param>
    /// <param name="toolName">The capability awaiting a decision, when one is named.</param>
    /// <param name="toolArguments">The arguments that capability would run with, when known.</param>
    /// <returns>The pending approval identifier and the decision task.</returns>
    public (string Id, Task<bool> Decision) Create(
        string message,
        string correlationId,
        CancellationToken cancellationToken,
        OperationsAgentControlPoint controlPoint = OperationsAgentControlPoint.InteractiveInput,
        string? toolName = null,
        string? toolArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = new PendingEntry(
            new OperationsAgentPendingApproval(
                id, message, timeProvider.GetUtcNow(), correlationId, controlPoint, toolName, toolArguments),
            completion);

        // Insert before registering the cancellation callback, so a token that fires at any
        // moment - including one that is already cancelled - always finds the entry to remove.
        lock (_gate)
        {
            _pending[id] = entry;
        }

        entry.Registration = cancellationToken.Register(() => Cancel(id, cancellationToken));

        if (cancellationToken.IsCancellationRequested)
        {
            // The registration may or may not have fired synchronously; this makes cleanup certain.
            Cancel(id, cancellationToken);
        }

        PendingApprovalLog.ApprovalRequested(logger, id, correlationId);
        return (id, completion.Task);
    }

    /// <summary>
    /// Gets the questions currently awaiting the operator, oldest first.
    /// </summary>
    public IReadOnlyList<OperationsAgentPendingApproval> GetAll()
    {
        lock (_gate)
        {
            return [.. _pending.Values
                .Select(entry => entry.Approval)
                .OrderBy(approval => approval.RequestedAt)];
        }
    }

    /// <summary>
    /// Records the operator's decision for one pending approval, releasing the paused tool call.
    /// </summary>
    /// <param name="id">The pending approval identifier.</param>
    /// <param name="approved">The operator's decision.</param>
    /// <returns><see langword="false"/> when the identifier is unknown or already answered.</returns>
    public bool TryRespond(string id, bool approved)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        PendingEntry? entry;

        lock (_gate)
        {
            if (!_pending.Remove(id, out entry))
            {
                return false;
            }
        }

        entry.Registration.Dispose();
        var answered = entry.Completion.TrySetResult(approved);

        if (answered)
        {
            PendingApprovalLog.ApprovalAnswered(logger, id, approved);
        }

        return answered;
    }

    /// <summary>
    /// Cancels every pending approval - used when the demo stage moves backward, so a request
    /// composed at a higher stage can never be approved and executed at a lower one.
    /// </summary>
    /// <returns>The number of approvals cancelled.</returns>
    public int CancelAll()
    {
        List<PendingEntry> entries;

        lock (_gate)
        {
            entries = [.. _pending.Values];
            _pending.Clear();
        }

        foreach (var entry in entries)
        {
            entry.Registration.Dispose();
            entry.Completion.TrySetCanceled();
        }

        if (entries.Count > 0)
        {
            PendingApprovalLog.ApprovalsCancelled(logger, entries.Count);
        }

        return entries.Count;
    }

    private void Cancel(string id, CancellationToken cancellationToken)
    {
        PendingEntry? entry;

        lock (_gate)
        {
            if (!_pending.Remove(id, out entry))
            {
                return;
            }
        }

        entry.Registration.Dispose();
        entry.Completion.TrySetCanceled(cancellationToken);
    }

    private sealed record PendingEntry(
        OperationsAgentPendingApproval Approval,
        TaskCompletionSource<bool> Completion)
    {
        public CancellationTokenRegistration Registration { get; set; }
    }
}

internal static partial class PendingApprovalLog
{
    [LoggerMessage(
        EventId = 2630,
        Level = LogLevel.Information,
        Message = "Interactive input {ApprovalId} awaiting the operator. CorrelationId: {CorrelationId}.")]
    internal static partial void ApprovalRequested(ILogger logger, string approvalId, string correlationId);

    [LoggerMessage(
        EventId = 2631,
        Level = LogLevel.Information,
        Message = "Interactive input {ApprovalId} answered: approved={Approved}.")]
    internal static partial void ApprovalAnswered(ILogger logger, string approvalId, bool approved);

    [LoggerMessage(
        EventId = 2632,
        Level = LogLevel.Warning,
        Message = "{CancelledCount} pending interactive input(s) cancelled by a stage downgrade.")]
    internal static partial void ApprovalsCancelled(ILogger logger, int cancelledCount);
}
