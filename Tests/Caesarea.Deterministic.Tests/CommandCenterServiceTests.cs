using CommandCenter.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class CommandCenterServiceTests
{
    [Fact]
    public void GetIncidentReturnsSeededIncident()
    {
        var clock = new TestTimeProvider();
        var service = CreateService(clock);
        var incident = new IncidentRecord(
            "INC-L417-001",
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            "Daylight anomaly",
            "An operator already acknowledged the anomaly.",
            IncidentSeverity.Warning,
            IncidentStatus.Open,
            clock.GetUtcNow(),
            "inc-corr");

        service.ApplyScenarioContext(
            new CommandCenterScenarioContext(
                new ScenarioStatus(ScenarioId.ExistingIncident, "Existing Incident", "Existing incident seeded.", clock.GetUtcNow(), "scenario-corr"),
                null,
                incident,
                []),
            "scenario-corr");

        var resolved = service.GetIncident("INC-L417-001");

        Assert.NotNull(resolved);
        Assert.Equal("Daylight anomaly", resolved.Title);
    }

    [Fact]
    public async Task GetSnapshotAsyncMergesLocalAndEnergyActivity()
    {
        var clock = new TestTimeProvider();
        var service = CreateService(clock);

        service.ApplyScenarioContext(
            new CommandCenterScenarioContext(
                new ScenarioStatus(ScenarioId.NormalOperation, "Normal Operation", "Baseline", clock.GetUtcNow(), "scenario-corr"),
                null,
                null,
                [
                    new ActivityRecord(
                        "local-1",
                        DemoAssets.StreetlightAssetId,
                        "scenario-corr",
                        ActivitySource.DemoScenario,
                        ActivityKind.Scenario,
                        "Scenario applied.",
                        clock.GetUtcNow(),
                        true,
                        null)
                ]),
            "scenario-corr");

        var snapshot = await service.GetSnapshotAsync(DemoAssets.StreetlightAssetId, 10, "query-corr", CancellationToken.None);

        Assert.Equal(3, snapshot.RecentActivity.Count);
        Assert.Contains(snapshot.RecentActivity, activity => activity.Source == ActivitySource.DemoScenario);
        Assert.Contains(snapshot.RecentActivity, activity => activity.Source == ActivitySource.EnergyHub);
        Assert.Contains(snapshot.RecentActivity, activity => activity.Source == ActivitySource.CommandCenter);
        Assert.Equal("Normal Operation", snapshot.CurrentScenario.Name);
    }

    [Fact]
    public async Task RestoreScheduledModeReturnsFailureWhenEnergyHubGatewayThrows()
    {
        var clock = new TestTimeProvider();
        var gateway = CreateGateway(clock);
        var service = CreateService(clock, gateway);
        gateway.OnRestoreScheduledModeAsync = static (assetId, correlationId, cancellationToken) => throw new HttpRequestException("Energy Hub unavailable.");

        var result = await service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "gateway-failure-corr", CancellationToken.None);
        var activity = await service.GetRecentActivityAsync(DemoAssets.StreetlightAssetId, 10, "gateway-failure-corr", CancellationToken.None);

        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.Null(result.DesiredIsOn);
        Assert.Null(result.ReportedIsOn);
        Assert.Contains(activity, record => !record.IsSuccess && record.CorrelationId == "gateway-failure-corr");
    }

    [Fact]
    public async Task ApplyStagePropagatesToSnapshotAndSurvivesReset()
    {
        var clock = new TestTimeProvider();
        var service = CreateService(clock);
        var stage = new DemoStageStatus(
            DemoStage.InvestigationAgent,
            "Investigation Agent",
            "The read-only Operations Agent can investigate the current anomaly.",
            ["Deterministic scenarios", "Read-only Operations Agent investigation"],
            clock.GetUtcNow(),
            "stage-corr");

        var applied = service.ApplyStage(stage, "stage-corr");

        Assert.Equal(DemoStage.InvestigationAgent, applied.Id);
        Assert.Equal(DemoStage.InvestigationAgent, service.GetCurrentStage().Id);

        var snapshot = await service.GetSnapshotAsync(DemoAssets.StreetlightAssetId, 5, "snapshot-corr", CancellationToken.None);
        Assert.Equal(DemoStage.InvestigationAgent, snapshot.CurrentStage.Id);

        service.Reset("reset-corr");

        Assert.Equal(DemoStage.InvestigationAgent, service.GetCurrentStage().Id);
    }

    [Fact]
    public void GetCurrentStageDefaultsToDeterministic()
    {
        var service = CreateService(new TestTimeProvider());

        Assert.Equal(DemoStage.Deterministic, service.GetCurrentStage().Id);
    }

    private static CommandCenterService CreateService(TestTimeProvider clock, FakeEnergyHubGateway? gateway = null) =>
        new(
            gateway ?? CreateGateway(clock),
            new IncidentModule(),
            new ActivityTimelineModule(),
            new CustomerReportModule(),
            new ScenarioContextModule(clock),
            new SpatialContextModule(),
            new StageContextModule(clock),
            clock,
            NullLogger<CommandCenterService>.Instance);

    private static FakeEnergyHubGateway CreateGateway(TestTimeProvider clock)
    {
        var state = new EnergyOperationalTwin(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            false,
            false,
            true,
            false,
            false,
            ControllerHealthInfo.Healthy,
            null,
            null,
            false,
            null,
            clock.GetUtcNow(),
            OperationalContext.None);

        var energyGateway = new FakeEnergyHubGateway
        {
            State = state,
            Activity =
            [
                new ActivityRecord(
                    "energy-1",
                    DemoAssets.StreetlightAssetId,
                    "energy-corr",
                    ActivitySource.EnergyHub,
                    ActivityKind.Observation,
                    "Energy Hub published a stable snapshot.",
                    clock.GetUtcNow().AddSeconds(-5),
                    true,
                    null)
            ],
            RestoreResult = new RestoreScheduledModeResult(
                DemoAssets.StreetlightAssetId,
                false,
                false,
                CommandExecutionStatus.Succeeded,
                "restore-corr",
                "Scheduled mode restored.",
                clock.GetUtcNow())
        };

        return energyGateway;
    }
}
