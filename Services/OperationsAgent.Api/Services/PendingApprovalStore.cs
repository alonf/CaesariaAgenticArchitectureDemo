namespace OperationsAgent.Api.Services;

/// <summary>
/// Bridges MCP interactive-input requests (MRTR) to the operator. When a remote tool pauses
/// input-required, the elicitation handler parks the question here; the Command Center lists it
/// and posts the operator's decision, which releases the paused tool call. If the agent run is
/// cancelled or times out, the pending entry is cancelled and removed - the tool never executes.
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
    /// <returns>The pending approval identifier and the decision task.</returns>
    public (string Id, Task<bool> Decision) Create(string message, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() =>
        {
            if (completion.TrySetCanceled(cancellationToken))
            {
                Remove(id);
            }
        });

        lock (_gate)
        {
            _pending[id] = new PendingEntry(
                new OperationsAgentPendingApproval(id, message, timeProvider.GetUtcNow(), correlationId),
                completion,
                registration);
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

    private void Remove(string id)
    {
        lock (_gate)
        {
            _pending.Remove(id);
        }
    }

    private sealed record PendingEntry(
        OperationsAgentPendingApproval Approval,
        TaskCompletionSource<bool> Completion,
        CancellationTokenRegistration Registration);
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
}
