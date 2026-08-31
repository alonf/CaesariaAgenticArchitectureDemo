namespace OperationsAgent.Api.Services;

/// <summary>
/// Answers operator questions using the general Caesarea Operations Agent.
/// </summary>
public interface IOperationsAgent
{
    /// <summary>
    /// Runs one agent request, continuing the supplied conversational session when one is given.
    /// </summary>
    public Task<OperationsAgentAnswer> AskAsync(
        string question,
        string? sessionId,
        string correlationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Returns the agent's answer together with safe execution metadata for the UI capability trace.
/// </summary>
/// <param name="Answer">The agent's natural-language answer.</param>
/// <param name="SessionId">The conversational session a follow-up question can continue.</param>
/// <param name="ToolCalls">The tools the model invoked during the run, in order.</param>
/// <param name="Evidence">The work evidence the knowledge search returned during the run, deduplicated by identifier.</param>
/// <param name="RecalledCases">The closed cases the agent's memory recalled during the run.</param>
/// <param name="Skills">The skills advertised to the agent during the run and whether each was loaded.</param>
/// <param name="ToolSource">Where the streetlight tool came from for this run.</param>
/// <param name="ModelRoundTrips">The number of model round trips the run required.</param>
public sealed record OperationsAgentAnswer(
    string Answer,
    string SessionId,
    IReadOnlyList<OperationsAgentToolCall> ToolCalls,
    IReadOnlyList<OperationsAgentEvidence> Evidence,
    IReadOnlyList<OperationsAgentRecalledCase> RecalledCases,
    IReadOnlyList<OperationsAgentSkill> Skills,
    OperationsAgentToolSource ToolSource,
    int ModelRoundTrips);
