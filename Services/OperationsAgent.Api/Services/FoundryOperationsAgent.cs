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
        Do not invent operational facts. If the available tool cannot answer the question, say so clearly.
        """;

    private readonly AIProjectClient _projectClient = projectClient ?? throw new ArgumentNullException(nameof(projectClient));
    private readonly IEnergyReadGateway _energyReadGateway = energyReadGateway ?? throw new ArgumentNullException(nameof(energyReadGateway));
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

        #region H08_S13_AGENT
        DemoBreakpoints.Pause(DemoSnippets.AgentCreation);

        AIAgent agent = _projectClient.AsAIAgent(
            model: _modelDeploymentName,
            name: _agentName,
            instructions: Instructions,
            tools:
            [
                AIFunctionFactory.Create(
                    energyTools.GetStreetlightStateAsync,
                    EnergyTools.StreetlightStateToolName,
                    "Gets the current authoritative operational state of a streetlight.")
            ],
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
            var response = await agent.RunAsync(question, cancellationToken: timeoutSource.Token);
            OperationsAgentLog.RequestCompleted(_logger, correlationId);

            IReadOnlyList<OperationsAgentToolCall> toolCalls = modelFlightRecorder is null
                ? []
                : [.. modelFlightRecorder.ToolCalls.Select(call => new OperationsAgentToolCall(call.ToolName, call.Arguments))];

            return new OperationsAgentAnswer(response.Text, toolCalls, modelFlightRecorder?.Exchanges.Count ?? 0);
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
