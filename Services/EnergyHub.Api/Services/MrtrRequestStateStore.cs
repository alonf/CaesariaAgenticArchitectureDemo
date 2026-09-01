using System.Collections.Concurrent;

namespace EnergyHub.Api.Services;

/// <summary>
/// Issues and validates the one-time, expiring request state that binds an MRTR confirmation to
/// the invocation that asked for it. A continuation carrying no state, expired state, or state
/// issued for a different asset is rejected - a caller cannot fabricate or replay a confirmation.
/// </summary>
public sealed class MrtrRequestStateStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);
    private const int MaxOutstandingStates = 100;
    private readonly ConcurrentDictionary<string, (string AssetId, DateTimeOffset ExpiresAt)> _issued = new(StringComparer.Ordinal);

    /// <summary>
    /// Issues a new one-time state token bound to the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset the pending confirmation concerns.</param>
    /// <returns>The opaque state token to return with the input-required result.</returns>
    public string Issue(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        // A pause the client never returns to would otherwise leave its token behind forever.
        PruneExpired();

        // A hard ceiling as well as an expiry: a client that opens pauses in a loop cannot grow
        // the store without bound inside the five-minute window. The oldest unanswered pause is
        // dropped first, and its continuation is then refused rather than silently honored.
        while (_issued.Count >= MaxOutstandingStates)
        {
            var oldest = _issued.OrderBy(entry => entry.Value.ExpiresAt).Select(entry => entry.Key).FirstOrDefault();

            if (oldest is null || !_issued.TryRemove(oldest, out _))
            {
                break;
            }
        }

        var token = Guid.NewGuid().ToString("N");
        _issued[token] = (assetId, timeProvider.GetUtcNow() + StateLifetime);
        return token;
    }

    private void PruneExpired()
    {
        var now = timeProvider.GetUtcNow();

        foreach (var (token, issued) in _issued)
        {
            if (now > issued.ExpiresAt)
            {
                _issued.TryRemove(token, out _);
            }
        }
    }

    /// <summary>
    /// Consumes a state token: valid exactly once, before expiry, and only for the asset it was
    /// issued for.
    /// </summary>
    /// <param name="token">The state token echoed back by the client.</param>
    /// <param name="assetId">The asset the continuation targets.</param>
    /// <returns><see langword="true"/> when the token was valid and has now been consumed.</returns>
    public bool TryConsume(string? token, string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (string.IsNullOrWhiteSpace(token) || !_issued.TryRemove(token, out var issued))
        {
            return false;
        }

        return string.Equals(issued.AssetId, assetId, StringComparison.OrdinalIgnoreCase)
            && timeProvider.GetUtcNow() <= issued.ExpiresAt;
    }
}
