using System.Text.Json;
using Azure.Core;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class DemoStageSynchronizerTests
{
    [Fact]
    public async Task SuccessfulAttemptAppliesTheAuthoritativeStage()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);
        var reader = new FakeStageReader
        {
            OnGetCurrentStageAsync = static (_, _) => Task.FromResult(CreateStatus(DemoStage.Session))
        };
        var synchronizer = CreateSynchronizer(reader, gate);

        var synchronized = await synchronizer.TrySynchronizeAsync(CancellationToken.None);

        Assert.True(synchronized);
        Assert.Equal(DemoStage.Session, gate.GetCurrent().Id);
        Assert.True(gate.IsAgentEnabled);
    }

    [Fact]
    public async Task MalformedResponseIsSwallowedAndReported()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);
        var reader = new FakeStageReader
        {
            OnGetCurrentStageAsync = static (_, _) => throw new JsonException("Malformed stage payload.")
        };
        var synchronizer = CreateSynchronizer(reader, gate);

        var synchronized = await synchronizer.TrySynchronizeAsync(CancellationToken.None);

        Assert.False(synchronized);
        Assert.Equal(DemoStage.Deterministic, gate.GetCurrent().Id);
    }

    [Fact]
    public async Task ResilienceTimeoutIsSwallowedAndReported()
    {
        var gate = new DemoStageGate(DemoStage.Deterministic);
        var reader = new FakeStageReader
        {
            // The resilience pipeline surfaces attempt timeouts as cancellation without the host
            // shutdown token being triggered; the synchronizer must survive that too.
            OnGetCurrentStageAsync = static (_, _) => throw new TaskCanceledException("Attempt timed out.")
        };
        var synchronizer = CreateSynchronizer(reader, gate);

        var synchronized = await synchronizer.TrySynchronizeAsync(CancellationToken.None);

        Assert.False(synchronized);
    }

    [Fact]
    public async Task HostShutdownCancellationPropagates()
    {
        using var shutdownSource = new CancellationTokenSource();
        var reader = new FakeStageReader
        {
            OnGetCurrentStageAsync = (_, cancellationToken) =>
                Task.FromException<DemoStageStatus>(new OperationCanceledException(cancellationToken))
        };
        var synchronizer = CreateSynchronizer(reader, new DemoStageGate(DemoStage.Deterministic));

        await shutdownSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            synchronizer.TrySynchronizeAsync(shutdownSource.Token));
    }

    private static DemoStageSynchronizer CreateSynchronizer(ICommandCenterStageReader reader, DemoStageGate gate) =>
        new(
            reader,
            gate,
            new FoundryCredentialWarmup(new FakeTokenCredential(), NullLogger<FoundryCredentialWarmup>.Instance),
            CreateTransitionEffects(),
            NullLogger<DemoStageSynchronizer>.Instance);

    private static StageTransitionEffects CreateTransitionEffects()
    {
        var approvals = new PendingApprovalStore(TimeProvider.System, NullLogger<PendingApprovalStore>.Instance);
        return new StageTransitionEffects(
            approvals,
            new ToolSourceSwitch(),
            StageTransitionEffectsTests.CreateWorkflowService(approvals),
            new SecurityConsultSwitch(),
            StageTransitionEffectsTests.CreateWarmup(),
            StageTransitionEffectsTests.CreateWorkforceWarmup(),
            NullLogger<StageTransitionEffects>.Instance);
    }

    private static DemoStageStatus CreateStatus(DemoStage stage) =>
        new(stage, stage.ToString(), "Test stage", ["Test"], DateTimeOffset.UtcNow, "sync-corr");

    private sealed class FakeStageReader : ICommandCenterStageReader
    {
        public Func<string, CancellationToken, Task<DemoStageStatus>>? OnGetCurrentStageAsync { get; set; }

        public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken) =>
            OnGetCurrentStageAsync is not null
                ? OnGetCurrentStageAsync(correlationId, cancellationToken)
                : throw new InvalidOperationException("No stage configured.");
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("fake-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AccessToken("fake-token", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
