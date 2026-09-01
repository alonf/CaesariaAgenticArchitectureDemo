namespace DemoScenario.Api.Services;

/// <summary>
/// Represents a deterministic recipe that can be applied across SmartPole, Energy Hub, and Command Center boundaries.
/// </summary>
/// <param name="Descriptor">The scenario descriptor exposed to the presenter console.</param>
/// <param name="SmartPoleState">The simulator state to apply first.</param>
/// <param name="EnergyState">The Energy Hub synchronization request.</param>
/// <param name="CustomerReport">The synthetic customer report that triggers the scenario, if any.</param>
/// <param name="OpenIncident">The Command Center incident to seed, if any.</param>
/// <param name="Activity">The Command Center activity to seed.</param>
/// <param name="ApplicationSummary">The projector-friendly completion summary.</param>
/// <param name="SecurityState">
/// The Security Hub synchronization request, for scenarios where another domain is acting. Every
/// scenario clears the Security Hub first, so only a scenario that asserts an operation needs one.
/// </param>
public sealed record ScenarioRecipe(
    ScenarioDescriptor Descriptor,
    SmartPoleScenarioState SmartPoleState,
    EnergyScenarioSyncRequest EnergyState,
    CustomerReportRecord? CustomerReport,
    IncidentRecord? OpenIncident,
    IReadOnlyList<ActivityRecord> Activity,
    string ApplicationSummary,
    SecurityScenarioSyncRequest? SecurityState = null);

/// <summary>
/// Stores the deterministic scenario definitions used by the presenter console and scenario coordinator.
/// </summary>
/// <param name="timeProvider">The clock used to stamp scenario activities and seeded incidents.</param>
public sealed class ScenarioCatalog(TimeProvider timeProvider)
{
    private static readonly ScenarioDescriptor[] Descriptors =
    [
        new(ScenarioId.ForgottenOverride, "Lights On Reported by a Client", "A customer reports L-417 illuminated during daylight; the Command Center confirms that it is on against schedule."),
        new(ScenarioId.SecurityOperation, "Security Operation", "North Promenade requires lighting during a daytime security operation."),
        new(ScenarioId.ControllerFault, "Controller Fault", "The controller is faulted and rejects attempts to return L-417 to schedule."),
        new(ScenarioId.ExistingIncident, "Existing Incident", "An anomaly is already known and an incident exists before the operator acts."),
        new(ScenarioId.NormalOperation, "Normal Operation", "The deterministic daytime baseline with no active issues.")
    ];

    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the full deterministic scenario catalog.
    /// </summary>
    /// <returns>The available scenario descriptors.</returns>
    public IReadOnlyList<ScenarioDescriptor> GetAll() => Descriptors;

