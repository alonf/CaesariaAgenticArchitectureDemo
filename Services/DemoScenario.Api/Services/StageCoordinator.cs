using Polly.Timeout;

namespace DemoScenario.Api.Services;

/// <summary>
/// Coordinates the presenter-selected demo stage and propagates it to the Command Center boundary.
/// </summary>
public sealed partial class StageCoordinator : IDisposable
{
    private readonly SemaphoreSlim _applicationLock = new(1, 1);
    private readonly ICommandCenterStageClient _commandCenterStageClient;
    private readonly IOperationsAgentStageClient _operationsAgentStageClient;
    private readonly StageCatalog _stageCatalog;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StageCoordinator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StageCoordinator"/> class.
    /// </summary>
    /// <param name="commandCenterStageClient">The client used to propagate stage changes to the Command Center.</param>
    /// <param name="operationsAgentStageClient">The client used to propagate stage changes to the Operations Agent.</param>
    /// <param name="stageCatalog">The catalog of available demo stages.</param>
    /// <param name="timeProvider">The clock used to stamp stage changes.</param>
    /// <param name="logger">The logger used for stage coordination events.</param>
    public StageCoordinator(
        ICommandCenterStageClient commandCenterStageClient,
        IOperationsAgentStageClient operationsAgentStageClient,
        StageCatalog stageCatalog,
        TimeProvider timeProvider,
        ILogger<StageCoordinator> logger)
    {
        _commandCenterStageClient = commandCenterStageClient ?? throw new ArgumentNullException(nameof(commandCenterStageClient));
        _operationsAgentStageClient = operationsAgentStageClient ?? throw new ArgumentNullException(nameof(operationsAgentStageClient));
        _stageCatalog = stageCatalog ?? throw new ArgumentNullException(nameof(stageCatalog));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the demo stage currently considered authoritative by the switchboard.
    /// </summary>
    /// <returns>The current demo stage.</returns>
    public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken) =>
        _commandCenterStageClient.GetCurrentStageAsync(correlationId, cancellationToken);

    /// <summary>
    /// Applies the supplied demo stage and propagates it to the Command Center.
    /// </summary>
    /// <param name="stage">The demo stage to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the stage change.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The stage change result.</returns>
    public async Task<DemoStageChangeResult> ApplyAsync(DemoStage stage, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var descriptor = _stageCatalog.GetDescriptor(stage);
        var status = new DemoStageStatus(
            descriptor.Id,
            descriptor.Name,
            descriptor.Description,
            descriptor.Capabilities,
            _timeProvider.GetUtcNow(),
            correlationId,
            descriptor.Walkthrough);

        await _applicationLock.WaitAsync(cancellationToken);

        try
        {
            StageCoordinatorLog.ApplyingStage(_logger, descriptor.Name, correlationId);

            var appliedStage = await _commandCenterStageClient.ApplyStageAsync(status, correlationId, cancellationToken);
            var summary = $"Stage set to {appliedStage.Name}.";

            // The Operations Agent may legitimately be unavailable during the deterministic lecture
            // path, so a failed agent propagation degrades to a visible warning instead of failing
            // the stage change; the agent's own stage gate keeps it safe until it catches up. The
            // resilience pipeline surfaces its attempt timeout as TimeoutRejectedException, and its
            // internal cancellation as OperationCanceledException without the caller's token being
            // cancelled - both degrade the same way. Real caller cancellation still propagates.
            try
            {
                await _operationsAgentStageClient.ApplyStageAsync(status, correlationId, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException
                || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                StageCoordinatorLog.AgentPropagationFailed(_logger, descriptor.Name, correlationId, exception);
                summary += " Warning: could not confirm the stage change reached the Operations Agent; background reconciliation will retry.";
            }

            StageCoordinatorLog.StageApplied(_logger, descriptor.Name, correlationId);
            return new DemoStageChangeResult(appliedStage, summary);
        }
        finally
        {
            _applicationLock.Release();
        }
    }

    /// <summary>
    /// Releases the synchronization primitive used to serialize stage changes.
    /// </summary>
    public void Dispose() => _applicationLock.Dispose();
}

internal static partial class StageCoordinatorLog
{
    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Applying demo stage {StageName}. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplyingStage(ILogger logger, string stageName, string correlationId);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Demo stage {StageName} applied and propagated to the Command Center. CorrelationId: {CorrelationId}.")]
    internal static partial void StageApplied(ILogger logger, string stageName, string correlationId);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "Demo stage {StageName} could not be propagated to the Operations Agent. CorrelationId: {CorrelationId}.")]
    internal static partial void AgentPropagationFailed(ILogger logger, string stageName, string correlationId, Exception exception);
}
