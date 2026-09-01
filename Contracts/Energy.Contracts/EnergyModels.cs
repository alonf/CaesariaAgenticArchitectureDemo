using System.ComponentModel.DataAnnotations;
using Caesarea.CanonicalModel;

namespace Energy.Contracts;

/// <summary>
/// Represents the Energy Hub state required to synchronize a deterministic scenario after SmartPole has been seeded.
/// </summary>
/// <param name="DesiredIsOn">The desired state the Energy Hub should project for the asset.</param>
/// <param name="OpenIncidentId">The currently open incident identifier, if any.</param>
/// <param name="Summary">The projector-friendly synchronization summary.</param>
public sealed record EnergyScenarioSyncRequest(
    bool DesiredIsOn,
    [property: StringLength(64)] string? OpenIncidentId,
    [property: Required]
    [property: StringLength(200, MinimumLength = 3)] string Summary);

/// <summary>
/// Represents the authoritative Energy Hub operational twin for the Stage 0 streetlight asset.
/// </summary>
/// <param name="AssetId">The asset identifier.</param>
/// <param name="Area">The area containing the asset.</param>
/// <param name="ReportedIsOn">The last confirmed physical state reported by SmartPole.</param>
/// <param name="DesiredIsOn">The desired operational target managed by the Energy Hub.</param>
/// <param name="IsDaylight">Indicates whether the asset is currently in daylight.</param>
/// <param name="ExpectedScheduledState">Indicates the state requested by the lighting schedule.</param>
/// <param name="ManualOverride">Indicates whether a manual override is currently active.</param>
/// <param name="ControllerHealth">The controller health reported by SmartPole.</param>
/// <param name="LastCommand">The most recent authoritative command record.</param>
/// <param name="LastMaintenanceTime">The most recent maintenance time shown to operators.</param>
/// <param name="HasRecentMaintenance">Indicates whether recent maintenance should be shown in the UI.</param>
/// <param name="OpenIncidentId">The current open incident identifier, if any.</param>
/// <param name="LastReportedAt">The time of the last authoritative SmartPole report.</param>
/// <param name="OperationContext">The contextual requirement that can override the normal schedule.</param>
/// <param name="StateRevision">
/// The authoritative state version, bumped by every mutation the Energy Hub accepts (reset,
/// scenario application, and each accepted command). A caller that validated state and then took
/// time to decide - a workflow waiting for an operator, for example - passes the revision it
/// validated back with its command, and the Energy Hub refuses to act on a stale picture.
/// </param>
public sealed record EnergyOperationalTwin(
    string AssetId,
    string Area,
    bool ReportedIsOn,
    bool DesiredIsOn,
    bool IsDaylight,
    bool ExpectedScheduledState,
    bool ManualOverride,
    ControllerHealthInfo ControllerHealth,
    CommandRecord? LastCommand,
    DateTimeOffset? LastMaintenanceTime,
    bool HasRecentMaintenance,
    string? OpenIncidentId,
    DateTimeOffset LastReportedAt,
    OperationalContext OperationContext,
    long StateRevision = 0)
{
    /// <summary>
    /// Gets the effective lighting target: what the lamp should be doing right now once
    /// cross-domain operational context is taken into account, not merely what the daylight
    /// schedule says.
    /// </summary>
    public bool EffectiveTargetIsOn => LightingTarget.Resolve(ExpectedScheduledState, OperationContext);

    /// <summary>
    /// Gets a value indicating whether the reported state disagrees with the effective target.
    /// </summary>
    public bool IsAnomalous => ReportedIsOn != EffectiveTargetIsOn;
}

/// <summary>
/// Represents the outcome of the narrow deterministic operation that returns the asset to scheduled mode.
/// </summary>
/// <param name="AssetId">The asset identifier targeted by the command.</param>
/// <param name="DesiredIsOn">The desired schedule-driven target, or <see langword="null"/> when an upstream failure prevents it from being known.</param>
/// <param name="ReportedIsOn">The authoritative reported state, or <see langword="null"/> when an upstream failure prevents it from being known.</param>
/// <param name="Status">The command execution status.</param>
/// <param name="CorrelationId">The correlation identifier spanning the end-to-end request.</param>
/// <param name="Summary">The projector-friendly completion summary.</param>
/// <param name="CompletedAt">The time at which the command completed.</param>
/// <param name="PreconditionFailed">
/// Indicates that the command was refused without any side effect because the caller's validated
/// state revision is no longer current. This is not a downstream failure: the caller decided
/// against a picture that has since moved, and must re-validate before commanding again.
/// </param>
public sealed record RestoreScheduledModeResult(
    string AssetId,
    bool? DesiredIsOn,
    bool? ReportedIsOn,
    CommandExecutionStatus Status,
    string CorrelationId,
    string Summary,
    DateTimeOffset CompletedAt,
    bool PreconditionFailed = false);

/// <summary>
/// Names the machine-readable markers the Energy Hub puts on command problem responses, so a
/// caller can tell a refused precondition ("your picture is stale, look again") from a genuine
/// downstream failure ("the city did not do what you asked") without parsing prose.
/// </summary>
public static class EnergyCommandProblem
{
    /// <summary>
    /// The problem-details extension set to <see langword="true"/> when a command was refused
    /// because the caller's validated state revision is no longer current.
    /// </summary>
    public const string PreconditionFailedExtension = "preconditionFailed";
}
