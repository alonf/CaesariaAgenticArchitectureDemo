using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class PendingApprovalStoreTests
{
    [Fact]
    public async Task DecisionReleasesTheWaitingCall()
    {
        var store = CreateStore();
        var (id, decision) = store.Create("Restore L-417?", "approval-corr", CancellationToken.None);

        Assert.Contains(store.GetAll(), approval => approval.Id == id && approval.Message == "Restore L-417?");
        Assert.True(store.TryRespond(id, approved: true));
        Assert.True(await decision);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public async Task DenialIsDeliveredAsFalse()
    {
        var store = CreateStore();
        var (id, decision) = store.Create("Restore L-417?", "approval-corr", CancellationToken.None);

        Assert.True(store.TryRespond(id, approved: false));
        Assert.False(await decision);
    }

    [Fact]
    public void UnknownOrAlreadyAnsweredIdsAreRejected()
    {
        var store = CreateStore();
        var (id, _) = store.Create("Restore L-417?", "approval-corr", CancellationToken.None);

        Assert.False(store.TryRespond("unknown", approved: true));
        Assert.True(store.TryRespond(id, approved: true));
        Assert.False(store.TryRespond(id, approved: true));
    }

    [Fact]
    public async Task AbandonedRunCancelsThePendingApproval()
    {
        // If the agent run times out or is cancelled, the paused tool never executes and the
        // stale prompt disappears from the operator's list.
        var store = CreateStore();
        using var runCancellation = new CancellationTokenSource();
        var (_, decision) = store.Create("Restore L-417?", "approval-corr", runCancellation.Token);

        await runCancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decision);
        Assert.Empty(store.GetAll());
    }

    private static PendingApprovalStore CreateStore() =>
        new(new TestTimeProvider(), NullLogger<PendingApprovalStore>.Instance);
}
