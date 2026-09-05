using SmartPole.Simulator.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class SmartPoleSimulatorServiceTests
{
    [Fact]
    public void DefaultBootStateIsTheQuietBaseline()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);

        Assert.False(state.IsOn);
        Assert.False(state.ManualOverride);
        Assert.False(state.HasRecentMaintenance);
    }

    [Fact]
    public void ForgottenOverrideBootMatchesTheScenarioSituation()
    {
        // The cloud habitat has no switchboard - its demo surface is deliberately off - so the pole
        // boots into the situation the lecture investigates. The state must be the ForgottenOverride
        // scenario's, field for field: the hosted agent's triage reads HasRecentMaintenance and the
        // override to classify it, and its OneDrive work order describes exactly this.
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(
            clock, NullLogger<SmartPoleSimulatorService>.Instance, startWithForgottenOverride: true);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);

        Assert.True(state.IsOn);
        Assert.True(state.IsDaylight);
        Assert.False(state.ExpectedScheduledState);
        Assert.True(state.ManualOverride);
        Assert.True(state.HasRecentMaintenance);
        Assert.Equal(clock.GetUtcNow().AddMinutes(-30), state.LastMaintenanceTime);
        Assert.Equal(ControllerHealthStatus.Healthy, state.ControllerHealth.Status);
    }

    [Fact]
    public void ResetReturnsToTheConfiguredBaselineNotTheQuietOne()
    {
        // A container restart and a reset must land in the same place, or the cloud city would
        // change personality the first time its pod recycles.
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(
            clock, NullLogger<SmartPoleSimulatorService>.Instance, startWithForgottenOverride: true);

        simulator.ApplyScenario(
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
            "seed-quiet");

        var state = simulator.Reset("reset-corr");

        Assert.True(state.IsOn);
        Assert.True(state.ManualOverride);
        Assert.True(state.HasRecentMaintenance);
    }

    [Fact]
    public async Task SetLampStateAsyncSucceedsAndClearsManualOverride()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);

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
    public async Task SetLampStateAsyncFailureKeepsPhysicalStateUnchanged()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);

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
    public async Task SetLampStateAsyncTimeoutKeepsPhysicalStateUnchanged()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);

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

    [Fact]
    public async Task SetLampStateAsyncCancellationMarksLastCommandAsFailed()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);

        simulator.UpdateConfiguration(new SmartPoleBehaviorConfiguration(250, false, false), "config");

        using var cancellationTokenSource = new CancellationTokenSource();
        var commandTask = simulator.SetLampStateAsync(new SetLampStateCommand(DemoAssets.StreetlightAssetId, true), "cancel-corr", cancellationTokenSource.Token);
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => commandTask);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);
        Assert.Equal(CommandExecutionStatus.Failed, state.LastCommand?.Status);
    }
}
