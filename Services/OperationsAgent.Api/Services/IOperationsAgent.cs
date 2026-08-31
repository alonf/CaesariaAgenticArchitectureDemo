namespace OperationsAgent.Api.Services;

/// <summary>
/// Answers operator questions using the general Caesarea Operations Agent.
/// </summary>
public interface IOperationsAgent
{
    /// <summary>
    /// Runs one independent agent request.
    /// </summary>
    public Task<OperationsAgentAnswer> AskAsync(
        string question,
        string correlationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Returns the agent's answer together with safe execution metadata for the UI capability trace.
/// </summary>
/// <param name="Answer">The agent's natural-language answer.</param>
/// <param name="ToolCalls">The tools the model invoked during the run, in order.</param>
/// <param name="ModelRoundTrips">The number of model round trips the run required.</param>
public sealed record OperationsAgentAnswer(
    string Answer,
    IReadOnlyList<OperationsAgentToolCall> ToolCalls,
    int ModelRoundTrips);
