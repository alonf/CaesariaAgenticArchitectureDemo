using System.Text.Json;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class AgentSessionStoreTests
{
    [Fact]
    public void SaveStateCreatesIdAndRoundTripsState()
    {
        var store = new AgentSessionStore(new TestTimeProvider());

        var sessionId = store.SaveState(null, CreateState("turn-1"));

        Assert.True(store.TryGetState(sessionId, out var state));
        Assert.Equal("turn-1", state.GetProperty("marker").GetString());
    }

    [Fact]
    public void SaveStateWithKnownIdUpdatesInPlace()
    {
        var store = new AgentSessionStore(new TestTimeProvider());
        var sessionId = store.SaveState(null, CreateState("turn-1"));

        var updatedId = store.SaveState(sessionId, CreateState("turn-2"));

        Assert.Equal(sessionId, updatedId);
        Assert.True(store.TryGetState(sessionId, out var state));
        Assert.Equal("turn-2", state.GetProperty("marker").GetString());
    }

    [Fact]
    public void TryGetStateRejectsUnknownId()
    {
        var store = new AgentSessionStore(new TestTimeProvider());

        Assert.False(store.TryGetState("unknown-session", out _));
    }

    [Fact]
    public void ExpiredSessionIsPruned()
    {
        var clock = new TestTimeProvider();
        var store = new AgentSessionStore(clock);
        var sessionId = store.SaveState(null, CreateState("turn-1"));

        clock.Advance(TimeSpan.FromMinutes(31));

        Assert.False(store.TryGetState(sessionId, out _));
    }

    [Fact]
    public void SaveStatePrunesExpiredEntries()
    {
        var clock = new TestTimeProvider();
        var store = new AgentSessionStore(clock);
        var oldSessionId = store.SaveState(null, CreateState("old"));

        clock.Advance(TimeSpan.FromMinutes(31));
        store.SaveState(null, CreateState("new"));

        // The expired entry is gone even though it was never read again.
        Assert.False(store.TryGetState(oldSessionId, out _));
    }

    [Fact]
    public void CapacityIsBoundedByEvictingOldestSessions()
    {
        var clock = new TestTimeProvider();
        var store = new AgentSessionStore(clock);
        var firstSessionId = store.SaveState(null, CreateState("first"));

        for (var i = 0; i < 60; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            store.SaveState(null, CreateState($"session-{i}"));
        }

        Assert.False(store.TryGetState(firstSessionId, out _));

        // The cap is an exact invariant after every save, not an eventual one.
        Assert.Equal(50, store.Count);
    }

    [Fact]
    public async Task ConcurrentSavesNeverExceedTheCapacityCap()
    {
        var store = new AgentSessionStore(new TestTimeProvider());

        var writers = Enumerable.Range(0, 8).Select(writer => Task.Run(() =>
        {
            string? knownSessionId = null;

            for (var i = 0; i < 50; i++)
            {
                knownSessionId = store.SaveState(i % 3 == 0 ? knownSessionId : null, CreateState($"w{writer}-s{i}"));
                Assert.True(store.Count <= 50);
            }
        }));

        await Task.WhenAll(writers);

        Assert.Equal(50, store.Count);
    }

    private static JsonElement CreateState(string marker)
    {
        using var document = JsonDocument.Parse($"{{\"marker\":\"{marker}\"}}");
        return document.RootElement.Clone();
    }
}
