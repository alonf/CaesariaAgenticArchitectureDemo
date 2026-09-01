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
/// The sanitized answer that crosses the domain boundary. It carries a decision and a
/// non-sensitive justification - never the operation record, and never a restricted field.
/// </summary>
/// <param name="Area">The area assessed.</param>
/// <param name="RequiresLighting">Whether an active operation requires the area to stay lit.</param>
/// <param name="UntilUtc">When the requirement lapses, when one applies.</param>
/// <param name="Reason">A short non-sensitive justification.</param>
/// <param name="DetailsWithheld">Whether restricted detail was deliberately not disclosed.</param>
public sealed record SecurityLightingAssessment(
    string Area,
    bool RequiresLighting,
    DateTimeOffset? UntilUtc,
    string Reason,
    bool DetailsWithheld);

/// <summary>
/// Synchronizes the Security Hub with a deterministic presenter scenario.
/// </summary>
/// <param name="Operations">The operations the scenario asserts, replacing any current ones.</param>
/// <param name="Summary">The projector-friendly description of the change.</param>
public sealed record SecurityScenarioSyncRequest(
    [property: Required] IReadOnlyList<SecurityOperationRecord> Operations,
    [property: Required] string Summary);
