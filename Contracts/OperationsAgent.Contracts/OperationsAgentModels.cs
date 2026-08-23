namespace OperationsAgent.Contracts;

/// <summary>
/// Represents the lifecycle outcome of a read-only Operations Agent investigation.
/// </summary>
public enum InvestigationStatus
{
    /// <summary>
    /// The Operations Agent produced a schema-valid investigation result.
    /// </summary>
    Completed,

    /// <summary>
    /// The investigation could not be completed. Reserved for future partial/async investigation flows;
    /// Stage 1 surfaces infrastructure failures as HTTP errors instead of returning this value.
    /// </summary>
    Failed
}

/// <summary>
/// Represents a single fact the Operations Agent verified using one of its read-only tools.
/// </summary>
/// <param name="Statement">The verified factual statement.</param>
/// <param name="Source">The evidence source that grounds the statement, such as a tool or system name.</param>
public sealed record VerifiedFact(string Statement, string Source);

/// <summary>
/// Represents a candidate explanation the Operations Agent has not fully verified.
/// </summary>
/// <param name="Statement">The hypothesis statement.</param>
/// <param name="Confidence">The agent's confidence in the hypothesis, expressed between 0 and 1.</param>
/// <param name="Reason">The reasoning that supports the hypothesis.</param>
public sealed record Hypothesis(string Statement, double Confidence, string Reason);

/// <summary>
/// Represents a single ordered read-only tool invocation performed while investigating an asset.
/// </summary>
/// <param name="Sequence">The 1-based order in which the tool was invoked during the investigation.</param>
/// <param name="ToolName">The stable name of the invoked read-only tool.</param>
/// <param name="AssetId">The asset identifier the tool call targeted.</param>
/// <param name="OutputSummary">The evidence summary returned by the tool.</param>
/// <param name="InvokedAt">The time at which the tool call completed.</param>
/// <param name="Succeeded">Indicates whether the tool call returned usable evidence.</param>
public sealed record EvidenceTraceEntry(
    int Sequence,
    string ToolName,
    string AssetId,
    string OutputSummary,
    DateTimeOffset InvokedAt,
    bool Succeeded);

/// <summary>
/// Represents the structured, evidence-grounded result of a read-only Operations Agent investigation.
/// </summary>
/// <param name="AssetId">The asset identifier that was investigated.</param>
/// <param name="AgentName">The projector-friendly name of the Operations Agent identity that produced the result.</param>
/// <param name="Status">The lifecycle outcome of the investigation.</param>
/// <param name="VerifiedFacts">The facts the agent verified using its read-only tools.</param>
/// <param name="Hypotheses">The candidate explanations the agent formed, each with a confidence and reason.</param>
/// <param name="MissingEvidence">The evidence the agent could not obtain or that would be needed for certainty.</param>
/// <param name="EvidenceTrace">The ordered read-only tool calls the agent made while investigating.</param>
/// <param name="Summary">The projector-friendly investigation summary.</param>
/// <param name="CorrelationId">The correlation identifier spanning the investigation request.</param>
/// <param name="StartedAt">The time at which the investigation started.</param>
/// <param name="CompletedAt">The time at which the investigation completed.</param>
public sealed record InvestigationResult(
    string AssetId,
    string AgentName,
    InvestigationStatus Status,
    IReadOnlyList<VerifiedFact> VerifiedFacts,
    IReadOnlyList<Hypothesis> Hypotheses,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<EvidenceTraceEntry> EvidenceTrace,
    string Summary,
    string CorrelationId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
