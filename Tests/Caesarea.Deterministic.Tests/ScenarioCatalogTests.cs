using DemoScenario.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class ScenarioCatalogTests
{
    [Fact]
    public void EveryAdvertisedScenarioHasARecipe()
    {
        var catalog = new ScenarioCatalog(TimeProvider.System);

        foreach (var descriptor in catalog.GetAll())
        {
            var recipe = catalog.GetRecipe(descriptor.Id);
            Assert.Equal(descriptor.Id, recipe.Descriptor.Id);
        }
    }

    [Fact]
    public void ExistingIncidentIsTheControllerFaultAlreadyTracked()
    {
        // The Tool Approval beat asks the same question against Controller Fault and then against
        // Existing Incident, and the only difference the agent should produce is filing nothing the
        // second time. That contrast holds only if the two scenarios share the fault and differ in
        // the incident alone.
        var catalog = new ScenarioCatalog(TimeProvider.System);

        var fault = catalog.GetRecipe(ScenarioId.ControllerFault);
        var tracked = catalog.GetRecipe(ScenarioId.ExistingIncident);

        Assert.Equal(ControllerHealthInfo.Faulted, fault.SmartPoleState.ControllerHealth);
        Assert.Equal(ControllerHealthInfo.Faulted, tracked.SmartPoleState.ControllerHealth);
        Assert.Equal(fault.SmartPoleState.ManualOverride, tracked.SmartPoleState.ManualOverride);
        Assert.Equal(fault.SmartPoleState.IsOn, tracked.SmartPoleState.IsOn);

        Assert.Null(fault.OpenIncident);
        Assert.Null(fault.EnergyState.OpenIncidentId);

        var incident = Assert.IsType<IncidentRecord>(tracked.OpenIncident);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(DemoAssets.StreetlightAssetId, incident.AssetId);
        // The state the agent reads names the same incident the Command Center holds, which is
        // how the lookup is reachable at all.
        Assert.Equal(incident.Id, tracked.EnergyState.OpenIncidentId);
        // And the incident says the work already exists - the fact the agent must find.
        Assert.Contains("dispatch", incident.Description, StringComparison.OrdinalIgnoreCase);
    }
}
