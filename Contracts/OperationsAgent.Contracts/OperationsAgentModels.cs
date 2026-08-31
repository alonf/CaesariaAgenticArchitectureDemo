namespace OperationsAgent.Contracts;

/// <summary>
/// Asks the general Caesarea Operations Agent a question.
/// </summary>
/// <param name="Question">The operator's natural-language question.</param>
public sealed record OperationsAgentRequest(string Question);

/// <summary>
/// Describes one tool invocation the model chose during an agent run. Safe execution metadata only:
/// tool name and validated arguments, never hidden reasoning or prompts.
/// </summary>
/// <param name="ToolName">The stable tool name the model invoked.</param>
/// <param name="Arguments">The validated tool arguments as compact JSON.</param>
public sealed record OperationsAgentToolCall(string ToolName, string Arguments);

/// <summary>
/// Returns the general agent's answer, safe execution metadata, and request correlation metadata.
/// </summary>
/// <param name="AgentName">The stable name of the general operations agent.</param>
/// <param name="Answer">The agent's natural-language answer.</param>
/// <param name="ToolCalls">The tools the model invoked during the run, in order.</param>
/// <param name="ModelRoundTrips">The number of model round trips the run required.</param>
/// <param name="CorrelationId">The correlation identifier spanning the request and tool call.</param>
public sealed record OperationsAgentResponse(
    string AgentName,
    string Answer,
    IReadOnlyList<OperationsAgentToolCall> ToolCalls,
    int ModelRoundTrips,
    string CorrelationId);
