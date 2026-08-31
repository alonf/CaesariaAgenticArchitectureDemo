namespace OperationsAgent.Contracts;

/// <summary>
/// Asks the general Caesarea Operations Agent a question.
/// </summary>
/// <param name="Question">The operator's natural-language question.</param>
/// <param name="SessionId">The conversational session to continue, or <see langword="null"/> to start a new one.</param>
public sealed record OperationsAgentRequest(string Question, string? SessionId = null);

/// <summary>
/// Describes one tool invocation the model chose during an agent run. Safe execution metadata only:
/// tool name and validated arguments, never hidden reasoning or prompts.
/// </summary>
/// <param name="ToolName">The stable tool name the model invoked.</param>
/// <param name="Arguments">The validated tool arguments as compact JSON.</param>
public sealed record OperationsAgentToolCall(string ToolName, string Arguments);

/// <summary>
/// Well-known tool names the general agent exposes, shared so UI code never hard-codes them.
/// </summary>
public static class OperationsAgentToolNames
{
    /// <summary>The on-demand organizational work-knowledge search tool.</summary>
    public const string SearchWorkKnowledge = "search_work_knowledge";
}

/// <summary>
/// One piece of work evidence the knowledge search returned during an agent run. This is a
/// retrieval trace: it records what the search found, which is not the same claim as what the
/// agent cited - the model may examine a result and discard it as irrelevant.
/// </summary>
/// <param name="Id">The stable, human-citable evidence identifier, for example a work-order number.</param>
/// <param name="SourceType">The evidence kind, for example "Work order" or "Technician note".</param>
/// <param name="Title">The evidence title.</param>
/// <param name="Summary">The evidence content relevant to operational reasoning.</param>
/// <param name="OccurredAt">When the evidence was produced.</param>
/// <param name="SourceLabel">The provenance label, for example "Simulated work knowledge".</param>
/// <param name="SourceUri">A link to the original item when the provider has one, otherwise <see langword="null"/>.</param>
public sealed record OperationsAgentEvidence(
    string Id,
    string SourceType,
    string Title,
    string Summary,
    DateTimeOffset OccurredAt,
    string SourceLabel,
    string? SourceUri);

/// <summary>
/// Returns the general agent's answer, safe execution metadata, and request correlation metadata.
/// </summary>
/// <param name="AgentName">The stable name of the general operations agent.</param>
/// <param name="Answer">The agent's natural-language answer.</param>
/// <param name="SessionId">The conversational session a follow-up question can continue.</param>
/// <param name="ToolCalls">The tools the model invoked during the run, in order.</param>
/// <param name="Evidence">The work evidence the knowledge search returned during the run, deduplicated by identifier.</param>
/// <param name="ModelRoundTrips">The number of model round trips the run required.</param>
/// <param name="CorrelationId">The correlation identifier spanning the request and tool call.</param>
public sealed record OperationsAgentResponse(
    string AgentName,
    string Answer,
    string SessionId,
    IReadOnlyList<OperationsAgentToolCall> ToolCalls,
    IReadOnlyList<OperationsAgentEvidence> Evidence,
    int ModelRoundTrips,
    string CorrelationId);
