using System.Collections.Concurrent;
using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Keeps serialized conversational session state across independent HTTP requests so a follow-up
/// question can continue the previous turn. Each request deserializes its own private
/// <c>AgentSession</c> instance from this state, so no live session object is ever shared between
/// requests or agent instances. Session state is conversational context only; it is never the
/// authoritative operational state, which stays in the deterministic Hubs.
/// </summary>
public sealed class AgentSessionStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private const int MaxSessions = 50;

    private readonly ConcurrentDictionary<string, StoredSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the number of stored sessions; never exceeds the capacity cap after a save completes.
    /// </summary>
    public int Count => _sessions.Count;

    /// <summary>
    /// Gets the serialized session state for the supplied identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier returned by an earlier request.</param>
    /// <param name="state">The serialized session state, when the identifier is known and current.</param>
    /// <returns><see langword="false"/> when the identifier is unknown or expired.</returns>
    public bool TryGetState(string sessionId, out JsonElement state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        Prune();

        if (_sessions.TryGetValue(sessionId, out var stored))
        {
            state = stored.State;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>
    /// Stores the serialized session state and returns its identifier: the supplied identifier when
    /// it is still known, otherwise a newly created one. Concurrent saves for the same identifier
    /// are last-writer-wins; requests get isolated session instances, so the worst case is a lost
    /// turn of conversational context, never corrupted state.
    /// </summary>
    /// <param name="sessionId">The identifier the caller supplied, or <see langword="null"/>.</param>
    /// <param name="state">The serialized session state after the current run.</param>
    /// <returns>The identifier a follow-up question should send.</returns>
    public string SaveState(string? sessionId, JsonElement state)
    {
        Prune();

        var now = _timeProvider.GetUtcNow();
        var stored = new StoredSession(state.Clone(), now);

        if (sessionId is not null && _sessions.ContainsKey(sessionId))
        {
            _sessions[sessionId] = stored;
            return sessionId;
        }

        var newSessionId = Guid.NewGuid().ToString("N");
        _sessions[newSessionId] = stored;

        // Enforce the capacity cap after the insertion, so the store never settles above it.
        Prune();
        return newSessionId;
    }

    private void Prune()
    {
        var cutoff = _timeProvider.GetUtcNow() - SessionLifetime;

        foreach (var pair in _sessions)
        {
            if (pair.Value.LastUsedAt < cutoff)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }

        // Capacity guard for long-running demos: evict the oldest sessions beyond the cap.
        while (_sessions.Count > MaxSessions)
        {
            var oldest = _sessions.MinBy(pair => pair.Value.LastUsedAt);
            _sessions.TryRemove(oldest.Key, out _);
        }
    }

    private sealed record StoredSession(JsonElement State, DateTimeOffset LastUsedAt);
}
