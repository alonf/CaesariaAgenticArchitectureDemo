using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Resolves the tool-approval requests a run returns, then resumes the same session with the
/// operator's decisions. Separated from the agent so this control flow - which decides whether a
/// protected capability runs - can be exercised without a model.
/// </summary>
public sealed class ToolApprovalResolver(int maxRounds)
{
    private readonly int _maxRounds = maxRounds > 0
        ? maxRounds
        : throw new ArgumentOutOfRangeException(nameof(maxRounds));

    /// <summary>
    /// Carries each request to the operator and resumes until no approval requests remain.
    /// </summary>
    /// <param name="response">The response the run produced.</param>
    /// <param name="askOperator">Puts one request to the operator and returns their decision.</param>
    /// <param name="resume">Resumes the same session with the decisions.</param>
    /// <param name="onStandingRefusal">Reports a capability answered from a standing refusal rather than put to the operator again.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The response once no approval requests remain, with the decision made for each intercepted call.</returns>
    /// <exception cref="OperationsAgentApprovalLoopException">Requests still remained after the bounded rounds.</exception>
    public async Task<ToolApprovalOutcome> ResolveAsync(
        AgentResponse response,
        Func<ToolApprovalRequestContent, CancellationToken, Task<bool>> askOperator,
        Func<ChatMessage, CancellationToken, Task<AgentResponse>> resume,
        Action<string>? onStandingRefusal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(askOperator);
        ArgumentNullException.ThrowIfNull(resume);

        // A refusal stands for this request: if the model asks again for a capability that was
        // just refused, it is answered from the standing decision rather than asking again until
        // the execution budget runs out. The refusal is not attributed to the operator, because a
        // capability can also be refused by a stage downgrade after they approved it.
        HashSet<string> declined = new(StringComparer.Ordinal);

        // The decision for each intercepted call, keyed by the call it covers. A declined call
        // still produces a tool result ("invocation rejected"), so a result alone cannot say
        // whether a capability ran; the decision is what settles it.
        Dictionary<string, bool> decisions = new(StringComparer.Ordinal);

        for (var round = 0; round < _maxRounds; round++)
        {
            var requests = FindRequests(response);

            if (requests.Count == 0)
            {
                return new ToolApprovalOutcome(response, decisions);
            }

            List<AIContent> replies = [];

            foreach (var request in requests)
            {
                var toolName = DescribeToolCall(request).Name;
                var callId = (request.ToolCall as FunctionCallContent)?.CallId;

                if (declined.Contains(toolName))
                {
                    replies.Add(request.CreateResponse(false, "This capability was already refused for this request."));
                    RecordDecision(decisions, callId, approved: false);
                    onStandingRefusal?.Invoke(toolName);
                    continue;
                }

                var approved = await askOperator(request, cancellationToken);

                if (!approved)
                {
                    declined.Add(toolName);
                }

                replies.Add(request.CreateResponse(approved));
                RecordDecision(decisions, callId, approved);
            }

            response = await resume(new ChatMessage(ChatRole.User, replies), cancellationToken);
        }

        // The framework's guidance is to keep resolving until no approval requests remain. Bounded
        // here, so exhaustion is an explicit failure rather than a half-finished answer returned
        // and persisted as a completed turn.
        var unresolved = FindRequests(response)
            .Select(request => DescribeToolCall(request).Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return unresolved.Length == 0
            ? new ToolApprovalOutcome(response, decisions)
            : throw new OperationsAgentApprovalLoopException(_maxRounds, string.Join(", ", unresolved));
    }

    // A refusal is recorded even when it repeats an earlier one: the model asked again, and the
    // trace shows a second refused request rather than hiding it behind the first.
    private static void RecordDecision(Dictionary<string, bool> decisions, string? callId, bool approved)
    {
        if (!string.IsNullOrEmpty(callId))
        {
            decisions[callId] = approved;
        }
    }

    /// <summary>
    /// Describes the tool call a request covers, so the operator is shown which capability and
    /// which arguments they are approving. Arguments stay separate name/value pairs: flattening
    /// them into one string lets a model-authored value impersonate a further argument, and an
    /// approval given against a misleading display is not informed approval.
    /// </summary>
    /// <param name="request">The approval request.</param>
    /// <returns>The capability name and its arguments, one entry per declared argument.</returns>
    public static (string Name, IReadOnlyList<OperationsAgentToolArgument> Arguments) DescribeToolCall(
        ToolApprovalRequestContent request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ToolCall is not FunctionCallContent call)
        {
            return ("an unnamed capability", []);
        }

        IReadOnlyList<OperationsAgentToolArgument> arguments = call.Arguments is { Count: > 0 } callArguments
            ? [.. callArguments.Select(argument => new OperationsAgentToolArgument(argument.Key, argument.Value?.ToString() ?? string.Empty))]
            : [];

        return (call.Name, arguments);
    }

    private static List<ToolApprovalRequestContent> FindRequests(AgentResponse response) =>
        [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
}

/// <summary>
/// The result of resolving a run's tool approvals: the response once no requests remain, and what
/// the operator decided for each intercepted call.
/// </summary>
/// <param name="Response">The response with every approval request answered.</param>
/// <param name="Decisions">The decision per tool call identifier; absent for calls never intercepted.</param>
public sealed record ToolApprovalOutcome(AgentResponse Response, IReadOnlyDictionary<string, bool> Decisions);
