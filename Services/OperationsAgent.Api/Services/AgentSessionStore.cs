using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Keeps serialized conversational session state across independent HTTP requests so a follow-up
/// question can continue the previous turn. Each request deserializes its own private
/// <c>AgentSession</c> instance from this state, so no live session object is ever shared between
/// requests or agent instances. All store mutations run under one lock, keeping the expiry and
/// capacity invariants exact under concurrency; concurrent saves for the same identifier are
/// last-writer-wins at the turn level. Session state is conversational context only; it is never
/// the authoritative operational state, which stays in the deterministic Hubs.
/// </summary>
public sealed class AgentSessionStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private const int MaxSessions = 50;

    private readonly object _gate = new();
    private readonly Dictionary<string, StoredSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the number of stored sessions; never exceeds the capacity cap.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    /// <summary>
    /// Gets the serialized session state for the supplied identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier returned by an earlier request.</param>
    /// <param name="state">The serialized session state, when the identifier is known and current.</param>
    /// <returns><see langword="false"/> when the identifier is unknown or expired.</returns>
    public bool TryGetState(string sessionId, out JsonElement state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_gate)
        {
            Prune();

            if (_sessions.TryGetValue(sessionId, out var stored))
            {
                state = stored.State;
                return true;
            }

            state = default;
            return false;
        }
    }

    /// <summary>
    /// Stores the serialized session state and returns its identifier: the supplied identifier when
    /// it is still known, otherwise a newly created one.
    /// </summary>
    /// <param name="sessionId">The identifier the caller supplied, or <see langword="null"/>.</param>
    /// <param name="state">The serialized session state after the current run.</param>
    /// <returns>The identifier a follow-up question should send.</returns>
    public string SaveState(string? sessionId, JsonElement state)
    {
        lock (_gate)
        {
            Prune();

            var stored = new StoredSession(state.Clone(), _timeProvider.GetUtcNow());

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
    }

    private void Prune()
    {
        // Callers hold the gate.
        var cutoff = _timeProvider.GetUtcNow() - SessionLifetime;
        var expired = _sessions
            .Where(pair => pair.Value.LastUsedAt < cutoff)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expired)
        {
            _sessions.Remove(key);
        }

        while (_sessions.Count > MaxSessions)
        {
            var oldest = _sessions.MinBy(pair => pair.Value.LastUsedAt);
            _sessions.Remove(oldest.Key);
        }
    }

    private sealed record StoredSession(JsonElement State, DateTimeOffset LastUsedAt);
}
