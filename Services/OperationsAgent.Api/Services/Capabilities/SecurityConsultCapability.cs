using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// A second agent, not a second tool: Security owns records this service may not read, so the
/// question crosses a boundary and comes back as a judgment. The relationship is delegation - the
/// Operations Agent keeps ownership of the answer it gives the operator - and what it reports is
/// the specialist's identity and its sanitized verdict, not a line of generic tool-result text.
/// </summary>
internal sealed class SecurityConsultCapability(
    SecurityConsultSwitch securityConsult,
    IHttpClientFactory httpClientFactory,
    Uri securityAgentEndpoint,
    ILoggerFactory loggerFactory,
    ILogger logger) : AgentCapability
{
    private const string SecurityAgentSourceName = "Security Operations Agent";

    private McpClient? _securityMcpClient;
    private HttpClientTransport? _securityTransport;

    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.MultiAgent && securityConsult.Enabled;

    public override async ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        var securityHttpClient = httpClientFactory.CreateClient("securityagent-mcp");
        securityHttpClient.DefaultRequestHeaders.Add(CorrelationHeaderNames.XCorrelationId, composition.CorrelationId);

        _securityTransport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = securityAgentEndpoint },
            securityHttpClient,
            loggerFactory,
            ownsHttpClient: true);

        try
        {
            _securityMcpClient = await McpClient.CreateAsync(
                _securityTransport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);
            var securityTools = await _securityMcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            composition.Tools.Add(FindDiscoveredTool(securityTools, OperationsAgentToolNames.AssessLightingRequirement, SecurityAgentSourceName));
        }
        catch (McpException exception)
        {
            throw new OperationsAgentToolUnavailableException(
                $"The Security Operations Agent could not be consulted: {exception.Message}", exception);
        }
    }

    public override void Describe(AgentRunTrace trace, OperationsAgentAnswerParts parts)
    {
        if (trace.Recorder is null)
        {
            return;
        }

        parts.Delegations.AddRange(AgentTraceProjection.DescribeDelegations(
            trace.Recorder,
            trace.ApprovalDecisions,
            SecurityAgentSourceName,
            (toolName, exception) => OperationsAgentLog.DelegationTraceUnreadable(logger, toolName, exception)));
    }

    public override async ValueTask DisposeAsync()
    {
        if (_securityMcpClient is not null)
        {
            await _securityMcpClient.DisposeAsync();
        }

        if (_securityTransport is not null)
        {
            await _securityTransport.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
