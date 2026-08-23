namespace DemoScenario.Contracts;

/// <summary>
/// Identifies the deterministic scenarios available to the presenter console.
/// </summary>
public enum ScenarioId
{
    /// <summary>
    /// Seeds a daytime forgotten override where the lamp remains on against schedule.
    /// </summary>
    ForgottenOverride,

    /// <summary>
    /// Seeds a daytime security operation that requires lighting.
    /// </summary>
    SecurityOperation,

    /// <summary>
    /// Seeds a controller fault that prevents scheduled restoration.
    /// </summary>
    ControllerFault,

    /// <summary>
    /// Seeds an already acknowledged anomaly with an existing incident.
    /// </summary>
    ExistingIncident,

    /// <summary>
    /// Seeds the normal daytime deterministic baseline.
    /// </summary>
    NormalOperation,

    /// <summary>
    /// Seeds the normal night-time deterministic baseline.
    /// </summary>
    NightOperation
}

/// <summary>
/// Describes the lifecycle state of a coordinated scenario application.
/// </summary>
public enum ScenarioApplicationStatus
{
    /// <summary>
    /// The scenario is being applied across deterministic services.
    /// </summary>
    Applying,

    /// <summary>
    /// The scenario was applied successfully across deterministic services.
    /// </summary>
    Applied,

    /// <summary>
    /// The scenario application failed and downstream state may require a reset.
    /// </summary>
    Failed
}

/// <summary>
/// Describes a deterministic scenario available to the presenter console.
/// </summary>
/// <param name="Id">The scenario identifier.</param>
/// <param name="Name">The projector-friendly scenario name.</param>
/// <param name="Description">The scenario description shown in the presenter UI.</param>
public sealed record ScenarioDescriptor(ScenarioId Id, string Name, string Description);

/// <summary>
/// Represents the scenario currently applied across the deterministic Stage 0 services.
/// </summary>
/// <param name="Id">The current scenario identifier.</param>
/// <param name="Name">The projector-friendly scenario name.</param>
/// <param name="Description">The scenario description shown in the UI.</param>
/// <param name="AppliedAt">The time at which the scenario was applied.</param>
/// <param name="CorrelationId">The correlation identifier spanning the scenario application.</param>
/// <param name="ApplicationStatus">The lifecycle state of the coordinated scenario application.</param>
/// <param name="FailureSummary">The failure summary when the coordinated operation does not complete.</param>
public sealed record ScenarioStatus(
    ScenarioId Id,
    string Name,
    string Description,
    DateTimeOffset AppliedAt,
    string CorrelationId,
    ScenarioApplicationStatus ApplicationStatus = ScenarioApplicationStatus.Applied,
    string? FailureSummary = null);

/// <summary>
/// Represents the scenario catalog returned to the presenter console.
/// </summary>
/// <param name="Scenarios">The available deterministic scenarios.</param>
/// <param name="CurrentScenario">The scenario currently applied across the services.</param>
public sealed record ScenarioCatalogResponse(
    IReadOnlyList<ScenarioDescriptor> Scenarios,
    ScenarioStatus CurrentScenario);

/// <summary>
/// Represents the result of applying or resetting a deterministic scenario.
/// </summary>
/// <param name="CurrentScenario">The scenario that is now current.</param>
/// <param name="Summary">The projector-friendly outcome summary.</param>
public sealed record ScenarioApplicationResult(
    ScenarioStatus CurrentScenario,
    string Summary);
