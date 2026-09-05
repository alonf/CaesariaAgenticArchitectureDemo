using EnergyHub.Api.Services;
using SmartPole.Simulator.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Pins the startup twin hydration. The hub's constructor seeds a quiet baseline twin so it can
/// answer before its device layer does; in the deployed habitat nothing ever reshapes it - the
/// demo surface is off - so hydration is the only path from "constructor's guess" to "what the
/// device actually reports". Found live: the deployed SmartPole booted into the forgotten-override
/// state while the hub kept reporting quiet, and the hosted agent faithfully relayed the wrong
/// answer.
/// </summary>
public sealed class EnergyHubTwinHydrationTests
{
    [Fact]
    public async Task HydrationProjectsTheDeviceStateOntoTheUntouchedTwin()
    {
        var clock = new TestTimeProvider();
        // The exact state the deployed simulator boots into, taken from the same factory the
        // cloud container uses - so this test breaks if the two ends ever drift apart.
        var deviceState = SmartPoleSimulatorService.CreateForgottenOverrideState(clock.GetUtcNow());
        var gateway = new FakeSmartPoleGateway { PhysicalState = deviceState };
        var hub = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);

        await hub.HydrateFromDeviceAsync("hydrate-corr", TestContext.Current.CancellationToken);

        var twin = hub.GetState(DemoAssets.StreetlightAssetId);
        Assert.True(twin.ReportedIsOn);
        Assert.True(twin.ManualOverride);
        Assert.True(twin.HasRecentMaintenance);
        Assert.True(twin.IsAnomalous);
    }

    [Fact]
    public async Task HydrationYieldsToATwinThatWasAlreadyShaped()
    {
        // A scenario applied while the hub was still reading its device must win: hydration is a
        // startup convenience, never an overwrite.
        var clock = new TestTimeProvider();
        var quietDevice = SmartPoleSimulatorService.CreateBaselineState(clock.GetUtcNow());
        var gateway = new FakeSmartPoleGateway { PhysicalState = quietDevice };
        var hub = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);

        var overrideDevice = SmartPoleSimulatorService.CreateForgottenOverrideState(clock.GetUtcNow());
        gateway.OnGetStateAsync = (_, _, _) => Task.FromResult(overrideDevice);
        await hub.ApplyScenarioAsync(
            new EnergyScenarioSyncRequest(true, null, "Scenario applied before hydration ran."),
            "scenario-corr",
            TestContext.Current.CancellationToken);

        gateway.OnGetStateAsync = (_, _, _) => Task.FromResult(quietDevice);
        await hub.HydrateFromDeviceAsync("late-hydrate-corr", TestContext.Current.CancellationToken);

        var twin = hub.GetState(DemoAssets.StreetlightAssetId);
        Assert.True(twin.ReportedIsOn);
        Assert.True(twin.ManualOverride);
    }
}
