using System.ComponentModel.DataAnnotations;
using Caesarea.CanonicalModel;
using DemoScenario.Contracts;
using Energy.Contracts;

namespace CommandCenter.Contracts;

/// <summary>
/// Represents the severity of a tracked Command Center incident.
/// </summary>
public enum IncidentSeverity
{
    /// <summary>
    /// The incident is informational or advisory.
    /// </summary>
    Advisory,

    /// <summary>
    /// The incident requires operator attention but is not critical.
    /// </summary>
    Warning,

    /// <summary>
    /// The incident represents a critical operational issue.
    /// </summary>
    Critical
}

/// <summary>
/// Represents the lifecycle state of a Command Center incident.
/// </summary>
public enum IncidentStatus
{
    /// <summary>
    /// The incident remains active and unresolved.
    /// </summary>
    Open,

    /// <summary>
    /// The incident has been resolved.
    /// </summary>
    Resolved
}

/// <summary>
/// Represents a Command Center incident associated with the deterministic streetlight asset.
/// </summary>
/// <param name="Id">The incident identifier.</param>
/// <param name="AssetId">The affected asset identifier.</param>
/// <param name="Area">The affected area.</param>
/// <param name="Title">The incident title shown to operators.</param>
/// <param name="Description">The detailed incident description.</param>
/// <param name="Severity">The incident severity.</param>
/// <param name="Status">The incident lifecycle state.</param>
/// <param name="CreatedAt">The incident creation time.</param>
/// <param name="CorrelationId">The correlation identifier that created or seeded the incident.</param>
public sealed record IncidentRecord(
    string Id,
    string AssetId,
    string Area,
    string Title,
    string Description,
    IncidentSeverity Severity,
    IncidentStatus Status,
    DateTimeOffset CreatedAt,
    string CorrelationId);

/// <summary>
/// Represents a synthetic customer report received by the Command Center.
/// </summary>
/// <param name="Id">The stable report identifier.</param>
/// <param name="AssetId">The asset identified by the customer-report channel.</param>
/// <param name="Area">The reported area.</param>
/// <param name="Message">The customer's original message.</param>
/// <param name="ImageUrl">The URL of the synthetic image attachment.</param>
/// <param name="Source">The channel through which the report arrived.</param>
/// <param name="ReceivedAt">The time at which the Command Center received the report.</param>
/// <param name="CorrelationId">The correlation identifier assigned to the inbound report.</param>
public sealed record CustomerReportRecord(
    string Id,
    string AssetId,
    string Area,
    string Message,
    string ImageUrl,
    string Source,
    DateTimeOffset ReceivedAt,
    string CorrelationId);

/// <summary>
/// Represents the Command Center state that should be synchronized when a deterministic scenario is applied.
/// </summary>
/// <param name="Scenario">The current scenario status to expose to operators.</param>
/// <param name="CustomerReport">The synthetic inbound customer report to display, if any.</param>
/// <param name="OpenIncident">The open incident to seed, if any.</param>
/// <param name="Activity">The local activity timeline to seed in the Command Center.</param>
public sealed record CommandCenterScenarioContext(
    [property: Required] ScenarioStatus Scenario,
    CustomerReportRecord? CustomerReport,
    IncidentRecord? OpenIncident,
    [property: Required] IReadOnlyList<ActivityRecord> Activity);

/// <summary>
/// Represents the projector-friendly operational snapshot shown in the Command Center web UI.
/// </summary>
/// <param name="AssetId">The asset identifier.</param>
/// <param name="Area">The area containing the asset.</param>
/// <param name="MapLabel">The stable map label shown in the UI.</param>
/// <param name="OperationalState">The authoritative Energy Hub operational twin.</param>
/// <param name="CurrentScenario">The current deterministic scenario.</param>
/// <param name="CustomerReport">The inbound customer report that triggered the operational view, if any.</param>
/// <param name="OpenIncident">The current open incident, if any.</param>
/// <param name="RecentActivity">The recent correlated activity timeline.</param>
/// <param name="CurrentStage">The demo stage currently propagated from the presenter switchboard.</param>
public sealed record CommandCenterSnapshot(
    string AssetId,
    string Area,
    string MapLabel,
    EnergyOperationalTwin OperationalState,
    ScenarioStatus CurrentScenario,
    CustomerReportRecord? CustomerReport,
    IncidentRecord? OpenIncident,
    IReadOnlyList<ActivityRecord> RecentActivity,
    DemoStageStatus CurrentStage);
