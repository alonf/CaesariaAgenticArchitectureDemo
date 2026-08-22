using Caesarea.Contracts;
using CommandCenter.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class CommandCenterServiceTests
{
    [Fact]
    public void GetIncident_ReturnsSeededIncident()
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
                incident,
                []),
            "scenario-corr");

        var resolved = service.GetIncident("INC-L417-001");

        Assert.NotNull(resolved);
        Assert.Equal("Daylight anomaly", resolved!.Title);
    }

    [Fact]
    public async Task GetSnapshotAsync_MergesLocalAndEnergyActivity()
    {
        var clock = new TestTimeProvider();
        var service = CreateService(clock);

        service.ApplyScenarioContext(
            new CommandCenterScenarioContext(
                new ScenarioStatus(ScenarioId.NormalOperation, "Normal Operation", "Baseline", clock.GetUtcNow(), "scenario-corr"),
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

    private static CommandCenterService CreateService(TestTimeProvider clock)
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

        return new CommandCenterService(
            energyGateway,
            new IncidentModule(),
            new ActivityTimelineModule(),
            new ScenarioContextModule(clock),
            new SpatialContextModule(),
            clock);
    }
}
