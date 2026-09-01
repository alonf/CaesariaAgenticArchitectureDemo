using System.Collections.Concurrent;
using EnergyHub.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class MrtrRequestStateStoreTests
{
    [Fact]
    public void IssuedStateIsConsumableExactlyOnce()
    {
        var store = new MrtrRequestStateStore(new TestTimeProvider());
        var token = store.Issue("L-417");

        Assert.True(store.TryConsume(token, "L-417"));
        Assert.False(store.TryConsume(token, "L-417"));
    }

    [Fact]
    public void MissingOrUnknownStateIsRejected()
    {
        var store = new MrtrRequestStateStore(new TestTimeProvider());

        Assert.False(store.TryConsume(null, "L-417"));
        Assert.False(store.TryConsume(string.Empty, "L-417"));
        Assert.False(store.TryConsume("fabricated-token", "L-417"));
    }

    [Fact]
    public void StateIssuedForAnotherAssetIsRejected()
    {
        var store = new MrtrRequestStateStore(new TestTimeProvider());
        var token = store.Issue("L-417");

        Assert.False(store.TryConsume(token, "L-528"));
        // The mismatch consumed the token: it cannot be replayed against the right asset either.
        Assert.False(store.TryConsume(token, "L-417"));
    }

    [Fact]
    public void ExpiredStateIsRejected()
    {
        var time = new TestTimeProvider();
        var store = new MrtrRequestStateStore(time);
        var token = store.Issue("L-417");

        time.Advance(TimeSpan.FromMinutes(6));

        Assert.False(store.TryConsume(token, "L-417"));
    }

    [Fact]
    public void AssetMatchIsCaseInsensitive()
    {
        var store = new MrtrRequestStateStore(new TestTimeProvider());
        var token = store.Issue("L-417");

        Assert.True(store.TryConsume(token, "l-417"));
    }

    [Fact]
    public void ConcurrentIssuersCannotPushThePauseStorePastItsCeiling()
    {
        // Prune, evict and insert happen under one lock, so issuers racing each other cannot each
        // observe room and then all take it. The store publishes no count, so the ceiling is
        // observed the way a client would meet it: exactly 100 of the 200 tokens still open a door.
        var store = new MrtrRequestStateStore(new TestTimeProvider());
        ConcurrentBag<string> tokens = [];

        Parallel.For(0, 200, _ => tokens.Add(store.Issue("L-417")));

        Assert.Equal(200, tokens.Count);
        Assert.Equal(100, tokens.Count(token => store.TryConsume(token, "L-417")));
    }
}
