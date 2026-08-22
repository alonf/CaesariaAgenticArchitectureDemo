namespace Caesarea.Contracts;

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

public sealed record CommandCenterScenarioContext(
    ScenarioStatus Scenario,
    IncidentRecord? OpenIncident,
    IReadOnlyList<ActivityRecord> Activity);

public sealed record CommandCenterSnapshot(
    string AssetId,
    string Area,
    string MapLabel,
    EnergyOperationalTwin OperationalState,
    ScenarioStatus CurrentScenario,
    IncidentRecord? OpenIncident,
    IReadOnlyList<ActivityRecord> RecentActivity);
