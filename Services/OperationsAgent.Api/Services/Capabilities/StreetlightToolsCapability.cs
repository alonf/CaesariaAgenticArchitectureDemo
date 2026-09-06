using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// The authoritative streetlight tool, from the presenter-selected boundary: the local function
/// compiled into this service, or the Energy Hub's MCP server discovered at runtime. Over MCP only,
/// it also carries the direct write in its one stage window, guarded by the operator's answer to
/// the tool's own mid-execution question.
/// </summary>
internal sealed class StreetlightToolsCapability(
    IEnergyReadGateway energyReadGateway,
    ToolSourceSwitch toolSourceSwitch,
    IHttpClientFactory httpClientFactory,
    Uri mcpEndpoint,
    PendingApprovalStore pendingApprovalStore,
    DemoStageGate stageGate,
    ILoggerFactory loggerFactory) : AgentCapability
{
    private const string EnergyHubSourceName = "Energy Hub";

    private McpClient? _mcpClient;
    private HttpClientTransport? _mcpTransport;

    // The one constant of every agent stage: the model can always read the city.
    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.InvestigationAgent;

    public override async ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        var correlationId = composition.CorrelationId;
        var currentStage = composition.Stage;

        #region MCP_CLIENT
        DemoBreakpoints.Pause(DemoSnippets.McpClient);

        // Same capability, presenter-selected boundary: the streetlight tool is either the local
        // function compiled into this service, or discovered at runtime from the Energy Hub's MCP
        // server - McpClientTool IS an AIFunction, so everything downstream cannot tell them apart.
        var toolSource = currentStage >= DemoStage.McpTools
            ? toolSourceSwitch.Current
            : OperationsAgentToolSource.Local;
        composition.ToolSource = toolSource;

        if (toolSource == OperationsAgentToolSource.Mcp)
        {
            var mcpHttpClient = httpClientFactory.CreateClient("energyhub-mcp");
            mcpHttpClient.DefaultRequestHeaders.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

            _mcpTransport = new HttpClientTransport(
                new HttpClientTransportOptions { Endpoint = mcpEndpoint },
                mcpHttpClient,
                loggerFactory,
                ownsHttpClient: true);

            // When a remote tool pauses input-required (MRTR), the elicitation handler carries
            // the question to the operator; the paused call resumes with the answer.
            var mcpOptions = new McpClientOptions
            {
                Handlers = new McpClientHandlers
                {
                    ElicitationHandler = CreateOperatorApprovalHandler(correlationId)
                }
            };

            IList<McpClientTool> discoveredTools;

            try
            {
                _mcpClient = await McpClient.CreateAsync(
                    _mcpTransport,
                    mcpOptions,
                    loggerFactory: loggerFactory,
                    cancellationToken: cancellationToken);
                discoveredTools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            }
            catch (McpException exception)
            {
                throw new OperationsAgentToolUnavailableException(
                    $"The Energy Hub MCP server could not be used: {exception.Message}", exception);
            }

            composition.Tools.Add(FindDiscoveredTool(discoveredTools, EnergyTools.StreetlightStateToolName, EnergyHubSourceName));

            // The direct write exists in one stage window only: it joins at InteractiveInput,
            // where the MRTR approval pause guards it, and is withdrawn again at Workflow,
            // where the agent must request the governed operation instead of performing it.
            if (currentStage >= DemoStage.InteractiveInput && currentStage < DemoStage.Workflow)
            {
                composition.Tools.Add(FindDiscoveredTool(discoveredTools, OperationsAgentToolNames.RestoreScheduledMode, EnergyHubSourceName));
            }
        }
        else
        {
            var energyTools = new EnergyTools(energyReadGateway, correlationId, loggerFactory.CreateLogger<EnergyTools>());

            composition.Tools.Add(AIFunctionFactory.Create(
                energyTools.GetStreetlightStateAsync,
                EnergyTools.StreetlightStateToolName,
                "Gets the current authoritative operational state of a streetlight."));
        }
        #endregion
    }

    public override async ValueTask DisposeAsync()
    {
        // The transport is disposed after the client, and owns the HTTP client it was given.
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
        }

        if (_mcpTransport is not null)
        {
            await _mcpTransport.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>
    /// Creates the MRTR elicitation handler that bridges a paused remote tool to the operator:
    /// the question parks in the pending-approval store, the Command Center collects the
    /// decision, and the tool call resumes with it. The tool produces no side effect until then.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the agent run.</param>
    private Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>> CreateOperatorApprovalHandler(string correlationId) =>
        async (elicitation, elicitationCancellation) =>
        {
            var (_, decision) = pendingApprovalStore.Create(
                elicitation?.Message ?? "A remote tool requests operator confirmation.",
                correlationId,
                elicitationCancellation,
                OperationsAgentControlPoint.InteractiveInput,
                OperationsAgentToolNames.RestoreScheduledMode);
            var approved = await decision;

            // The direct write exists in one stage window, so the confirmation is checked against
            // that whole window and not just its floor. Moving forward into Workflow withdraws
            // this capability exactly as moving backward does: the governed operation replaces it,
            // and a confirmation parked beforehand must not be able to perform the write anyway.
            var stageNow = stageGate.GetCurrent().Id;

            if (stageNow < DemoStage.InteractiveInput || stageNow >= DemoStage.Workflow)
            {
                approved = false;
            }

            return new ElicitResult
            {
                Action = "accept",
                Content = new Dictionary<string, JsonElement>
                {
                    ["approved"] = JsonSerializer.SerializeToElement(approved)
                }
            };
        };
}
