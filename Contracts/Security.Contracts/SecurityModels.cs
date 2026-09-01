using System.ComponentModel.DataAnnotations;

namespace Security.Contracts;

/// <summary>
/// An active security operation as the Security Hub records it. Several fields are restricted:
/// they exist so the Security Agent can reason, and they must never cross the boundary into
/// another domain's agent.
/// </summary>
/// <param name="OperationId">The operation identifier.</param>
/// <param name="Area">The area the operation covers.</param>
/// <param name="RequiresLighting">Whether the operation requires the area to stay lit.</param>
/// <param name="StartedAt">When the operation began.</param>
/// <param name="EndsAt">When the operation is scheduled to end.</param>
/// <param name="Classification">The operation's classification - restricted.</param>
/// <param name="AuthorizedBy">The officer who authorized the operation - restricted.</param>
/// <param name="UnitCallSign">The unit executing the operation - restricted.</param>
/// <param name="Notes">Operational notes - restricted.</param>
public sealed record SecurityOperationRecord(
    string OperationId,
    string Area,
    bool RequiresLighting,
    DateTimeOffset StartedAt,
    DateTimeOffset EndsAt,
    string Classification,
    string AuthorizedBy,
    string UnitCallSign,
    string Notes);

/// <summary>
/// The Security Hub's view of one area.
/// </summary>
/// <param name="Area">The area described.</param>
/// <param name="Operations">The operations currently active in the area.</param>
/// <param name="ObservedAt">When the view was taken.</param>
public sealed record SecurityAreaStatus(
    string Area,
    IReadOnlyList<SecurityOperationRecord> Operations,
    DateTimeOffset ObservedAt);

/// <summary>
/// The closed set of conclusions the Security domain will disclose to another domain. The
/// consulted agent selects one; it never authors the words that cross the boundary, because free
/// text cannot be checked for confidentiality - a fragment, a paraphrase or an abbreviation of a
/// restricted value would all pass a substring test.
/// </summary>
public enum SecurityLightingReason
{
    /// <summary>No operation is active in the area.</summary>
    NoActiveOperation,

    /// <summary>An operation is active and requires the area to remain lit.</summary>
    ActiveOperationRequiresLighting,

    /// <summary>An operation is active but does not require lighting.</summary>
    ActiveOperationWithoutLightingRequirement
}

/// <summary>
/// What the Security domain advises the asking domain to do. This is the consulted agent's own
/// judgment: it reads the operations - several may overlap, with free-text notes and different
/// windows - and picks the advice that fits. It is deliberately advisory, and deliberately cannot
/// change <see cref="SecurityLightingAssessment.RequiresLighting"/> or its deadline, because a
/// model must never be able to switch a city's security lighting off.
/// </summary>
public enum SecurityLightingRecommendation
{
    /// <summary>Nothing is required of the asking domain.</summary>
    NoActionRequired,

    /// <summary>Leave the lighting as it is for now.</summary>
    LeaveLitUntilWindowEnds,

    /// <summary>Re-ask once the current window has passed.</summary>
    ReassessAfterWindow,

    /// <summary>The situation needs a human at the security desk.</summary>
    ContactSecurityDesk
}

/// <summary>
/// The sanitized answer that crosses the domain boundary: a decision, a closed-set reason, and a
/// deadline computed from the authoritative records. It never carries the operation record, a
/// restricted field, or any words the consulted model wrote.
/// </summary>
/// <param name="Area">The area assessed.</param>
/// <param name="RequiresLighting">Whether an active operation requires the area to stay lit.</param>
/// <param name="UntilUtc">When the requirement lapses, when one applies.</param>
/// <param name="ReasonCode">The closed-set conclusion.</param>
/// <param name="Recommendation">The consulted agent's advice - its judgment, and the one field it decides.</param>
/// <param name="Reason">The public explanation, rendered from a deterministic template.</param>
/// <param name="DetailsWithheld">Whether restricted detail was deliberately not disclosed.</param>
/// <param name="AssessedBy">The agent that produced the conclusion, so the consult is visible in a trace.</param>
public sealed record SecurityLightingAssessment(
    string Area,
    bool RequiresLighting,
    DateTimeOffset? UntilUtc,
    SecurityLightingReason ReasonCode,
    SecurityLightingRecommendation Recommendation,
    string Reason,
    bool DetailsWithheld,
    string AssessedBy);

/// <summary>
/// Synchronizes the Security Hub with a deterministic presenter scenario.
/// </summary>
/// <param name="Operations">The operations the scenario asserts, replacing any current ones.</param>
/// <param name="Summary">The projector-friendly description of the change.</param>
public sealed record SecurityScenarioSyncRequest(
    [property: Required] IReadOnlyList<SecurityOperationRecord> Operations,
    [property: Required] string Summary);
