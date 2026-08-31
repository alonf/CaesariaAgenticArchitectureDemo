using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Hosts the general Caesarea Operations Agent using Microsoft Foundry and one read-only Energy Hub tool.
/// </summary>
public sealed partial class FoundryOperationsAgent(
    AIProjectClient projectClient,
    IEnergyReadGateway energyReadGateway,
    AgentSessionStore sessionStore,
    IWorkKnowledgeSearch workKnowledgeSearch,
    ICaseMemoryStore caseMemoryStore,
    DemoStageGate stageGate,
    ToolSourceSwitch toolSourceSwitch,
    PendingApprovalStore pendingApprovalStore,
    IHttpClientFactory httpClientFactory,
    Uri mcpEndpoint,
    string? skillsDirectory,
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
    private readonly ICaseMemoryStore _caseMemoryStore = caseMemoryStore ?? throw new ArgumentNullException(nameof(caseMemoryStore));
    private readonly ToolSourceSwitch _toolSourceSwitch = toolSourceSwitch ?? throw new ArgumentNullException(nameof(toolSourceSwitch));
    private readonly PendingApprovalStore _pendingApprovalStore = pendingApprovalStore ?? throw new ArgumentNullException(nameof(pendingApprovalStore));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly Uri _mcpEndpoint = mcpEndpoint ?? throw new ArgumentNullException(nameof(mcpEndpoint));
    private readonly string? _skillsDirectory = skillsDirectory;
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

        // One stage snapshot drives every capability decision in this request, so a stage change
        // mid-composition can never produce a mixed capability set.
        var currentStage = _stageGate.GetCurrent().Id;

        #region KNOWLEDGE_RETRIEVAL
        DemoBreakpoints.Pause(DemoSnippets.Knowledge);

        TextSearchProvider? workKnowledge = null;

        // Retrieval trace for the UI: what the search returned, which is not the same claim as
        // what the agent cited. Tool invocations run sequentially, so a plain list is safe.
        List<WorkEvidence> retrievedEvidence = [];

        if (currentStage >= DemoStage.Knowledge)
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

        #region CASE_MEMORY
        DemoBreakpoints.Pause(DemoSnippets.CaseMemory);

        CaseMemoryProvider? caseMemory = null;

        // Hypothesis trace for the UI: which closed cases the provider recalled this run.
        List<ClosedCase> recalledCases = [];

        if (currentStage >= DemoStage.Memory)
        {
            caseMemory = new CaseMemoryProvider(
                _caseMemoryStore,
                recalled => recalledCases.AddRange(recalled),
                correlationId,
                _loggerFactory.CreateLogger<CaseMemoryProvider>());
        }
        #endregion

        #region AGENT_SKILLS
        DemoBreakpoints.Pause(DemoSnippets.Skills);

        AgentSkillsProvider? skills = null;
        IReadOnlyList<SkillDescriptor> advertisedSkills = [];

        if (currentStage >= DemoStage.Skills && _skillsDirectory is not null)
        {
            // Progressive disclosure: skill names/descriptions are advertised in the system
            // prompt; the model loads a full procedure on demand through the load_skill tool.
            // Approval for load_skill is disabled here and returns in the ToolApproval stage.
            skills = new AgentSkillsProvider(
                _skillsDirectory,
                options: new AgentSkillsProviderOptions { DisableLoadSkillApproval = true },
                loggerFactory: _loggerFactory);
        }
        #endregion

        #region MCP_CLIENT
        DemoBreakpoints.Pause(DemoSnippets.McpClient);

        // Same capability, presenter-selected boundary: the streetlight tool is either the local
        // function compiled into this service, or discovered at runtime from the Energy Hub's MCP
        // server - McpClientTool IS an AIFunction, so everything downstream cannot tell them apart.
        var toolSource = currentStage >= DemoStage.McpTools
            ? _toolSourceSwitch.Current
            : OperationsAgentToolSource.Local;
        McpClient? mcpClient = null;
        List<AITool> agentTools = [];

        if (toolSource == OperationsAgentToolSource.Mcp)
        {
            var mcpHttpClient = _httpClientFactory.CreateClient("energyhub-mcp");
            mcpHttpClient.DefaultRequestHeaders.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

            // When a remote tool pauses input-required (MRTR), this handler carries the question
            // to the operator and the paused call resumes with the answer - the tool produces no
            // side effect until then.
            var mcpOptions = new McpClientOptions
            {
                Handlers = new McpClientHandlers
                {
                    ElicitationHandler = async (elicitation, elicitationCancellation) =>
                    {
                        var (_, decision) = _pendingApprovalStore.Create(
                            elicitation?.Message ?? "A remote tool requests operator approval.",
                            correlationId,
                            elicitationCancellation);
                        var approved = await decision;
                        return new ElicitResult
                        {
                            Action = "accept",
                            Content = new Dictionary<string, JsonElement>
                            {
                                ["approved"] = JsonSerializer.SerializeToElement(approved)
                            }
                        };
                    }
                }
            };

            mcpClient = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions { Endpoint = _mcpEndpoint }, mcpHttpClient, _loggerFactory, ownsHttpClient: true),
                mcpOptions,
                loggerFactory: _loggerFactory,
                cancellationToken: cancellationToken);
            var discoveredTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            agentTools.Add(discoveredTools.Single(tool => tool.Name == EnergyTools.StreetlightStateToolName));

            // The write tool joins only at the InteractiveInput stage - and only over MCP, where
            // the MRTR approval pause guards it.
            if (currentStage >= DemoStage.InteractiveInput)
            {
                agentTools.Add(discoveredTools.Single(tool => tool.Name == OperationsAgentToolNames.RestoreScheduledMode));
            }
        }
        else
        {
            agentTools.Add(AIFunctionFactory.Create(
                energyTools.GetStreetlightStateAsync,
                EnergyTools.StreetlightStateToolName,
                "Gets the current authoritative operational state of a streetlight."));
        }
        #endregion

        List<AIContextProvider> contextProviders = [];

        if (workKnowledge is not null)
        {
            contextProviders.Add(workKnowledge);
        }

        if (caseMemory is not null)
        {
            contextProviders.Add(caseMemory);
        }

        if (skills is not null)
        {
            contextProviders.Add(skills);
        }

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
                    Tools = [.. agentTools]
                },
                // Capabilities join as context providers: knowledge retrieval contributes an
                // on-demand search tool; case memory contributes trusted hypothesis rules plus
                // recalled cases as separate untrusted reference data.
                AIContextProviders = contextProviders.Count > 0 ? contextProviders : null
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

        if (skills is not null)
        {
            // Snapshot the advertised skills before the run - through the SDK's own discovery, so
            // the response reports exactly what the provider would advertise for THIS request even
            // if the presenter edits the files while the model works.
            advertisedSkills = await SkillCatalog.DescribeAsync(_skillsDirectory, agent, _loggerFactory, cancellationToken);
        }

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
            else if (_sessionStore.TryGetState(sessionId, currentStage, out var storedState))
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
            var resolvedSessionId = _sessionStore.SaveState(sessionId, serializedSession, currentStage);
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

            IReadOnlyList<OperationsAgentRecalledCase> recalled =
            [
                .. recalledCases
                    .DistinctBy(item => item.CaseId, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new OperationsAgentRecalledCase(
                        item.CaseId, item.AssetId, item.Symptom, item.Resolution, item.ClosedAt))
            ];

            // Skills trace from the pre-run snapshot. A skill counts as loaded only when a
            // load_skill call named it exactly (the SDK performs an exact lookup) AND the pipeline
            // executed the call and returned a result to the model.
            IReadOnlyList<OperationsAgentSkill> skillTrace =
            [
                .. advertisedSkills.Select(skill => new OperationsAgentSkill(
                    skill.Name,
                    skill.Description,
                    modelFlightRecorder is not null && modelFlightRecorder.ToolCalls.Any(call =>
                        call.ToolName == OperationsAgentToolNames.LoadSkill
                        && SkillCatalog.IsLoadSkillCallFor(call.Arguments, skill.Name)
                        && modelFlightRecorder.HasResult(call.CallId))))
            ];

            return new OperationsAgentAnswer(response.Text, resolvedSessionId, toolCalls, evidence, recalled, skillTrace, toolSource, modelFlightRecorder?.Exchanges.Count ?? 0);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OperationsAgentTimedOutException(
                $"The Operations Agent request exceeded its {_requestTimeout.TotalSeconds:0}-second execution budget.",
                exception);
        }
        finally
        {
            // Per-request resources are disposed with the request: the skills provider owns its
            // source pipeline, and the MCP client owns its transport (and, via ownsHttpClient,
            // the HTTP client the transport used).
            skills?.Dispose();

            if (mcpClient is not null)
            {
                await mcpClient.DisposeAsync();
            }
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
