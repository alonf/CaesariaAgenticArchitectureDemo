using DemoScenario.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class StageCoordinatorTests
{
    [Fact]
    public void CoordinatorStartsInDeterministicStage()
    {
        var coordinator = CreateCoordinator(new TestTimeProvider(), out _);

        var current = coordinator.GetCurrentStage();

        Assert.Equal(DemoStage.Deterministic, current.Id);
    }

    [Fact]
    public async Task ApplyAsyncPropagatesStageToCommandCenterAndUpdatesCurrent()
    {
        var coordinator = CreateCoordinator(new TestTimeProvider(), out var commandCenterStageClient);

        var result = await coordinator.ApplyAsync(DemoStage.InvestigationAgent, "stage-corr", CancellationToken.None);

        Assert.Equal(DemoStage.InvestigationAgent, result.CurrentStage.Id);
        Assert.Equal(DemoStage.InvestigationAgent, coordinator.GetCurrentStage().Id);
        Assert.Equal("stage-corr", coordinator.GetCurrentStage().CorrelationId);
        Assert.Equal(1, commandCenterStageClient.ApplyCalls);
        Assert.Equal(DemoStage.InvestigationAgent, commandCenterStageClient.LastStage?.Id);
        Assert.NotEmpty(coordinator.GetCurrentStage().Capabilities);
    }

    [Fact]
    public async Task ApplyAsyncDoesNotUpdateCurrentStageWhenPropagationFails()
    {
        var commandCenterStageClient = new FakeCommandCenterStageClient
        {
            OnApplyStageAsync = static (_, _, _) => throw new HttpRequestException("Command Center unavailable.")
        };
        var coordinator = new StageCoordinator(
            commandCenterStageClient,
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            coordinator.ApplyAsync(DemoStage.InvestigationAgent, "failure-corr", CancellationToken.None));

        Assert.Equal(DemoStage.Deterministic, coordinator.GetCurrentStage().Id);
    }

    [Fact]
    public void StageCatalogExposesBothStagesWithCapabilities()
    {
        var catalog = new StageCatalog();

        var stages = catalog.GetAll();

        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.Deterministic);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.InvestigationAgent);
        Assert.All(stages, descriptor => Assert.NotEmpty(descriptor.Capabilities));
    }

    [Fact]
    public async Task ApplyAsyncSerializesConcurrentStageChanges()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeCommandCenterStageClient
        {
            OnApplyStageAsync = async (stage, _, cancellationToken) =>
            {
                if (stage.Id == DemoStage.InvestigationAgent)
                {
                    firstStarted.SetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }
            }
        };
        using var coordinator = new StageCoordinator(
            client,
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        var first = coordinator.ApplyAsync(DemoStage.InvestigationAgent, "first-corr", CancellationToken.None);
        await firstStarted.Task;
        var second = coordinator.ApplyAsync(DemoStage.Deterministic, "second-corr", CancellationToken.None);

        Assert.False(second.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(DemoStage.Deterministic, coordinator.GetCurrentStage().Id);
        Assert.Equal("second-corr", coordinator.GetCurrentStage().CorrelationId);
    }

    private static StageCoordinator CreateCoordinator(TestTimeProvider clock, out FakeCommandCenterStageClient commandCenterStageClient)
    {
        commandCenterStageClient = new FakeCommandCenterStageClient();
        return new StageCoordinator(commandCenterStageClient, new StageCatalog(), clock, NullLogger<StageCoordinator>.Instance);
    }
}
