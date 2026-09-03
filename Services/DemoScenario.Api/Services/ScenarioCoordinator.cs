namespace DemoScenario.Api.Services;

/// <summary>
/// Coordinates deterministic scenario application across SmartPole, Energy Hub, and Command Center boundaries.
/// </summary>
public sealed partial class ScenarioCoordinator : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _applicationLock = new(1, 1);
    private readonly ISmartPoleScenarioClient _smartpoleScenarioClient;
    private readonly IEnergyScenarioClient _energyScenarioClient;
    private readonly ISecurityScenarioClient _securityScenarioClient;
    private readonly IWorkforceScenarioClient _workforceScenarioClient;
    private readonly ICommandCenterScenarioClient _commandCenterScenarioClient;
    private readonly ScenarioCatalog _scenarioCatalog;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScenarioCoordinator> _logger;
    private ScenarioStatus _currentScenario;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScenarioCoordinator"/> class.
    /// </summary>
    /// <param name="smartpoleScenarioClient">The client used to reset and seed the SmartPole simulator.</param>
    /// <param name="energyScenarioClient">The client used to synchronize the Energy Hub.</param>
    /// <param name="commandCenterScenarioClient">The client used to synchronize the Command Center.</param>
    /// <param name="scenarioCatalog">The catalog of deterministic scenario recipes.</param>
    /// <param name="timeProvider">The clock used to stamp scenario state changes.</param>
    /// <param name="securityScenarioClient">The Security Hub scenario client.</param>
    /// <param name="workforceScenarioClient">The Workforce Hub scenario client.</param>
    /// <param name="logger">The logger used for scenario orchestration events.</param>
    public ScenarioCoordinator(
        ISmartPoleScenarioClient smartpoleScenarioClient,
        IEnergyScenarioClient energyScenarioClient,
        ISecurityScenarioClient securityScenarioClient,
        IWorkforceScenarioClient workforceScenarioClient,
        ICommandCenterScenarioClient commandCenterScenarioClient,
        ScenarioCatalog scenarioCatalog,
        TimeProvider timeProvider,
        ILogger<ScenarioCoordinator> logger)
    {
        _smartpoleScenarioClient = smartpoleScenarioClient ?? throw new ArgumentNullException(nameof(smartpoleScenarioClient));
        _energyScenarioClient = energyScenarioClient ?? throw new ArgumentNullException(nameof(energyScenarioClient));
        _securityScenarioClient = securityScenarioClient ?? throw new ArgumentNullException(nameof(securityScenarioClient));
        _workforceScenarioClient = workforceScenarioClient ?? throw new ArgumentNullException(nameof(workforceScenarioClient));
        _commandCenterScenarioClient = commandCenterScenarioClient ?? throw new ArgumentNullException(nameof(commandCenterScenarioClient));
        _scenarioCatalog = scenarioCatalog ?? throw new ArgumentNullException(nameof(scenarioCatalog));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var descriptor = _scenarioCatalog.GetDescriptor(ScenarioId.NormalOperation);
        _currentScenario = new ScenarioStatus(
            descriptor.Id,
            descriptor.Name,
            descriptor.Description,
            _timeProvider.GetUtcNow(),
            "startup");
    }

    /// <summary>
    /// Gets the scenario currently considered authoritative by the scenario coordinator.
    /// </summary>
    /// <returns>The current scenario.</returns>
    public ScenarioStatus GetCurrentScenario()
    {
        lock (_gate)
        {
            return _currentScenario;
        }
    }

    /// <summary>
    /// Resets all deterministic services back to the Normal Operation scenario.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the reset request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The reset result.</returns>
    public Task<ScenarioApplicationResult> ResetAsync(string correlationId, CancellationToken cancellationToken) =>
        ApplyAsync(ScenarioId.NormalOperation, correlationId, cancellationToken);

    /// <summary>
    /// Applies the supplied deterministic scenario across all Stage 0 services.
    /// </summary>
    /// <param name="scenarioId">The scenario identifier to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The scenario application result.</returns>
    public async Task<ScenarioApplicationResult> ApplyAsync(ScenarioId scenarioId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var recipe = _scenarioCatalog.GetRecipe(scenarioId);
        await _applicationLock.WaitAsync(cancellationToken);

        try
        {
            var applyingStatus = new ScenarioStatus(
                recipe.Descriptor.Id,
                recipe.Descriptor.Name,
                recipe.Descriptor.Description,
                _timeProvider.GetUtcNow(),
                correlationId,
                ScenarioApplicationStatus.Applying);

            SetCurrentScenario(applyingStatus);
            ScenarioCoordinatorLog.ApplicationStarted(_logger, recipe.Descriptor.Name, correlationId);

            await _smartpoleScenarioClient.ResetAsync(correlationId, cancellationToken);
            await _smartpoleScenarioClient.ApplyScenarioAsync(recipe.SmartPoleState, correlationId, cancellationToken);

            await _energyScenarioClient.ResetAsync(correlationId, cancellationToken);
            await _energyScenarioClient.ApplyScenarioAsync(recipe.EnergyState, correlationId, cancellationToken);

            // The Security domain is synchronized from the same recipe, so after a successful
            // application the two boundaries agree about whether an operation is active. The
            // synchronization is sequential and not transactional: a failure part-way leaves the
            // earlier boundaries on the new scenario, and the presenter re-applies.
            await _securityScenarioClient.ResetAsync(correlationId, cancellationToken);

            if (recipe.SecurityState is { } securityState)
            {
                await _securityScenarioClient.ApplyScenarioAsync(securityState, correlationId, cancellationToken);
            }

            // The workforce domain holds no scenario state of its own, only its fixture. Resetting
            // it here is what keeps one demo's walk from being visible in the next one's consult.
            await _workforceScenarioClient.ResetAsync(correlationId, cancellationToken);

            await _commandCenterScenarioClient.ResetAsync(correlationId, cancellationToken);

            var status = new ScenarioStatus(
                recipe.Descriptor.Id,
                recipe.Descriptor.Name,
                recipe.Descriptor.Description,
                _timeProvider.GetUtcNow(),
                correlationId,
                ScenarioApplicationStatus.Applied);

            await _commandCenterScenarioClient.ApplyScenarioAsync(
                new CommandCenterScenarioContext(status, recipe.CustomerReport, recipe.OpenIncident, recipe.Activity),
                correlationId,
                cancellationToken);

            SetCurrentScenario(status);

            ScenarioCoordinatorLog.ApplicationCompleted(_logger, recipe.Descriptor.Name, recipe.OpenIncident?.Id ?? "none", correlationId);
            return new ScenarioApplicationResult(status, recipe.ApplicationSummary);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            SetFailedScenario(recipe.Descriptor, correlationId, "Scenario application was canceled before all deterministic services were synchronized.");
            ScenarioCoordinatorLog.ApplicationCanceled(_logger, recipe.Descriptor.Name, correlationId, exception);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            SetFailedScenario(recipe.Descriptor, correlationId, "Scenario application timed out before all deterministic services were synchronized.");
            ScenarioCoordinatorLog.ApplicationTimedOut(_logger, recipe.Descriptor.Name, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception)
        {
            SetFailedScenario(recipe.Descriptor, correlationId, "A downstream deterministic service could not complete the scenario application.");
            ScenarioCoordinatorLog.ApplicationFailed(_logger, recipe.Descriptor.Name, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            SetFailedScenario(recipe.Descriptor, correlationId, "A downstream deterministic service returned an invalid response.");
            ScenarioCoordinatorLog.ApplicationInvalid(_logger, recipe.Descriptor.Name, correlationId, exception);
            throw;
        }
        finally
        {
            _applicationLock.Release();
        }
    }

    private void SetCurrentScenario(ScenarioStatus status)
    {
        lock (_gate)
        {
            _currentScenario = status;
        }
    }

    private void SetFailedScenario(ScenarioDescriptor descriptor, string correlationId, string failureSummary) =>
        SetCurrentScenario(new ScenarioStatus(
            descriptor.Id,
            descriptor.Name,
            descriptor.Description,
            _timeProvider.GetUtcNow(),
            correlationId,
            ScenarioApplicationStatus.Failed,
            failureSummary));

    /// <summary>
    /// Releases the synchronization primitive used to serialize scenario applications.
    /// </summary>
    public void Dispose() => _applicationLock.Dispose();
}

internal static partial class ScenarioCoordinatorLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Applying deterministic scenario {ScenarioName}. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplicationStarted(ILogger logger, string scenarioName, string correlationId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Deterministic scenario {ScenarioName} applied successfully. OpenIncidentId: {IncidentId}. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplicationCompleted(ILogger logger, string scenarioName, string incidentId, string correlationId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Deterministic scenario {ScenarioName} was canceled. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplicationCanceled(ILogger logger, string scenarioName, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Deterministic scenario {ScenarioName} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplicationTimedOut(ILogger logger, string scenarioName, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Error,
        Message = "Deterministic scenario {ScenarioName} failed because a downstream HTTP request did not complete successfully. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplicationFailed(ILogger logger, string scenarioName, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Error,
        Message = "Deterministic scenario {ScenarioName} failed because a downstream response was invalid. CorrelationId: {CorrelationId}.")]
    internal static partial void ApplicationInvalid(ILogger logger, string scenarioName, string correlationId, Exception exception);
}
