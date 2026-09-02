namespace OperationsAgent.Api.Services;

/// <summary>
/// Wakes the Security Operations Agent's credential chain ahead of the first consult.
/// <para>
/// That agent runs in its own process with its own <c>DefaultAzureCredential</c>, and it has no
/// idea which demo stage is current - only this service holds the authoritative stage. The consult
/// is already the slowest beat in the lecture (a nested agent inside an agent run), so the first
/// one must not also pay for credential discovery. Reaching the MultiAgent stage is the signal:
/// the presenter still has to turn the consult on and ask, which is the lead time this buys.
/// </para>
/// <para>
/// Fire-and-forget and at most once per process, like the local warmup. A failure is logged and
/// swallowed: the consult itself still works, it is just slower.
/// </para>
/// </summary>
public sealed partial class SecurityAgentWarmup(
    IHttpClientFactory httpClientFactory,
    ILogger<SecurityAgentWarmup> logger)
{
    /// <summary>
    /// The named client addressing the Security Operations Agent.
    /// </summary>
    public const string HttpClientName = "securityagent-warmup";

    private int _started;

    /// <summary>
    /// Asks the Security Operations Agent to warm its credential, once per process.
    /// </summary>
    public void EnsureStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _ = Task.Run(WakeAsync);
    }

    // The route deliberately avoids the Security Hub's route prefix. An architecture test asserts
    // that no Hub path appears anywhere in this service - including in comments - because that
    // guard is the backbone of the MultiAgent argument, and waking a sibling agent must not read
    // like reaching into the domain this service is not allowed to touch.
    private async Task WakeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsync(
                new Uri("/api/agent-warmup", UriKind.Relative), content: null, timeout.Token);

            response.EnsureSuccessStatusCode();
            SecurityAgentWarmupLog.Requested(logger);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            SecurityAgentWarmupLog.Failed(logger, exception);
        }
    }
}

internal static partial class SecurityAgentWarmupLog
{
    [LoggerMessage(
        EventId = 2480,
        Level = LogLevel.Information,
        Message = "Asked the Security Operations Agent to warm its credential ahead of the first consult.")]
    internal static partial void Requested(ILogger logger);

    [LoggerMessage(
        EventId = 2481,
        Level = LogLevel.Warning,
        Message = "The Security Operations Agent credential warmup could not be requested; the first consult will acquire the token itself.")]
    internal static partial void Failed(ILogger logger, Exception exception);
}
