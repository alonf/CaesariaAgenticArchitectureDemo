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

    /// <summary>
    /// Answers a question that belongs to another domain by delegating it to that domain's agent
    /// over A2A, then composing the operator-facing answer from what came back.
    /// <para>
    /// The peer is not a tool: the model never selects it, and this service decides to delegate.
    /// Ownership of the answer stays here - the peer contributes what only it can know.
    /// </para>
    /// </summary>
    /// <param name="question">The operator's question.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The answer, carrying the consultation that produced it.</returns>
    public Task<OperationsAgentAnswer> ConsultWorkforceAsync(
        string question,
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
/// <param name="Delegations">The other-domain agents consulted during the run, with their sanitized answers.</param>
/// <param name="RemoteConsult">The peer agent consulted across a service boundary, when one was.</param>
public sealed record OperationsAgentAnswer(
    string Answer,
    string SessionId,
    IReadOnlyList<OperationsAgentToolCall> ToolCalls,
    IReadOnlyList<OperationsAgentEvidence> Evidence,
    IReadOnlyList<OperationsAgentRecalledCase> RecalledCases,
    IReadOnlyList<OperationsAgentSkill> Skills,
    OperationsAgentToolSource ToolSource,
    int ModelRoundTrips,
    IReadOnlyList<OperationsAgentDelegation> Delegations,
    OperationsAgentRemoteConsult? RemoteConsult = null);
