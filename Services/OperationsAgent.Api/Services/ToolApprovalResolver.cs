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
    /// <returns>The response once no approval requests remain.</returns>
    /// <exception cref="OperationsAgentApprovalLoopException">Requests still remained after the bounded rounds.</exception>
    public async Task<AgentResponse> ResolveAsync(
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

        for (var round = 0; round < _maxRounds; round++)
        {
            var requests = FindRequests(response);

            if (requests.Count == 0)
            {
                return response;
            }

            List<AIContent> decisions = [];

            foreach (var request in requests)
            {
                var toolName = DescribeToolCall(request).Name;

                if (declined.Contains(toolName))
                {
                    decisions.Add(request.CreateResponse(false, "This capability was already refused for this request."));
                    onStandingRefusal?.Invoke(toolName);
                    continue;
                }

                var approved = await askOperator(request, cancellationToken);

                if (!approved)
                {
                    declined.Add(toolName);
                }

                decisions.Add(request.CreateResponse(approved));
            }

            response = await resume(new ChatMessage(ChatRole.User, decisions), cancellationToken);
        }

        // The framework's guidance is to keep resolving until no approval requests remain. Bounded
        // here, so exhaustion is an explicit failure rather than a half-finished answer returned
        // and persisted as a completed turn.
        var unresolved = FindRequests(response)
            .Select(request => DescribeToolCall(request).Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return unresolved.Length == 0
            ? response
            : throw new OperationsAgentApprovalLoopException(_maxRounds, string.Join(", ", unresolved));
    }

    /// <summary>
    /// Describes the tool call a request covers, so the operator is shown which capability and
    /// which arguments they are approving.
    /// </summary>
    /// <param name="request">The approval request.</param>
    /// <returns>The capability name and its arguments.</returns>
    public static (string Name, string Arguments) DescribeToolCall(ToolApprovalRequestContent request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ToolCall is not FunctionCallContent call)
        {
            return ("an unnamed capability", "no arguments");
        }

        var arguments = call.Arguments is { Count: > 0 } callArguments
            ? string.Join(", ", callArguments.Select(argument => $"{argument.Key}: {argument.Value}"))
            : "no arguments";

        return (call.Name, arguments);
    }

    private static List<ToolApprovalRequestContent> FindRequests(AgentResponse response) =>
        [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
}
