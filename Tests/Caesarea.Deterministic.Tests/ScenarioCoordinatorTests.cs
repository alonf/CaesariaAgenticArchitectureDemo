using DemoScenario.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class ScenarioCoordinatorTests
{
    [Fact]
    public async Task ApplyAsyncResetsAllServicesForEveryScenario()
    {
        var clock = new TestTimeProvider();
        var smartpole = new FakeSmartPoleScenarioClient();
        var energy = new FakeEnergyScenarioClient();
        var commandCenter = new FakeCommandCenterScenarioClient();
        var catalog = new ScenarioCatalog(clock);
        var workforce = new FakeWorkforceScenarioClient();
        var coordinator = new ScenarioCoordinator(smartpole, energy, new FakeSecurityScenarioClient(), workforce, commandCenter, catalog, clock, NullLogger<ScenarioCoordinator>.Instance);

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
        // Every scenario resets the workforce domain too, so a consult in one demo cannot be
        // answered from a work order the previous demo left behind.
        Assert.Equal(catalog.GetAll().Count, workforce.ResetCalls);
        Assert.Equal(catalog.GetAll().Count, commandCenter.ResetCalls);
        Assert.NotNull(commandCenter.LastScenarioContext);
    }

    [Fact]
    public async Task ForgottenOverrideStartsWithCustomerReportAndPhoto()
    {
        var clock = new TestTimeProvider();
        var commandCenter = new FakeCommandCenterScenarioClient();
        var coordinator = new ScenarioCoordinator(
            new FakeSmartPoleScenarioClient(),
            new FakeEnergyScenarioClient(),
            new FakeSecurityScenarioClient(),
            new FakeWorkforceScenarioClient(),
            commandCenter,
            new ScenarioCatalog(clock),
            clock,
            NullLogger<ScenarioCoordinator>.Instance);

        await coordinator.ApplyAsync(ScenarioId.ForgottenOverride, "customer-report-corr", CancellationToken.None);

        var report = Assert.IsType<CustomerReportRecord>(commandCenter.LastScenarioContext?.CustomerReport);
        Assert.Equal(DemoAssets.StreetlightAssetId, report.AssetId);
        Assert.Equal("/images/customer-report-l417.png", report.ImageUrl);
        Assert.Contains("ON during the day", report.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsyncMarksRequestedScenarioFailedWhenFinalSynchronizationFails()
    {
        var clock = new TestTimeProvider();
        var smartpole = new FakeSmartPoleScenarioClient();
        var energy = new FakeEnergyScenarioClient();
        var commandCenter = new FakeCommandCenterScenarioClient
        {
            OnApplyScenarioAsync = static (scenarioContext, correlationId, cancellationToken) => throw new HttpRequestException("Command Center unavailable.")
        };
        var catalog = new ScenarioCatalog(clock);
        var workforce = new FakeWorkforceScenarioClient();
        var coordinator = new ScenarioCoordinator(smartpole, energy, new FakeSecurityScenarioClient(), workforce, commandCenter, catalog, clock, NullLogger<ScenarioCoordinator>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => coordinator.ApplyAsync(ScenarioId.ForgottenOverride, "failure-corr", CancellationToken.None));

        var status = coordinator.GetCurrentScenario();
        Assert.Equal(ScenarioId.ForgottenOverride, status.Id);
        Assert.Equal(ScenarioApplicationStatus.Failed, status.ApplicationStatus);
        Assert.False(string.IsNullOrWhiteSpace(status.FailureSummary));
        Assert.Equal("failure-corr", status.CorrelationId);
    }
}
