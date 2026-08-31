using DemoScenario.Api.Services;
using Polly.Timeout;

namespace Caesarea.Deterministic.Tests;

public sealed class StageCoordinatorTests
{
    [Fact]
    public async Task CoordinatorReadsAuthoritativeDeterministicStage()
    {
        var coordinator = CreateCoordinator(new TestTimeProvider(), out _, out _);

        var current = await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None);

        Assert.Equal(DemoStage.Deterministic, current.Id);
    }

    [Fact]
    public async Task ApplyAsyncPropagatesStageToCommandCenterAndUpdatesCurrent()
    {
        var coordinator = CreateCoordinator(new TestTimeProvider(), out var commandCenterStageClient, out var operationsAgentStageClient);

        var result = await coordinator.ApplyAsync(DemoStage.InvestigationAgent, "stage-corr", CancellationToken.None);

        Assert.Equal(DemoStage.InvestigationAgent, result.CurrentStage.Id);
        Assert.Equal(DemoStage.InvestigationAgent, (await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None)).Id);
        Assert.Equal("stage-corr", (await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None)).CorrelationId);
        Assert.Equal(1, commandCenterStageClient.ApplyCalls);
        Assert.Equal(DemoStage.InvestigationAgent, commandCenterStageClient.LastStage?.Id);
        Assert.Equal(1, operationsAgentStageClient.ApplyCalls);
        Assert.Equal(DemoStage.InvestigationAgent, operationsAgentStageClient.LastStage?.Id);
        Assert.NotEmpty((await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None)).Capabilities);
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
            new FakeOperationsAgentStageClient(),
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            coordinator.ApplyAsync(DemoStage.InvestigationAgent, "failure-corr", CancellationToken.None));

        Assert.Equal(DemoStage.Deterministic, (await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None)).Id);
    }

    [Fact]
    public void StageCatalogExposesAllStagesWithCapabilities()
    {
        var catalog = new StageCatalog();

        var stages = catalog.GetAll();

        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.Deterministic);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.InvestigationAgent);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.Session);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.Knowledge);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.Memory);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.Skills);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.McpTools);
        Assert.Contains(stages, descriptor => descriptor.Id == DemoStage.InteractiveInput);
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

                return stage;
            }
        };
        using var coordinator = new StageCoordinator(
            client,
            new FakeOperationsAgentStageClient(),
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        var first = coordinator.ApplyAsync(DemoStage.InvestigationAgent, "first-corr", CancellationToken.None);
        await firstStarted.Task;
        var second = coordinator.ApplyAsync(DemoStage.Deterministic, "second-corr", CancellationToken.None);

        Assert.False(second.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(DemoStage.Deterministic, (await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None)).Id);
        Assert.Equal("second-corr", (await coordinator.GetCurrentStageAsync("read-corr", CancellationToken.None)).CorrelationId);
    }

    [Fact]
    public async Task ApplyAsyncToleratesOperationsAgentPropagationFailure()
    {
        var commandCenterStageClient = new FakeCommandCenterStageClient();
        var operationsAgentStageClient = new FakeOperationsAgentStageClient
        {
            OnApplyStageAsync = static (_, _, _) => throw new HttpRequestException("Operations Agent unavailable.")
        };
        using var coordinator = new StageCoordinator(
            commandCenterStageClient,
            operationsAgentStageClient,
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        var result = await coordinator.ApplyAsync(DemoStage.InvestigationAgent, "agent-fail-corr", CancellationToken.None);

        Assert.Equal(DemoStage.InvestigationAgent, result.CurrentStage.Id);
        Assert.Contains("Warning", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsyncToleratesOperationsAgentResilienceTimeout()
    {
        // The resilience pipeline surfaces its attempt timeout as TimeoutRejectedException, not
        // HttpRequestException - it must degrade to the same visible warning.
        var operationsAgentStageClient = new FakeOperationsAgentStageClient
        {
            OnApplyStageAsync = static (_, _, _) => throw new TimeoutRejectedException("The resilience attempt timed out.")
        };
        using var coordinator = new StageCoordinator(
            new FakeCommandCenterStageClient(),
            operationsAgentStageClient,
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        var result = await coordinator.ApplyAsync(DemoStage.InvestigationAgent, "agent-timeout-corr", CancellationToken.None);

        Assert.Equal(DemoStage.InvestigationAgent, result.CurrentStage.Id);
        Assert.Contains("Warning", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsyncToleratesOperationsAgentInternalCancellation()
    {
        // A cancellation raised inside the pipeline while the caller's token is NOT cancelled is a
        // dependency failure, not a caller intent - it degrades to the warning.
        var operationsAgentStageClient = new FakeOperationsAgentStageClient
        {
            OnApplyStageAsync = static (_, _, _) => throw new TaskCanceledException("The pipeline cancelled internally.")
        };
        using var coordinator = new StageCoordinator(
            new FakeCommandCenterStageClient(),
            operationsAgentStageClient,
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        var result = await coordinator.ApplyAsync(DemoStage.InvestigationAgent, "agent-cancel-corr", CancellationToken.None);

        Assert.Equal(DemoStage.InvestigationAgent, result.CurrentStage.Id);
        Assert.Contains("Warning", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsyncPropagatesCallerCancellationFromAgentPush()
    {
        using var callerCancellation = new CancellationTokenSource();
        var operationsAgentStageClient = new FakeOperationsAgentStageClient
        {
            OnApplyStageAsync = (_, _, _) =>
            {
                callerCancellation.Cancel();
                throw new OperationCanceledException(callerCancellation.Token);
            }
        };
        using var coordinator = new StageCoordinator(
            new FakeCommandCenterStageClient(),
            operationsAgentStageClient,
            new StageCatalog(),
            new TestTimeProvider(),
            NullLogger<StageCoordinator>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.ApplyAsync(DemoStage.InvestigationAgent, "caller-cancel-corr", callerCancellation.Token));
    }

    private static StageCoordinator CreateCoordinator(
        TestTimeProvider clock,
        out FakeCommandCenterStageClient commandCenterStageClient,
        out FakeOperationsAgentStageClient operationsAgentStageClient)
    {
        commandCenterStageClient = new FakeCommandCenterStageClient();
        operationsAgentStageClient = new FakeOperationsAgentStageClient();
        return new StageCoordinator(
            commandCenterStageClient,
            operationsAgentStageClient,
            new StageCatalog(),
            clock,
            NullLogger<StageCoordinator>.Instance);
    }
}
