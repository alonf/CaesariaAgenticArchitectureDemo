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

    /// <summary>The skill-loading tool contributed by the agent skills provider.</summary>
    public const string LoadSkill = "load_skill";
}

/// <summary>
/// Identifies where the agent's streetlight tool comes from for a run.
/// </summary>
public enum OperationsAgentToolSource
{
    /// <summary>The tool is a local function compiled into the agent service.</summary>
    Local,

    /// <summary>The tool is discovered at runtime from the Energy Hub's MCP server.</summary>
    Mcp
}

/// <summary>
/// Reports the presenter-selected tool source.
/// </summary>
/// <param name="Source">The tool source used for subsequent agent runs.</param>
public sealed record OperationsAgentToolSourceStatus(OperationsAgentToolSource Source);

/// <summary>
/// One skill the agent could discover during a run. Skills are documented procedures - versioned,
/// expert-authored, auditable text - that the agent loads on demand; the trace shows which were
/// advertised and which the model actually loaded.
/// </summary>
/// <param name="Name">The stable skill name, for example streetlight-investigation.</param>
/// <param name="Description">The advertised skill description.</param>
/// <param name="Loaded">Whether the model loaded the full skill body during the run.</param>
public sealed record OperationsAgentSkill(string Name, string Description, bool Loaded);

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
/// One closed case the agent's memory recalled during a run. Recall is a hypothesis trace: it
/// records what past experience the agent was reminded of, which is never evidence about the
/// current asset.
/// </summary>
/// <param name="CaseId">The stable case identifier, for example CASE-1.</param>
/// <param name="AssetId">The asset the recalled case concerned.</param>
/// <param name="Symptom">The observed symptom that opened the recalled case.</param>
/// <param name="Resolution">The conclusion the recalled case closed with.</param>
/// <param name="ClosedAt">When the recalled case was closed.</param>
public sealed record OperationsAgentRecalledCase(
    string CaseId,
    string AssetId,
    string Symptom,
    string Resolution,
    DateTimeOffset ClosedAt);

/// <summary>
/// Asks the agent service to record a closed case in its memory.
/// </summary>
/// <param name="AssetId">The asset the case concerned.</param>
/// <param name="Symptom">The observed symptom that opened the case.</param>
/// <param name="Resolution">The closing conclusion, typically assembled from the agent's answer.</param>
public sealed record OperationsAgentCloseCaseRequest(string AssetId, string Symptom, string Resolution);

/// <summary>
/// Reports the current state of the agent's case memory.
/// </summary>
/// <param name="Cases">The closed cases the memory holds, newest first.</param>
public sealed record OperationsAgentCaseMemoryStatus(IReadOnlyList<OperationsAgentRecalledCase> Cases);

/// <summary>
/// Returns the general agent's answer, safe execution metadata, and request correlation metadata.
/// </summary>
/// <param name="AgentName">The stable name of the general operations agent.</param>
/// <param name="Answer">The agent's natural-language answer.</param>
/// <param name="SessionId">The conversational session a follow-up question can continue.</param>
/// <param name="ToolCalls">The tools the model invoked during the run, in order.</param>
/// <param name="Evidence">The work evidence the knowledge search returned during the run, deduplicated by identifier.</param>
/// <param name="RecalledCases">The closed cases the agent's memory recalled during the run.</param>
/// <param name="Skills">The skills advertised to the agent during the run and whether each was loaded.</param>
/// <param name="ToolSource">Where the streetlight tool came from for this run.</param>
/// <param name="ModelRoundTrips">The number of model round trips the run required.</param>
/// <param name="CorrelationId">The correlation identifier spanning the request and tool call.</param>
public sealed record OperationsAgentResponse(
    string AgentName,
    string Answer,
    string SessionId,
    IReadOnlyList<OperationsAgentToolCall> ToolCalls,
    IReadOnlyList<OperationsAgentEvidence> Evidence,
    IReadOnlyList<OperationsAgentRecalledCase> RecalledCases,
    IReadOnlyList<OperationsAgentSkill> Skills,
    OperationsAgentToolSource ToolSource,
    int ModelRoundTrips,
    string CorrelationId);
