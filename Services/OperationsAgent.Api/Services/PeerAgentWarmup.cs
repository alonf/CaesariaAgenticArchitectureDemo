namespace OperationsAgent.Api.Services;

/// <summary>
/// Wakes a peer agent's credential chain ahead of the first consult.
/// <para>
/// A peer runs in its own process with its own <c>DefaultAzureCredential</c>, and it has no idea
/// which demo stage is current - only this service holds the authoritative stage. Consulting a peer
/// is already among the slowest beats in the lecture (a whole agent run nested inside an agent
/// run), so the first one must not also pay for credential discovery. Reaching the stage that
/// unlocks the peer is the signal: the presenter still has to ask, which is the lead time this
/// buys.
/// </para>
/// <para>
/// Fire-and-forget and at most once per process, like the local warmup. A failure is logged and
/// swallowed: the consult itself still works, it is just slower.
/// </para>
/// </summary>
public abstract class PeerAgentWarmup(IHttpClientFactory httpClientFactory, ILogger logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private int _started;

    /// <summary>Gets the named client addressing the peer.</summary>
    protected abstract string ClientName { get; }

    /// <summary>Gets the peer's display name, used only in this service's own logs.</summary>
    protected abstract string PeerName { get; }

    /// <summary>
    /// Asks the peer to warm its credential, once per process.
    /// </summary>
    public void EnsureStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _ = Task.Run(WakeAsync);
    }

    // The route deliberately avoids every peer domain's own route prefix. An architecture test
    // asserts that no Hub path appears anywhere in this service - including in comments - because
    // that guard is the backbone of the delegation argument, and waking a sibling agent must not
    // read like reaching into a domain this service is not allowed to touch.
    private async Task WakeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            using var client = _httpClientFactory.CreateClient(ClientName);
            using var response = await client.PostAsync(
                new Uri("/api/agent-warmup", UriKind.Relative), content: null, timeout.Token);

            response.EnsureSuccessStatusCode();
            PeerAgentWarmupLog.Requested(_logger, PeerName);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            PeerAgentWarmupLog.Failed(_logger, PeerName, exception);
        }
    }
}

internal static partial class PeerAgentWarmupLog
{
    [LoggerMessage(
        EventId = 2480,
        Level = LogLevel.Information,
        Message = "Asked {PeerName} to warm its credential ahead of the first consult.")]
    internal static partial void Requested(ILogger logger, string peerName);

    [LoggerMessage(
        EventId = 2481,
        Level = LogLevel.Warning,
        Message = "The {PeerName} credential warmup could not be requested; the first consult will acquire the token itself.")]
    internal static partial void Failed(ILogger logger, string peerName, Exception exception);
}
