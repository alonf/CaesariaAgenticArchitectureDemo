using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Reads the authoritative current demo stage from the Command Center boundary.
/// </summary>
public interface ICommandCenterStageReader
{
    /// <summary>
    /// Gets the current authoritative demo stage.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the read.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current stage.</returns>
    public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ICommandCenterStageReader"/>
public sealed class CommandCenterStageReader(HttpClient httpClient) : ICommandCenterStageReader
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/command-center/stage");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoStageStatus>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Command Center returned an empty stage response.");
    }
}

/// <summary>
/// Continuously reconciles the local stage gate with the authoritative Command Center stage. The
/// switchboard push remains the fast path; this loop closes every divergence window - a restarted
/// Operations Agent, or a stage change whose push to this service failed - within one poll interval,
/// so a direct request can never keep reaching Foundry after the authoritative stage went back to
/// Deterministic. The <see cref="DemoStageGate"/> ordering guard keeps a stale read from
/// overwriting a newer pushed stage.
/// </summary>
public sealed partial class DemoStageSynchronizer(
    ICommandCenterStageReader stageReader,
    DemoStageGate stageGate,
    FoundryCredentialWarmup credentialWarmup,
    ILogger<DemoStageSynchronizer> logger) : BackgroundService
{
    private static readonly TimeSpan InitialRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int FailureLogEvery = 30;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var synchronizedOnce = false;
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (await TrySynchronizeAsync(stoppingToken))
            {
                synchronizedOnce = true;
                consecutiveFailures = 0;
            }
            else
            {
                consecutiveFailures++;

                if (consecutiveFailures == 1 || consecutiveFailures % FailureLogEvery == 0)
                {
                    StageSynchronizerLog.SynchronizationFailing(logger, consecutiveFailures);
                }
            }

            try
            {
                await Task.Delay(synchronizedOnce ? PollInterval : InitialRetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Performs one reconciliation attempt. Every failure except host shutdown is swallowed and
    /// reported through the return value, so no transport, resilience, or parsing exception can
    /// escape the background loop and stop the host.
    /// </summary>
    /// <param name="cancellationToken">The token that observes host shutdown.</param>
    /// <returns><see langword="true"/> when the authoritative stage was read and applied.</returns>
    public async Task<bool> TrySynchronizeAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();

        try
        {
            var stage = await stageReader.GetCurrentStageAsync(correlationId, cancellationToken);
            var previous = stageGate.GetCurrent();
            var applied = stageGate.SetCurrent(stage);

            if (applied.Id != previous.Id)
            {
                StageSynchronizerLog.Synchronized(logger, applied.Name, correlationId);
            }

            if (stageGate.IsAgentEnabled)
            {
                credentialWarmup.EnsureStarted();
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            StageSynchronizerLog.AttemptFailed(logger, correlationId, exception);
            return false;
        }
    }
}

internal static partial class StageSynchronizerLog
{
    [LoggerMessage(
        EventId = 2480,
        Level = LogLevel.Information,
        Message = "Demo stage reconciled from the Command Center: {StageName}. CorrelationId: {CorrelationId}.")]
    internal static partial void Synchronized(ILogger logger, string stageName, string correlationId);

    [LoggerMessage(
        EventId = 2481,
        Level = LogLevel.Warning,
        Message = "Demo stage reconciliation has failed {ConsecutiveFailures} consecutive time(s); keeping the last known stage until the Command Center is reachable.")]
    internal static partial void SynchronizationFailing(ILogger logger, int consecutiveFailures);

    [LoggerMessage(
        EventId = 2482,
        Level = LogLevel.Debug,
        Message = "Demo stage reconciliation attempt failed. CorrelationId: {CorrelationId}.")]
    internal static partial void AttemptFailed(ILogger logger, string correlationId, Exception exception);
}
