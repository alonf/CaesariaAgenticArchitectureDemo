namespace EnergyHub.Api.Services;

/// <summary>
/// Runs the twin hydration at startup, retrying until the device layer answers.
/// </summary>
/// <remarks>
/// The retry loop exists because the hub and the SmartPole simulator come up together and in no
/// guaranteed order - locally under the AppHost and in Container Apps alike. Losing the race to a
/// scenario is not a failure: <see cref="EnergyHubService.HydrateFromDeviceAsync"/> yields to any
/// twin that was already shaped, so in the switchboard-driven habitat this loop quietly concedes
/// to the first applied scenario, and in the deployed habitat - where no scenario ever arrives -
/// it is the only thing standing between the hub and serving its constructor baseline forever.
/// </remarks>
internal sealed partial class TwinHydration(
    EnergyHubService energyHub,
    ILogger<TwinHydration> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var correlationId = CorrelationIds.Create();
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                await energyHub.HydrateFromDeviceAsync(correlationId, stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or OperationCanceledException)
            {
                // The device layer is not up yet, or not answering yet. Worth saying each time:
                // a hub that silently never hydrates reads as a SmartPole bug from the outside.
                TwinHydrationLog.Retrying(logger, attempt, exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}

internal static partial class TwinHydrationLog
{
    [LoggerMessage(
        EventId = 1565,
        Level = LogLevel.Warning,
        Message = "Twin hydration attempt {Attempt} could not read the SmartPole state; retrying.")]
    internal static partial void Retrying(ILogger logger, int attempt, Exception exception);
}
