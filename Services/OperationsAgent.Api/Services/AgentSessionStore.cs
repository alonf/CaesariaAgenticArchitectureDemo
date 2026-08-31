using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Keeps conversational <see cref="AgentSession"/> state across independent HTTP requests so a
/// follow-up question can continue the previous turn. Session state is conversational context only;
/// it is never the authoritative operational state, which stays in the deterministic Hubs.
/// </summary>
public sealed class AgentSessionStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, StoredSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Gets the live session for the supplied identifier, or <see langword="null"/> when it is
    /// unknown or expired.
    /// </summary>
    /// <param name="sessionId">The session identifier returned by an earlier request.</param>
    /// <returns>The stored session, or <see langword="null"/>.</returns>
    public AgentSession? TryGet(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        Prune();

        return _sessions.TryGetValue(sessionId, out var stored) ? stored.Session : null;
    }

    /// <summary>
    /// Stores the supplied session and returns its identifier: the existing identifier when the
    /// session was already stored, otherwise a newly created one.
    /// </summary>
    /// <param name="sessionId">The identifier the caller supplied, or <see langword="null"/>.</param>
    /// <param name="session">The session to keep for follow-up questions.</param>
    /// <returns>The identifier a follow-up question should send.</returns>
    public string Save(string? sessionId, AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var now = _timeProvider.GetUtcNow();

        if (sessionId is not null && _sessions.ContainsKey(sessionId))
        {
            _sessions[sessionId] = new StoredSession(session, now);
            return sessionId;
        }

        var newSessionId = Guid.NewGuid().ToString("N");
        _sessions[newSessionId] = new StoredSession(session, now);
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
    }

    private sealed record StoredSession(AgentSession Session, DateTimeOffset LastUsedAt);
}
