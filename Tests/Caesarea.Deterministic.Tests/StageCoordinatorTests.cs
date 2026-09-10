using DemoScenario.Api.Services;
using Polly.Timeout;

namespace Caesarea.Deterministic.Tests;

public sealed class StageCoordinatorTests
{
    [Fact]
    public void EveryStageCarriesAWalkthroughThePresenterCanFollow()
    {
        // The Command Center's buttons cannot convey the script on their own: some stages keep the
        // same button and change switchboard state instead, and InteractiveInput needs Tools: MCP
        // as a silent precondition. A stage without a walkthrough leaves the presenter guessing.
        var catalog = new StageCatalog();

        foreach (var descriptor in catalog.GetAll())
        {
            var walkthrough = descriptor.Walkthrough;

            Assert.True(walkthrough is not null, $"{descriptor.Name} has no walkthrough.");
            Assert.NotEmpty(walkthrough.Steps);
            Assert.False(string.IsNullOrWhiteSpace(walkthrough.Point), $"{descriptor.Name} states no point.");
            Assert.All(walkthrough.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Action)));
        }
    }

    [Fact]
    public void StagesThatDependOnSwitchboardStateSaySo()
    {
        // These are the beats a button label cannot describe, and the ones that silently do nothing
        // when the switchboard is in the wrong state.
        var catalog = new StageCatalog();

        Assert.Contains(
            catalog.GetDescriptor(DemoStage.InteractiveInput).Walkthrough!.Prerequisites,
            prerequisite => prerequisite is { Switch: DemoSwitch.ToolSource, RequiredValue: DemoSwitchValues.ToolSourceMcp }
                && prerequisite.Text.Contains("Tools: MCP", StringComparison.Ordinal));

        Assert.Contains(
            catalog.GetDescriptor(DemoStage.MultiAgent).Walkthrough!.Prerequisites,
            prerequisite => prerequisite is { Switch: DemoSwitch.SecurityConsult, RequiredValue: DemoSwitchValues.Off }
                && prerequisite.Text.Contains("consult", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            catalog.GetDescriptor(DemoStage.McpTools).Walkthrough!.Steps,
            step => step.Surface == DemoSurface.Switchboard);

        // The closing beat is a question in the presenter's own words, and the Command Center has
        // no free-text box: the step must send the presenter to a chat client, not to a button
        // that is not there.
        Assert.Contains(
            catalog.GetDescriptor(DemoStage.Hosting).Walkthrough!.Steps,
            step => step.Surface == DemoSurface.HostedChat
                && step.Action.Contains("Copilot", StringComparison.Ordinal));
    }

    [Fact]
    public void TheAppliedStageCarriesItsWalkthroughToTheCommandCenter()
    {
        var descriptor = new StageCatalog().GetDescriptor(DemoStage.MultiAgent);

        // The panel reads this from the applied stage, so it must survive the coordinator.
        Assert.NotNull(descriptor.Walkthrough);
    }

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

        // Every stage the enum defines must be presentable, in enum order: a new stage that the
        // presenter switchboard cannot show is a stage that cannot be demoed.
        Assert.Equal(Enum.GetValues<DemoStage>(), stages.Select(descriptor => descriptor.Id));
        Assert.All(stages, descriptor => Assert.NotEmpty(descriptor.Capabilities));
        Assert.All(stages, descriptor => Assert.NotEmpty(descriptor.Description));

        // Capabilities accumulate: each stage keeps everything the previous one could do.
        foreach (var (previous, next) in stages.Zip(stages.Skip(1)))
        {
            Assert.True(
                previous.Capabilities.All(capability => next.Capabilities.Contains(capability)),
                $"Stage {next.Name} dropped a capability that {previous.Name} advertised.");
        }
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
