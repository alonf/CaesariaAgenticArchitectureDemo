namespace OperationsAgent.Api.Services;

/// <summary>
/// Orchestrates a single read-only Operations Agent investigation: it builds a per-request evidence recorder and
/// toolset, asks the configured <see cref="IInvestigationAgentRunner"/> to reason over the available evidence, and
/// assembles the public <see cref="InvestigationResult"/> contract from the strict model response and the recorded
/// tool trace. This service never writes, restores, commands, applies a scenario, or reaches the vendor/device
/// simulator layer directly.
/// </summary>
public sealed partial class InvestigationService
{
    private readonly IEnergyReadGateway _energyReadGateway;
    private readonly ICommandCenterReadGateway _commandCenterReadGateway;
    private readonly IInvestigationAgentRunner _agentRunner;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _agentName;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InvestigationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvestigationService"/> class.
    /// </summary>
    /// <param name="energyReadGateway">The read-only gateway used to reach the Energy Hub.</param>
    /// <param name="commandCenterReadGateway">The read-only gateway used to reach the Command Center.</param>
    /// <param name="agentRunner">The runner used to perform the model reasoning step.</param>
    /// <param name="loggerFactory">The factory used to create the per-request toolset logger.</param>
    /// <param name="agentName">The projector-friendly Operations Agent identity name.</param>
    /// <param name="timeProvider">The clock used to stamp investigation start and completion times.</param>
    /// <param name="logger">The logger used for investigation orchestration events.</param>
    public InvestigationService(
        IEnergyReadGateway energyReadGateway,
        ICommandCenterReadGateway commandCenterReadGateway,
        IInvestigationAgentRunner agentRunner,
        ILoggerFactory loggerFactory,
        string agentName,
        TimeProvider timeProvider,
        ILogger<InvestigationService> logger)
    {
        _energyReadGateway = energyReadGateway ?? throw new ArgumentNullException(nameof(energyReadGateway));
        _commandCenterReadGateway = commandCenterReadGateway ?? throw new ArgumentNullException(nameof(commandCenterReadGateway));
        _agentRunner = agentRunner ?? throw new ArgumentNullException(nameof(agentRunner));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        _agentName = agentName;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs a read-only investigation for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to investigate.</param>
    /// <param name="correlationId">The correlation identifier spanning the investigation request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The structured, evidence-grounded investigation result.</returns>
    public async Task<InvestigationResult> InvestigateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var startedAt = _timeProvider.GetUtcNow();
        InvestigationServiceLog.InvestigationStarted(_logger, assetId, correlationId);

        var recorder = new InvestigationEvidenceRecorder(_timeProvider);
        var toolset = new OperationsToolset(
            assetId,
            correlationId,
            _energyReadGateway,
            _commandCenterReadGateway,
            recorder,
            _loggerFactory.CreateLogger<OperationsToolset>());

        var question =
            $"Investigate asset {assetId}. Determine whether it is currently operating as expected and explain " +
            "any anomaly using only the evidence you can verify with your tools.";

        var modelResponse = await _agentRunner.InvestigateAsync(toolset, assetId, question, cancellationToken);
        var completedAt = _timeProvider.GetUtcNow();
        var trace = recorder.GetTrace();
        var successfulTools = trace
            .Where(entry => entry.Succeeded)
            .Select(entry => entry.ToolName)
            .ToHashSet(StringComparer.Ordinal);

        if (!successfulTools.Contains(OperationsToolset.EnergyAssetStateToolName))
        {
            throw new InvestigationEvidenceUnavailableException(
                "The Operations Agent could not obtain the authoritative Energy Hub asset state.");
        }

        var unsupportedFact = modelResponse.VerifiedFacts.FirstOrDefault(fact => !successfulTools.Contains(fact.Source));
        if (unsupportedFact is not null)
        {
            throw new InvestigationResponseFormatException(
                $"Verified fact source '{unsupportedFact.Source}' did not produce successful evidence during this investigation.");
        }

        InvestigationServiceLog.InvestigationCompleted(_logger, assetId, correlationId, trace.Count);

        return new InvestigationResult(
            assetId,
            _agentName,
            InvestigationStatus.Completed,
            modelResponse.VerifiedFacts.Select(fact => new VerifiedFact(fact.Statement, fact.Source)).ToArray(),
            modelResponse.Hypotheses.Select(hypothesis => new Hypothesis(hypothesis.Statement, hypothesis.Confidence, hypothesis.Reason)).ToArray(),
            modelResponse.MissingEvidence.ToArray(),
            trace,
            modelResponse.Summary,
            correlationId,
            startedAt,
            completedAt);
    }
}

internal static partial class InvestigationServiceLog
{
    [LoggerMessage(
        EventId = 2350,
        Level = LogLevel.Information,
        Message = "Operations Agent investigation started for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void InvestigationStarted(ILogger logger, string assetId, string correlationId);

    [LoggerMessage(
        EventId = 2351,
        Level = LogLevel.Information,
        Message = "Operations Agent investigation completed for asset {AssetId} with {ToolCallCount} recorded tool calls. CorrelationId: {CorrelationId}.")]
    internal static partial void InvestigationCompleted(ILogger logger, string assetId, string correlationId, int toolCallCount);
}
