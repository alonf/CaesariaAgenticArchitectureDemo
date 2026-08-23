using Azure.AI.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <inheritdoc cref="IInvestigationAgentRunner"/>
/// <remarks>
/// Wraps the Microsoft Foundry project endpoint as an ephemeral Microsoft Agent Framework <see cref="AIAgent"/>.
/// No agent definition is persisted to Microsoft Foundry; the agent's instructions, tools, and model exist only
/// for the lifetime of this call. Authentication and model invocation happen lazily on <see cref="AIAgent.RunAsync(string, AgentSession, AgentRunOptions, CancellationToken)"/>,
/// so a missing or invalid Azure credential surfaces here as a request-time failure rather than blocking service startup.
/// </remarks>
public sealed class FoundryInvestigationAgentRunner : IInvestigationAgentRunner
{
    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    private readonly AIProjectClient _projectClient;
    private readonly string _modelDeploymentName;
    private readonly string _agentName;
    private readonly ILogger<FoundryInvestigationAgentRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FoundryInvestigationAgentRunner"/> class.
    /// </summary>
    /// <param name="projectClient">The Microsoft Foundry project client used to reach the configured project.</param>
    /// <param name="modelDeploymentName">The Microsoft Foundry model deployment used for investigation reasoning.</param>
    /// <param name="agentName">The projector-friendly Operations Agent identity name.</param>
    /// <param name="logger">The logger used for investigation run events.</param>
    public FoundryInvestigationAgentRunner(
        AIProjectClient projectClient,
        string modelDeploymentName,
        string agentName,
        ILogger<FoundryInvestigationAgentRunner> logger)
    {
        _projectClient = projectClient ?? throw new ArgumentNullException(nameof(projectClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDeploymentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        _modelDeploymentName = modelDeploymentName;
        _agentName = agentName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ModelInvestigationResponse> InvestigateAsync(
        OperationsToolset toolset,
        string assetId,
        string question,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolset);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        AITool[] tools =
        [
            AIFunctionFactory.Create(toolset.GetCustomerReportAsync),
            AIFunctionFactory.Create(toolset.GetEnergyAssetStateAsync),
            AIFunctionFactory.Create(toolset.GetEnergyRecentActivityAsync),
            AIFunctionFactory.Create(toolset.GetIncidentContextAsync)
        ];

        FoundryInvestigationAgentRunnerLog.RunStarting(_logger, assetId, _modelDeploymentName);

        AIAgent agent = _projectClient.AsAIAgent(
            model: _modelDeploymentName,
            instructions: InvestigationAgentInstructions.Text,
            name: _agentName,
            tools: tools);

        var runOptions = new AgentRunOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema<ModelInvestigationResponse>(SerializerOptions)
        };

        var response = await agent.RunAsync(question, options: runOptions, cancellationToken: cancellationToken);

        FoundryInvestigationAgentRunnerLog.RunCompleted(_logger, assetId);

        return InvestigationResponseParser.Parse(response.Text);
    }
}

internal static partial class FoundryInvestigationAgentRunnerLog
{
    [LoggerMessage(
        EventId = 2400,
        Level = LogLevel.Information,
        Message = "Starting Operations Agent investigation for asset {AssetId} using model {ModelDeploymentName}.")]
    internal static partial void RunStarting(ILogger logger, string assetId, string modelDeploymentName);

    [LoggerMessage(
        EventId = 2401,
        Level = LogLevel.Information,
        Message = "Operations Agent investigation for asset {AssetId} completed.")]
    internal static partial void RunCompleted(ILogger logger, string assetId);
}
