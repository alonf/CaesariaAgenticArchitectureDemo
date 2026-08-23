namespace OperationsAgent.Api.Services;

/// <summary>
/// Represents the strict JSON schema the investigation model must return. This is an implementation detail of the
/// Operations Agent runner and is intentionally distinct from the public
/// <see cref="OperationsAgent.Contracts.InvestigationResult"/> contract, which also carries the evidence trace,
/// agent identity, correlation identifier, and timestamps assembled by the API.
/// </summary>
/// <param name="VerifiedFacts">The facts the model verified using its read-only tools.</param>
/// <param name="Hypotheses">The candidate explanations the model formed, each with a confidence and reason.</param>
/// <param name="MissingEvidence">The evidence the model could not obtain or that would be needed for certainty.</param>
/// <param name="Summary">The projector-friendly investigation summary.</param>
public sealed record ModelInvestigationResponse(
    IReadOnlyList<ModelVerifiedFact> VerifiedFacts,
    IReadOnlyList<ModelHypothesis> Hypotheses,
    IReadOnlyList<string> MissingEvidence,
    string Summary);

/// <summary>
/// Represents a single verified fact as produced by the investigation model.
/// </summary>
/// <param name="Statement">The verified factual statement.</param>
/// <param name="Source">The evidence source that grounds the statement.</param>
public sealed record ModelVerifiedFact(string Statement, string Source);

/// <summary>
/// Represents a single hypothesis as produced by the investigation model.
/// </summary>
/// <param name="Statement">The hypothesis statement.</param>
/// <param name="Confidence">The model's confidence in the hypothesis, expressed between 0 and 1.</param>
/// <param name="Reason">The reasoning that supports the hypothesis.</param>
public sealed record ModelHypothesis(string Statement, double Confidence, string Reason);
