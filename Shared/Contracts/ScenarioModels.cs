namespace Caesarea.Contracts;

public sealed record ScenarioDescriptor(ScenarioId Id, string Name, string Description);

public sealed record ScenarioStatus(
    ScenarioId Id,
    string Name,
    string Description,
    DateTimeOffset AppliedAt,
    string CorrelationId);

public sealed record ScenarioCatalogResponse(
    IReadOnlyList<ScenarioDescriptor> Scenarios,
    ScenarioStatus CurrentScenario);

public sealed record ScenarioApplicationResult(
    ScenarioStatus CurrentScenario,
    string Summary);
