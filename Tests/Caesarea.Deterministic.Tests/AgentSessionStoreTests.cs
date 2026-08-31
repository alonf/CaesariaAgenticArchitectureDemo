using System.Text.Json;
using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class AgentSessionStoreTests
{
    [Fact]
    public void SaveStateCreatesIdAndRoundTripsState()
    {
        var store = new AgentSessionStore(new TestTimeProvider());

        var sessionId = store.SaveState(null, CreateState("turn-1"), DemoStage.Session);

        Assert.True(store.TryGetState(sessionId, DemoStage.Session, out var state));
        Assert.Equal("turn-1", state.GetProperty("marker").GetString());
    }

    [Fact]
    public void SaveStateWithKnownIdUpdatesInPlace()
    {
        var store = new AgentSessionStore(new TestTimeProvider());
        var sessionId = store.SaveState(null, CreateState("turn-1"), DemoStage.Session);

        var updatedId = store.SaveState(sessionId, CreateState("turn-2"), DemoStage.Session);

        Assert.Equal(sessionId, updatedId);
        Assert.True(store.TryGetState(sessionId, DemoStage.Session, out var state));
        Assert.Equal("turn-2", state.GetProperty("marker").GetString());
    }

    [Fact]
    public void TryGetStateRejectsUnknownId()
    {
        var store = new AgentSessionStore(new TestTimeProvider());

        Assert.False(store.TryGetState("unknown-session", DemoStage.Session, out _));
    }

    [Fact]
    public void SessionContinuesAtALaterStage()
    {
        // Stages are cumulative: a Session-stage conversation may continue after upgrading.
        var store = new AgentSessionStore(new TestTimeProvider());
        var sessionId = store.SaveState(null, CreateState("turn-1"), DemoStage.Session);

        Assert.True(store.TryGetState(sessionId, DemoStage.Knowledge, out _));
    }

    [Fact]
    public void SessionIsRejectedAtAnEarlierStage()
    {
        // A Skills-stage conversation may carry loaded procedures and tool results; continuing it
        // at Memory would leak capabilities the earlier stage must not expose.
        var store = new AgentSessionStore(new TestTimeProvider());
        var sessionId = store.SaveState(null, CreateState("turn-1"), DemoStage.Skills);

        Assert.False(store.TryGetState(sessionId, DemoStage.Memory, out _));
        Assert.True(store.TryGetState(sessionId, DemoStage.Skills, out _));
    }

    [Fact]
    public void SessionHighestStageIsMonotonic()
    {
        // Continuing at a higher stage raises the session's floor; a later save at a lower
        // allowed stage does not lower it back.
        var store = new AgentSessionStore(new TestTimeProvider());
        var sessionId = store.SaveState(null, CreateState("turn-1"), DemoStage.Session);
        store.SaveState(sessionId, CreateState("turn-2"), DemoStage.Skills);
        store.SaveState(sessionId, CreateState("turn-3"), DemoStage.Skills);

        Assert.False(store.TryGetState(sessionId, DemoStage.Knowledge, out _));
        Assert.True(store.TryGetState(sessionId, DemoStage.Skills, out _));
    }

    [Fact]
    public void ExpiredSessionIsPruned()
    {
        var clock = new TestTimeProvider();
        var store = new AgentSessionStore(clock);
        var sessionId = store.SaveState(null, CreateState("turn-1"), DemoStage.Session);

        clock.Advance(TimeSpan.FromMinutes(31));

        Assert.False(store.TryGetState(sessionId, DemoStage.Session, out _));
    }

    [Fact]
    public void SaveStatePrunesExpiredEntries()
    {
        var clock = new TestTimeProvider();
        var store = new AgentSessionStore(clock);
        var oldSessionId = store.SaveState(null, CreateState("old"), DemoStage.Session);

        clock.Advance(TimeSpan.FromMinutes(31));
        store.SaveState(null, CreateState("new"), DemoStage.Session);

        // The expired entry is gone even though it was never read again.
        Assert.False(store.TryGetState(oldSessionId, DemoStage.Session, out _));
    }

    [Fact]
    public void CapacityIsBoundedByEvictingOldestSessions()
    {
        var clock = new TestTimeProvider();
        var store = new AgentSessionStore(clock);
        var firstSessionId = store.SaveState(null, CreateState("first"), DemoStage.Session);

        for (var i = 0; i < 60; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            store.SaveState(null, CreateState($"session-{i}"), DemoStage.Session);
        }

        Assert.False(store.TryGetState(firstSessionId, DemoStage.Session, out _));

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
                knownSessionId = store.SaveState(i % 3 == 0 ? knownSessionId : null, CreateState($"w{writer}-s{i}"), DemoStage.Session);
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
