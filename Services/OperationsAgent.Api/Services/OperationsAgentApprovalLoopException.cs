namespace OperationsAgent.Api.Services;

/// <summary>
/// Thrown when a run still holds unanswered tool-approval requests after the bounded number of
/// rounds. The turn is deliberately failed rather than returned: a response that still contains
/// approval requests is not an answer, and persisting it as one would leave the session claiming
/// a decision that nobody made.
/// </summary>
public sealed class OperationsAgentApprovalLoopException(int rounds, string toolNames)
    : Exception($"The agent still requested approval for {toolNames} after {rounds} rounds; the request was abandoned rather than answered on the operator's behalf.")
{
    /// <summary>
    /// Gets the number of approval rounds that were attempted.
    /// </summary>
    public int Rounds { get; } = rounds;
}
