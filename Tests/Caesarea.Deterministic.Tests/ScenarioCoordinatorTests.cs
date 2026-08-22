using Caesarea.Contracts;
using DemoScenario.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class ScenarioCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_ResetsAllServicesForEveryScenario()
    {
        var clock = new TestTimeProvider();
        var smartpole = new FakeSmartPoleScenarioClient();
        var energy = new FakeEnergyScenarioClient();
        var commandCenter = new FakeCommandCenterScenarioClient();
        var catalog = new ScenarioCatalog(clock);
        var coordinator = new ScenarioCoordinator(smartpole, energy, commandCenter, catalog, clock);

        foreach (var scenario in catalog.GetAll())
        {
            var correlationId = $"corr-{scenario.Id}";
            var result = await coordinator.ApplyAsync(scenario.Id, correlationId, CancellationToken.None);

            Assert.Equal(scenario.Id, result.CurrentScenario.Id);
            Assert.Equal(scenario.Id, coordinator.GetCurrentScenario().Id);
            Assert.Equal(correlationId, coordinator.GetCurrentScenario().CorrelationId);
        }

        Assert.Equal(catalog.GetAll().Count, smartpole.ResetCalls);
        Assert.Equal(catalog.GetAll().Count, energy.ResetCalls);
        Assert.Equal(catalog.GetAll().Count, commandCenter.ResetCalls);
        Assert.NotNull(commandCenter.LastScenarioContext);
    }
}
