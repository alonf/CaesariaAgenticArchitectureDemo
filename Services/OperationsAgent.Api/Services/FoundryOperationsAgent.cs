using System.Diagnostics;
using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Hosts the general Caesarea Operations Agent using Microsoft Foundry and one read-only Energy Hub tool.
/// </summary>
public sealed partial class FoundryOperationsAgent(
    AIProjectClient projectClient,
    IEnergyReadGateway energyReadGateway,
    AgentSessionStore sessionStore,
    IWorkKnowledgeSearch workKnowledgeSearch,
    DemoStageGate stageGate,
    string modelDeploymentName,
    string agentName,
    int maxFunctionIterations,
    TimeSpan requestTimeout,
    ILoggerFactory loggerFactory,
    ILogger<FoundryOperationsAgent> logger) : IOperationsAgent
{
    private const string Instructions = """
        You are the Caesarea Operations Agent for the city Command & Control center.
        Answer operator questions using the tools available to you.
        Use the authoritative Energy Hub tool whenever current streetlight state is needed.
        When asked why an operational state exists and a work-knowledge search capability is
        available, search it for maintenance or override evidence and cite the evidence identifiers
        you used. If no evidence exists, say so; never invent work orders or notes.
        Do not invent operational facts. If the available tools cannot answer the question, say so clearly.
        """;

    private readonly AIProjectClient _projectClient = projectClient ?? throw new ArgumentNullException(nameof(projectClient));
    private readonly IEnergyReadGateway _energyReadGateway = energyReadGateway ?? throw new ArgumentNullException(nameof(energyReadGateway));
    private readonly AgentSessionStore _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    private readonly IWorkKnowledgeSearch _workKnowledgeSearch = workKnowledgeSearch ?? throw new ArgumentNullException(nameof(workKnowledgeSearch));
    private readonly DemoStageGate _stageGate = stageGate ?? throw new ArgumentNullException(nameof(stageGate));
    private readonly string _modelDeploymentName = string.IsNullOrWhiteSpace(modelDeploymentName)
        ? throw new ArgumentException("A model deployment name is required.", nameof(modelDeploymentName))
        : modelDeploymentName;
    private readonly string _agentName = string.IsNullOrWhiteSpace(agentName)
        ? throw new ArgumentException("An agent name is required.", nameof(agentName))
        : agentName;
    private readonly int _maxFunctionIterations = maxFunctionIterations >= 2
        ? maxFunctionIterations
        : throw new ArgumentOutOfRangeException(nameof(maxFunctionIterations));
    private readonly TimeSpan _requestTimeout = requestTimeout >= TimeSpan.FromSeconds(1)
        ? requestTimeout
        : throw new ArgumentOutOfRangeException(nameof(requestTimeout));
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly ILogger<FoundryOperationsAgent> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<OperationsAgentAnswer> AskAsync(
        string question,
        string? sessionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var energyTools = new EnergyTools(
            _energyReadGateway,
            correlationId,
            _loggerFactory.CreateLogger<EnergyTools>());

        // Inspect modelFlightRecorder.Exchanges in the debugger to see every model round trip.
        ModelExchangeRecorder? modelFlightRecorder = null;

        #region KNOWLEDGE_RETRIEVAL
        DemoBreakpoints.Pause(DemoSnippets.Knowledge);

        TextSearchProvider? workKnowledge = null;

        // Retrieval trace for the UI: what the search returned, which is not the same claim as
        // what the agent cited. Tool invocations run sequentially, so a plain list is safe.
        List<WorkEvidence> retrievedEvidence = [];

        if (_stageGate.GetCurrent().Id >= DemoStage.Knowledge)
        {
            workKnowledge = new TextSearchProvider(
                async (query, searchCancellationToken) =>
                {
                    var evidence = await _workKnowledgeSearch.SearchAsync(query, correlationId, searchCancellationToken);
                    retrievedEvidence.AddRange(evidence);
                    return evidence.Select(item => new TextSearchProvider.TextSearchResult
                    {
                        SourceName = $"{item.SourceType} {item.Id} ({item.SourceLabel})",
                        Text = $"{item.Title} - {item.Summary} (recorded {item.OccurredAt:u})"
                    });
                },
                new TextSearchProviderOptions
                {
                    SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
                    FunctionToolName = OperationsAgentToolNames.SearchWorkKnowledge,
                    FunctionToolDescription =
                        "Searches organizational work knowledge such as work orders, technician notes, and maintenance records."
                },
                _loggerFactory);
        }
        #endregion

        #region AGENT_CREATION
        DemoBreakpoints.Pause(DemoSnippets.AgentCreation);

        AIAgent agent = _projectClient.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                Name = _agentName,
                ChatOptions = new()
                {
                    ModelId = _modelDeploymentName,
                    Instructions = Instructions,
                    Tools =
                    [
                        AIFunctionFactory.Create(
                            energyTools.GetStreetlightStateAsync,
                            EnergyTools.StreetlightStateToolName,
                            "Gets the current authoritative operational state of a streetlight.")
                    ]
                },
                // Knowledge retrieval joins as a context provider: it contributes the on-demand
                // search tool that the function-invoking pipeline can then execute.
                AIContextProviders = workKnowledge is null ? null : [workKnowledge]
            },
            clientFactory: client =>
            {
                modelFlightRecorder = new ModelExchangeRecorder(client);
                return new FunctionInvokingChatClient(modelFlightRecorder, _loggerFactory)
                {
                    MaximumIterationsPerRequest = _maxFunctionIterations,
                    MaximumConsecutiveErrorsPerRequest = 1,
                    AllowConcurrentInvocation = false
                };
            },
            loggerFactory: _loggerFactory);
        #endregion

        OperationsAgentLog.RequestStarted(_logger, _modelDeploymentName, correlationId);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // The execution budget guards against runaway model loops. With a debugger attached the
        // presenter may be single-stepping a demo breakpoint, so the budget is suspended; otherwise
        // a paused human would trip the timeout and abort the in-flight tool call mid-step.
        timeoutSource.CancelAfter(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : _requestTimeout);

        try
        {
            #region AGENT_SESSION
            DemoBreakpoints.Pause(DemoSnippets.Session);

            AgentSession session;

            if (sessionId is null)
            {
                session = await agent.CreateSessionAsync(timeoutSource.Token);
            }
            else if (_sessionStore.TryGetState(sessionId, out var storedState))
            {
                // Each request restores its own private session instance from serialized state, so a
                // live session object is never shared across requests or agent instances.
                session = await agent.DeserializeSessionAsync(storedState, cancellationToken: timeoutSource.Token);
            }
            else
            {
                throw new OperationsAgentSessionExpiredException(sessionId);
            }

            var response = await agent.RunAsync(question, session, cancellationToken: timeoutSource.Token);
            #endregion

            var serializedSession = await agent.SerializeSessionAsync(session, cancellationToken: timeoutSource.Token);
            var resolvedSessionId = _sessionStore.SaveState(sessionId, serializedSession);
            OperationsAgentLog.RequestCompleted(_logger, correlationId);

            IReadOnlyList<OperationsAgentToolCall> toolCalls = modelFlightRecorder is null
                ? []
                : [.. modelFlightRecorder.ToolCalls.Select(call => new OperationsAgentToolCall(call.ToolName, call.Arguments))];

            IReadOnlyList<OperationsAgentEvidence> evidence =
            [
                .. retrievedEvidence
                    .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new OperationsAgentEvidence(
                        item.Id, item.SourceType, item.Title, item.Summary, item.OccurredAt, item.SourceLabel, item.SourceUri))
            ];

            return new OperationsAgentAnswer(response.Text, resolvedSessionId, toolCalls, evidence, modelFlightRecorder?.Exchanges.Count ?? 0);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OperationsAgentTimedOutException(
                $"The Operations Agent request exceeded its {_requestTimeout.TotalSeconds:0}-second execution budget.",
                exception);
        }
    }
}

internal static partial class OperationsAgentLog
{
    [LoggerMessage(
        EventId = 2400,
        Level = LogLevel.Information,
        Message = "Starting Operations Agent request using model {ModelDeploymentName}. CorrelationId: {CorrelationId}.")]
    internal static partial void RequestStarted(ILogger logger, string modelDeploymentName, string correlationId);

    [LoggerMessage(
        EventId = 2401,
        Level = LogLevel.Information,
        Message = "Operations Agent request completed. CorrelationId: {CorrelationId}.")]
    internal static partial void RequestCompleted(ILogger logger, string correlationId);
}
