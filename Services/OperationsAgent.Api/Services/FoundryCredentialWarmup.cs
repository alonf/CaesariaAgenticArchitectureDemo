using System.Diagnostics;
using Azure.Core;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Warms the Azure credential chain in the background at startup, so the first operator question does
/// not pay the DefaultAzureCredential discovery cost (managed-identity probe, CLI and PowerShell
/// process spawns - tens of seconds on a developer machine). A failure is logged and ignored: the
/// Deterministic stage must keep starting cleanly on machines with no Azure credential at all.
/// </summary>
internal sealed partial class FoundryCredentialWarmup(
    TokenCredential credential,
    ILogger<FoundryCredentialWarmup> logger) : BackgroundService
{
    private const string FoundryScope = "https://ai.azure.com/.default";
    private static readonly TimeSpan WarmupBudget = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeoutSource.CancelAfter(WarmupBudget);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext([FoundryScope]), timeoutSource.Token);
            CredentialWarmupLog.Completed(logger, stopwatch.ElapsedMilliseconds, token.ExpiresOn);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown before the warmup finished; nothing to report.
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
        Message = "Foundry credential warmup did not complete after {ElapsedMs} ms; the first agent request will acquire the token instead.")]
    internal static partial void Failed(ILogger logger, long elapsedMs, Exception exception);
}
