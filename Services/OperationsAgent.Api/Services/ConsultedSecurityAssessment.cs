namespace OperationsAgent.Api.Services;

/// <summary>
/// The sanitized judgment the Security Operations Agent returns across the domain boundary, as
/// this service is allowed to see it.
/// <para>
/// It is declared here rather than shared from the Security domain's own contract assembly on
/// purpose: this service must have no compile-time path into that domain, so it models only the
/// shape that actually crosses - exactly what any external consumer of that agent would model.
/// Everything the specialist reasoned over stays on its side of the boundary.
/// </para>
/// </summary>
/// <param name="Area">The area assessed.</param>
/// <param name="RequiresLighting">The specialist's verdict.</param>
/// <param name="UntilUtc">When the requirement ends, when a deadline was stated.</param>
/// <param name="ReasonCode">The closed-set classification the specialist selected.</param>
/// <param name="Recommendation">What the specialist advised the asking domain to do.</param>
/// <param name="Reason">The specialist's public explanation, rendered by that domain from a template.</param>
/// <param name="DetailsWithheld">Whether operational detail exists that was deliberately not disclosed.</param>
/// <param name="AssessedBy">The consulted agent's name.</param>
internal sealed record ConsultedSecurityAssessment(
    string? Area,
    bool RequiresLighting,
    DateTimeOffset? UntilUtc,
    string? ReasonCode,
    string? Recommendation,
    string? Reason,
    bool DetailsWithheld,
    string? AssessedBy);
