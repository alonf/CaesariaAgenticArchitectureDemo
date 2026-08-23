using EnergyHub.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class EnergyHubServiceTests
{
    [Fact]
    public async Task GetStateReturnsScenarioSynchronizedTwin()
    {
        var clock = new TestTimeProvider();
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(isOn: true, manualOverride: true, controllerHealth: ControllerHealthInfo.Healthy, requiresLighting: false, now: clock.GetUtcNow())
        };

        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);

        await service.ApplyScenarioAsync(new EnergyScenarioSyncRequest(true, null, "seed"), "seed-corr", CancellationToken.None);

        var state = service.GetState(DemoAssets.StreetlightAssetId);
        Assert.True(state.ReportedIsOn);
        Assert.True(state.DesiredIsOn);
        Assert.True(state.ManualOverride);
    }

    [Fact]
    public async Task RestoreScheduledModeUpdatesDesiredBeforeReportedOnSuccess()
    {
        var clock = new TestTimeProvider();
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(isOn: true, manualOverride: true, controllerHealth: ControllerHealthInfo.Healthy, requiresLighting: false, now: clock.GetUtcNow())
        };

        var commandStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandRelease = new TaskCompletionSource<SmartPoleCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.OnSetLampStateAsync = (command, correlationId, cancellationToken) =>
        {
            commandStarted.SetResult();
            return commandRelease.Task;
        };

        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);
        await service.ApplyScenarioAsync(new EnergyScenarioSyncRequest(true, null, "seed"), "seed", CancellationToken.None);

        var restoreTask = service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "restore-corr", CancellationToken.None);
        await commandStarted.Task;

        var interimState = service.GetState(DemoAssets.StreetlightAssetId);
        Assert.False(interimState.DesiredIsOn);
        Assert.True(interimState.ReportedIsOn);
        Assert.Equal(CommandExecutionStatus.Pending, interimState.LastCommand?.Status);

        commandRelease.SetResult(new SmartPoleCommandResult(
            DemoAssets.StreetlightAssetId,
            false,
            CommandExecutionStatus.Succeeded,
            "restore-corr",
            "SmartPole confirmed off.",
            clock.GetUtcNow()));

        var result = await restoreTask;
        var finalState = service.GetState(DemoAssets.StreetlightAssetId);

        Assert.Equal(CommandExecutionStatus.Succeeded, result.Status);
        Assert.False(finalState.ReportedIsOn);
        Assert.False(finalState.DesiredIsOn);
        Assert.Equal("restore-corr", gateway.LastCorrelationId);
    }

    [Fact]
    public async Task RestoreScheduledModeFailureKeepsReportedStateAndPublishesFailureActivity()
    {
        var clock = new TestTimeProvider();
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(isOn: true, manualOverride: false, controllerHealth: ControllerHealthInfo.Faulted, requiresLighting: false, now: clock.GetUtcNow()),
            OnSetLampStateAsync = (command, correlationId, cancellationToken) => Task.FromResult(new SmartPoleCommandResult(
                command.AssetId,
                null,
                CommandExecutionStatus.Failed,
                correlationId,
                "Controller fault kept the light on.",
                clock.GetUtcNow()))
        };

        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);
        await service.ApplyScenarioAsync(new EnergyScenarioSyncRequest(false, null, "seed"), "seed", CancellationToken.None);

        var result = await service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "failure-corr", CancellationToken.None);
        var state = service.GetState(DemoAssets.StreetlightAssetId);
        var activity = service.GetRecentActivity(DemoAssets.StreetlightAssetId, 10);

        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.True(state.ReportedIsOn);
        Assert.False(state.DesiredIsOn);
        Assert.Contains(activity, record => !record.IsSuccess && record.CorrelationId == "failure-corr");
    }

    [Fact]
    public async Task RestoreScheduledModeTimeoutKeepsReportedStateVisible()
    {
        var clock = new TestTimeProvider();
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(isOn: true, manualOverride: false, controllerHealth: ControllerHealthInfo.Healthy, requiresLighting: false, now: clock.GetUtcNow()),
            OnSetLampStateAsync = (command, correlationId, cancellationToken) => Task.FromResult(new SmartPoleCommandResult(
                command.AssetId,
                null,
                CommandExecutionStatus.TimedOut,
                correlationId,
                "SmartPole timed out before acknowledgement.",
                clock.GetUtcNow()))
        };

        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);
        await service.ApplyScenarioAsync(new EnergyScenarioSyncRequest(false, null, "seed"), "seed", CancellationToken.None);

        var result = await service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "timeout-corr", CancellationToken.None);
        var state = service.GetState(DemoAssets.StreetlightAssetId);

        Assert.Equal(CommandExecutionStatus.TimedOut, result.Status);
        Assert.True(state.ReportedIsOn);
        Assert.False(state.DesiredIsOn);
        Assert.Equal(CommandExecutionStatus.TimedOut, state.LastCommand?.Status);
    }

    [Fact]
    public async Task RestoreScheduledModeUsesRequiresLightingRule()
    {
        var clock = new TestTimeProvider();
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(isOn: true, manualOverride: false, controllerHealth: ControllerHealthInfo.Healthy, requiresLighting: true, now: clock.GetUtcNow())
        };

        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);
        await service.ApplyScenarioAsync(new EnergyScenarioSyncRequest(true, null, "seed"), "seed", CancellationToken.None);

        var result = await service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "security-corr", CancellationToken.None);

        Assert.True(result.DesiredIsOn);
        Assert.True(gateway.LastCommand?.DesiredIsOn);
    }

    [Fact]
    public async Task RestoreScheduledModeReturnsFailureWhenAuthoritativeReadFails()
    {
        var clock = new TestTimeProvider();
        var gateway = new FakeSmartPoleGateway
        {
            PhysicalState = CreatePhysicalState(isOn: true, manualOverride: false, controllerHealth: ControllerHealthInfo.Healthy, requiresLighting: false, now: clock.GetUtcNow()),
            OnGetStateAsync = static (assetId, correlationId, cancellationToken) => throw new HttpRequestException("SmartPole unavailable.")
        };

        var service = new EnergyHubService(gateway, clock, NullLogger<EnergyHubService>.Instance);

        var result = await service.RestoreScheduledModeAsync(DemoAssets.StreetlightAssetId, "read-failure-corr", CancellationToken.None);
        var state = service.GetState(DemoAssets.StreetlightAssetId);

        Assert.Equal(CommandExecutionStatus.Failed, result.Status);
        Assert.Contains("could not reach SmartPole", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Null(gateway.LastCommand);
        Assert.Equal(CommandExecutionStatus.Failed, state.LastCommand?.Status);
    }

    private static SmartPolePhysicalState CreatePhysicalState(
        bool isOn,
        bool manualOverride,
        ControllerHealthInfo controllerHealth,
        bool requiresLighting,
        DateTimeOffset now) =>
        new(
            DemoAssets.StreetlightAssetId,
            DemoAssets.NorthPromenadeArea,
            isOn,
            true,
            false,
            manualOverride,
            controllerHealth,
            null,
            now.AddMinutes(-20),
            manualOverride,
            requiresLighting
                ? new OperationalContext(true, true, "Security operation requires lighting.")
                : OperationalContext.None,
            now,
            SmartPoleBehaviorConfiguration.Default);
}