    /// <summary>
    /// Gets the fully materialized deterministic recipe for the supplied scenario identifier.
    /// </summary>
    /// <param name="scenarioId">The scenario identifier to resolve.</param>
    /// <returns>The scenario recipe.</returns>
    public ScenarioRecipe GetRecipe(ScenarioId scenarioId)
    {
        var now = _timeProvider.GetUtcNow();
        var descriptor = GetDescriptor(scenarioId);

        return scenarioId switch
        {
            ScenarioId.ForgottenOverride => new ScenarioRecipe(
                descriptor,
                new SmartPoleScenarioState(
                    true,
                    true,
                    false,
                    true,
                    ControllerHealthInfo.Healthy,
                    now.AddMinutes(-30),
                    true,
                    OperationalContext.None,
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(true, null, "Energy Hub confirmed the customer-reported daylight lighting anomaly."),
                new CustomerReportRecord(
                    "REPORT-L417-001",
                    DemoAssets.StreetlightAssetId,
                    DemoAssets.NorthPromenadeArea,
                    "This light on this pole is ON during the day.",
                    "/images/customer-report-l417.png",
                    "Resident mobile report",
                    now.AddMinutes(-2),
                    "customer-report-seed"),
                null,
                [
                    CreateScenarioActivity("Customer report received with a photo of L-417 illuminated during daylight.", now)
                ],
                "Customer report received and Forgotten Override applied deterministically."),
            ScenarioId.SecurityOperation => new ScenarioRecipe(
                descriptor,
                new SmartPoleScenarioState(
                    true,
                    true,
                    false,
                    false,
                    ControllerHealthInfo.Healthy,
                    now.AddHours(-3),
                    false,
                    // The lighting domain is told that lighting is required, and nothing more:
                    // which domain requires it, and why, is disclosed only by that domain's agent.
                    new OperationalContext(true, "An external operational directive requires lighting in this area; the requesting domain is not disclosed to lighting operations."),
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(true, null, "Energy Hub synchronized to the Security Operation scenario."),
                null,
                null,
                [
                    CreateScenarioActivity("Security operations require lighting even though the daylight schedule is off.", now)
                ],
                "Security Operation applied deterministically.",
                // The Security domain's own record of the same fact, carrying the restricted
                // detail only its agent may read.
                new SecurityScenarioSyncRequest(
                    [
                        new SecurityOperationRecord(
                            "SEC-OP-2291",
                            DemoAssets.NorthPromenadeArea,
                            RequiresLighting: true,
                            now.AddHours(-1),
                            now.AddHours(3),
                            Classification: "RESTRICTED",
                            AuthorizedBy: "Superintendent R. Bar-On",
                            UnitCallSign: "NIGHTHAWK-3",
                            Notes: "Perimeter watch along the promenade; lighting required for camera coverage.")
                    ],
                    "Security Hub synchronized to the Security Operation scenario.")),
            ScenarioId.ControllerFault => new ScenarioRecipe(
                descriptor,
                new SmartPoleScenarioState(
                    true,
                    true,
                    false,
                    false,
                    ControllerHealthInfo.Faulted,
                    now.AddDays(-2),
                    false,
                    OperationalContext.None,
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(false, null, "Energy Hub synchronized to the Controller Fault scenario."),
                null,
                null,
                [
                    CreateScenarioActivity("The controller is faulted while L-417 remains on against schedule.", now)
                ],
                "Controller Fault applied deterministically."),
            ScenarioId.ExistingIncident => new ScenarioRecipe(
                descriptor,
                new SmartPoleScenarioState(
                    true,
                    true,
                    false,
                    false,
                    ControllerHealthInfo.Healthy,
                    now.AddHours(-6),
                    false,
                    OperationalContext.None,
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(false, "INC-L417-001", "Energy Hub synchronized to the Existing Incident scenario."),
                null,
                new IncidentRecord(
                    "INC-L417-001",
                    DemoAssets.StreetlightAssetId,
                    DemoAssets.NorthPromenadeArea,
                    "Streetlight on during daylight",
                    "The anomaly was already acknowledged before the current operator session.",
                    IncidentSeverity.Warning,
                    IncidentStatus.Open,
                    now.AddMinutes(-18),
                    "incident-seed"),
                [
                    CreateScenarioActivity("An existing incident is already tracking the daylight anomaly.", now)
                ],
                "Existing Incident applied deterministically."),
            ScenarioId.NormalOperation => new ScenarioRecipe(
                descriptor,
                new SmartPoleScenarioState(
                    false,
                    true,
                    false,
                    false,
                    ControllerHealthInfo.Healthy,
                    null,
                    false,
                    OperationalContext.None,
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(false, null, "Energy Hub synchronized to normal daytime operation."),
                null,
                null,
                [
                    CreateScenarioActivity("The deterministic baseline is active.", now)
                ],
                "Normal Operation restored deterministically."),
            _ => throw new ArgumentOutOfRangeException(nameof(scenarioId), scenarioId, "The requested scenario is not defined for Stage 0.")
        };
    }

    /// <summary>
    /// Gets the presenter-facing descriptor for the supplied scenario identifier.
    /// </summary>
    /// <param name="scenarioId">The scenario identifier to resolve.</param>
    /// <returns>The matching descriptor.</returns>
    public ScenarioDescriptor GetDescriptor(ScenarioId scenarioId) =>
        Descriptors.FirstOrDefault(candidate => candidate.Id == scenarioId)
        ?? throw new ArgumentOutOfRangeException(nameof(scenarioId), scenarioId, "The requested scenario is not defined for Stage 0.");

    private static ActivityRecord CreateScenarioActivity(string message, DateTimeOffset occurredAt) =>
        new(
            Guid.NewGuid().ToString("N"),
            DemoAssets.StreetlightAssetId,
            "scenario-seed",
            ActivitySource.DemoScenario,
            ActivityKind.Scenario,
            message,
            occurredAt,
            true,
            null);
}
