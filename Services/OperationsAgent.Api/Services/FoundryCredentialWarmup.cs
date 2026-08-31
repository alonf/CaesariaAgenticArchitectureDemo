using System.Diagnostics;
using Azure.Core;
using Azure.Identity;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Warms the Azure credential chain in the background, so the first operator question does not pay
/// the DefaultAzureCredential discovery cost (managed-identity probe, CLI and PowerShell process
/// spawns - tens of seconds on a developer machine). The warmup runs at most once per process and is
/// triggered only when an agent-enabled stage becomes active, so the Deterministic stage keeps its
/// promise that no AI credential is used.
/// </summary>
public sealed partial class FoundryCredentialWarmup(
    TokenCredential credential,
    ILogger<FoundryCredentialWarmup> logger)
{
    private const string FoundryScope = "https://ai.azure.com/.default";
    private static readonly TimeSpan WarmupBudget = TimeSpan.FromMinutes(2);
    private int _started;

    /// <summary>
    /// Starts the background warmup once; later calls are no-ops. Failures are logged and swallowed:
    /// the first agent request simply acquires the token itself.
    /// </summary>
    public void EnsureStarted()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        _ = Task.Run(WarmAsync);
    }

    private async Task WarmAsync()
    {
        using var timeoutSource = new CancellationTokenSource(WarmupBudget);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([FoundryScope]), timeoutSource.Token);
            CredentialWarmupLog.Completed(logger, stopwatch.ElapsedMilliseconds, token.ExpiresOn);
        }
        catch (OperationCanceledException)
        {
            CredentialWarmupLog.TimedOut(logger, stopwatch.ElapsedMilliseconds);
        }
        catch (CredentialUnavailableException exception)
        {
            CredentialWarmupLog.CredentialUnavailable(logger, stopwatch.ElapsedMilliseconds, exception);
        }
        catch (AuthenticationFailedException exception)
        {
            CredentialWarmupLog.AuthenticationFailed(logger, stopwatch.ElapsedMilliseconds, exception);
        }
        catch (Exception exception)
        {
            CredentialWarmupLog.Failed(logger, stopwatch.ElapsedMilliseconds, exception);
        }
    }
}

internal static partial class CredentialWarmupLog
{
    [LoggerMessage(
        EventId = 2470,
        Level = LogLevel.Information,
        Message = "Foundry credential warmup completed in {ElapsedMs} ms. Token expires at {ExpiresOn:O}.")]
    internal static partial void Completed(ILogger logger, long elapsedMs, DateTimeOffset expiresOn);

    [LoggerMessage(
        EventId = 2471,
        Level = LogLevel.Warning,
        Message = "Foundry credential warmup timed out after {ElapsedMs} ms; the first agent request will acquire the token instead.")]
    internal static partial void TimedOut(ILogger logger, long elapsedMs);

    [LoggerMessage(
        EventId = 2472,
        Level = LogLevel.Warning,
        Message = "No Azure credential is available (checked for {ElapsedMs} ms); agent requests will fail until one is configured.")]
    internal static partial void CredentialUnavailable(ILogger logger, long elapsedMs, Exception exception);

    [LoggerMessage(
        EventId = 2473,
        Level = LogLevel.Warning,
        Message = "Azure authentication failed during credential warmup after {ElapsedMs} ms.")]
    internal static partial void AuthenticationFailed(ILogger logger, long elapsedMs, Exception exception);

    [LoggerMessage(
        EventId = 2474,
        Level = LogLevel.Warning,
        Message = "Foundry credential warmup failed after {ElapsedMs} ms; the first agent request will acquire the token instead.")]
    internal static partial void Failed(ILogger logger, long elapsedMs, Exception exception);
}
