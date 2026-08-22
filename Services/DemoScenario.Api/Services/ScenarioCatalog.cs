using Caesarea.Contracts;

namespace DemoScenario.Api.Services;

public sealed record ScenarioRecipe(
    ScenarioDescriptor Descriptor,
    SmartPoleScenarioState SmartPoleState,
    EnergyScenarioSyncRequest EnergyState,
    IncidentRecord? OpenIncident,
    IReadOnlyList<ActivityRecord> Activity,
    string ApplicationSummary);

public sealed class ScenarioCatalog(TimeProvider timeProvider)
{
    private static readonly ScenarioDescriptor[] Descriptors =
    [
        new(ScenarioId.ForgottenOverride, "Forgotten Override", "Daylight is active, schedule expects off, and a recent maintenance override left L-417 on."),
        new(ScenarioId.SecurityOperation, "Security Operation", "North Promenade requires lighting during a daytime security operation."),
        new(ScenarioId.ControllerFault, "Controller Fault", "The controller is faulted and rejects attempts to return L-417 to schedule."),
        new(ScenarioId.ExistingIncident, "Existing Incident", "An anomaly is already known and an incident exists before the operator acts."),
        new(ScenarioId.NormalOperation, "Normal Operation", "The deterministic daytime baseline with no active issues."),
        new(ScenarioId.NightOperation, "Night Operation", "The nightly lighting schedule is active and L-417 is operating normally.")
    ];

    public IReadOnlyList<ScenarioDescriptor> GetAll() => Descriptors;

    public ScenarioRecipe GetRecipe(ScenarioId scenarioId)
    {
        var now = timeProvider.GetUtcNow();
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
                new EnergyScenarioSyncRequest(true, null, "Energy Hub synchronized to the Forgotten Override scenario."),
                null,
                [
                    CreateScenarioActivity("Recent maintenance left manual override enabled during daylight.", now)
                ],
                "Forgotten Override applied deterministically."),
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
                    new OperationalContext(true, true, "Security operation requires lighting in North Promenade."),
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(true, null, "Energy Hub synchronized to the Security Operation scenario."),
                null,
                [
                    CreateScenarioActivity("Security operations require lighting even though the daylight schedule is off.", now)
                ],
                "Security Operation applied deterministically."),
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
                [
                    CreateScenarioActivity("The deterministic baseline is active.", now)
                ],
                "Normal Operation restored deterministically."),
            ScenarioId.NightOperation => new ScenarioRecipe(
                descriptor,
                new SmartPoleScenarioState(
                    true,
                    false,
                    true,
                    false,
                    ControllerHealthInfo.Healthy,
                    null,
                    false,
                    OperationalContext.None,
                    SmartPoleBehaviorConfiguration.Default),
                new EnergyScenarioSyncRequest(true, null, "Energy Hub synchronized to normal night operation."),
                null,
                [
                    CreateScenarioActivity("Night schedule is active and L-417 is operating normally.", now)
                ],
                "Night Operation applied deterministically."),
            _ => throw new ArgumentOutOfRangeException(nameof(scenarioId), scenarioId, "The requested scenario is not defined for Stage 0.")
        };
    }

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
