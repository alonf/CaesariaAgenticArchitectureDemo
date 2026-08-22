using Caesarea.Contracts;
using SmartPole.Simulator.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class SmartPoleSimulatorServiceTests
{
    [Fact]
    public async Task SetLampStateAsync_SucceedsAndClearsManualOverride()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock);

        simulator.ApplyScenario(
            new SmartPoleScenarioState(
                true,
                true,
                false,
                true,
                ControllerHealthInfo.Healthy,
                clock.GetUtcNow().AddMinutes(-15),
                true,
                OperationalContext.None,
                SmartPoleBehaviorConfiguration.Default),
            "seed");

        var result = await simulator.SetLampStateAsync(new SetLampStateCommand(DemoAssets.StreetlightAssetId, false), "corr-1", CancellationToken.None);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);
        Assert.Equal(CommandExecutionStatus.Succeeded, result.Status);
        Assert.False(state.IsOn);
        Assert.False(state.ManualOverride);
        Assert.Equal("corr-1", state.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task SetLampStateAsync_FailureKeepsPhysicalStateUnchanged()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock);

        simulator.ApplyScenario(
            new SmartPoleScenarioState(
                true,
                true,
                false,
                false,
                ControllerHealthInfo.Faulted,
                null,
                false,
                OperationalContext.None,
                SmartPoleBehaviorConfiguration.Default),
            "seed");

        var result = await simulator.SetLampStateAsync(new SetLampStateCommand(DemoAssets.StreetlightAssetId, false), "corr-2", CancellationToken.None);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);
        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.True(state.IsOn);
        Assert.Equal(CommandExecutionStatus.Failed, state.LastCommand?.Status);
    }

    [Fact]
    public async Task SetLampStateAsync_TimeoutKeepsPhysicalStateUnchanged()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock);

        simulator.ApplyScenario(
            new SmartPoleScenarioState(
                true,
                true,
                false,
                false,
                ControllerHealthInfo.Healthy,
                null,
                false,
                OperationalContext.None,
                new SmartPoleBehaviorConfiguration(0, true, false)),
            "seed");

        var result = await simulator.SetLampStateAsync(new SetLampStateCommand(DemoAssets.StreetlightAssetId, false), "corr-3", CancellationToken.None);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);
        Assert.Equal(CommandExecutionStatus.TimedOut, result.Status);
        Assert.True(state.IsOn);
        Assert.Equal(CommandExecutionStatus.TimedOut, state.LastCommand?.Status);
    }
}
