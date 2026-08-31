using EnergyHub.Api.Services;
using SmartPole.Simulator.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class SupersededCommandTests
{
    [Fact]
    public async Task ResetDuringInFlightSimulatorCommandSupersedesTheCommand()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);
        simulator.UpdateConfiguration(new SmartPoleBehaviorConfiguration(1000, false, false), "config-corr");

        var commandTask = simulator.SetLampStateAsync(
            new SetLampStateCommand(DemoAssets.StreetlightAssetId, true),
            "command-corr",
            CancellationToken.None);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        simulator.Reset("reset-corr");

        var result = await commandTask;

        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.Contains("superseded", result.Summary, StringComparison.OrdinalIgnoreCase);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);
        Assert.False(state.IsOn);
        Assert.Null(state.LastCommand);
    }

    [Fact]
    public async Task ScenarioAppliedDuringRestoreSupersedesTheRestore()
    {
        var clock = new TestTimeProvider();
        var commandStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCommand = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(clock.GetUtcNow()),
            OnSetLampStateAsync = async (command, correlationId, cancellationToken) =>
            {
                commandStarted.TrySetResult();
                await releaseCommand.Task.WaitAsync(cancellationToken);
                return new SmartPoleCommandResult(
                    command.AssetId,
                    command.DesiredIsOn,
                    CommandExecutionStatus.Succeeded,
                    correlationId,
                    "Fake SmartPole confirmed the command.",
                    clock.GetUtcNow());
            }
        };
        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);

        var restoreTask = service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "restore-corr", CancellationToken.None);
        await commandStarted.Task;
        await service.ApplyScenarioAsync(
            new EnergyScenarioSyncRequest(true, null, "Scenario applied mid-restore."),
            "scenario-corr",
            CancellationToken.None);
        releaseCommand.SetResult();

        var result = await restoreTask;

        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.Contains("superseded", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.True(service.GetState(DemoAssets.StreetlightAssetId).DesiredIsOn);
    }

    [Fact]
    public async Task OlderRestoreFinishingLastIsSupersededByTheNewerRestore()
    {
        var clock = new TestTimeProvider();
        var olderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOlder = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(clock.GetUtcNow()),
            OnSetLampStateAsync = async (command, correlationId, cancellationToken) =>
            {
                if (correlationId == "restore-older")
                {
                    olderStarted.TrySetResult();
                    await releaseOlder.Task.WaitAsync(cancellationToken);
                }

                return new SmartPoleCommandResult(
                    command.AssetId,
                    command.DesiredIsOn,
                    CommandExecutionStatus.Succeeded,
                    correlationId,
                    "Fake SmartPole confirmed the command.",
                    clock.GetUtcNow());
            }
        };
        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);

        var olderRestore = service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "restore-older", CancellationToken.None);
        await olderStarted.Task;
        var newerResult = await service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "restore-newer", CancellationToken.None);
        releaseOlder.SetResult();
        var olderResult = await olderRestore;

        // Accepting a command claims a fresh revision, so the older restore can never overwrite the
        // newer one even though it completed last.
        Assert.Equal(CommandExecutionStatus.Succeeded, newerResult.Status);
        Assert.Equal(CommandExecutionStatus.Failed, olderResult.Status);
        Assert.Contains("superseded", olderResult.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("restore-newer", service.GetState(DemoAssets.StreetlightAssetId).LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task OlderSimulatorCommandIsSupersededByTheNewerCommand()
    {
        var clock = new TestTimeProvider();
        var simulator = new SmartPoleSimulatorService(clock, NullLogger<SmartPoleSimulatorService>.Instance);
        simulator.UpdateConfiguration(new SmartPoleBehaviorConfiguration(300, false, false), "config-corr");

        // Both commands are accepted (acceptance is synchronous before the execution delay); the
        // one accepted last claims the newer revision and is the only one allowed to commit.
        var olderCommand = simulator.SetLampStateAsync(
            new SetLampStateCommand(DemoAssets.StreetlightAssetId, true),
            "command-older",
            CancellationToken.None);
        var newerCommand = simulator.SetLampStateAsync(
            new SetLampStateCommand(DemoAssets.StreetlightAssetId, false),
            "command-newer",
            CancellationToken.None);

        var olderResult = await olderCommand;
        var newerResult = await newerCommand;

        Assert.Equal(CommandExecutionStatus.Failed, olderResult.Status);
        Assert.Contains("superseded", olderResult.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CommandExecutionStatus.Succeeded, newerResult.Status);

        var state = simulator.GetState(DemoAssets.StreetlightAssetId);
        Assert.False(state.IsOn);
        Assert.Equal("command-newer", state.LastCommand?.CorrelationId);
    }

    private static SmartPolePhysicalState CreatePhysicalState(DateTimeOffset now) =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            true,
            true,
            false,
            true,
            ControllerHealthInfo.Healthy,
            null,
            now.AddMinutes(-20),
            true,
            OperationalContext.None,
            now,
            SmartPoleBehaviorConfiguration.Default);
}
