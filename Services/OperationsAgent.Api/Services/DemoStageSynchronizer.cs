using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Reads the authoritative current demo stage from the Command Center boundary.
/// </summary>
internal sealed class CommandCenterStageReader(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <summary>
    /// Gets the current authoritative demo stage.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the read.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current stage.</returns>
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
/// Seeds the local stage gate from the authoritative Command Center stage at startup, so a restarted
/// Operations Agent does not diverge from the stage the presenter already applied. The switchboard
/// remains the push path for later stage changes; this closes the restart gap only.
/// </summary>
internal sealed partial class DemoStageSynchronizer(
    CommandCenterStageReader stageReader,
    DemoStageGate stageGate,
    FoundryCredentialWarmup credentialWarmup,
    ILogger<DemoStageSynchronizer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private const int MaxAttempts = 15;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var stage = await stageReader.GetCurrentStageAsync(CorrelationIds.Create(), stoppingToken);
                stageGate.SetCurrent(stage);
                StageSynchronizerLog.Synchronized(logger, stage.Name);

                if (stageGate.IsAgentEnabled)
                {
                    credentialWarmup.EnsureStarted();
                }

                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException
                && !stoppingToken.IsCancellationRequested)
            {
                if (attempt == MaxAttempts)
                {
                    StageSynchronizerLog.GaveUp(logger, attempt, exception);
                    return;
                }

                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}

internal static partial class StageSynchronizerLog
{
    [LoggerMessage(
        EventId = 2480,
        Level = LogLevel.Information,
        Message = "Demo stage synchronized from the Command Center at startup: {StageName}.")]
    internal static partial void Synchronized(ILogger logger, string stageName);

    [LoggerMessage(
        EventId = 2481,
        Level = LogLevel.Warning,
        Message = "Demo stage could not be synchronized from the Command Center after {Attempts} attempts; keeping the configured startup stage until the switchboard propagates one.")]
    internal static partial void GaveUp(ILogger logger, int attempts, Exception exception);
}
