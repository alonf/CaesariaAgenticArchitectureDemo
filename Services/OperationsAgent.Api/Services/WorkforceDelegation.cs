using A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;
using Microsoft.Extensions.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Consults the Caesarea Workforce Agent across a service boundary, over A2A.
/// <para>
/// Note what this is not. It is not an <c>AIFunction</c>, it is not in the model's tool list, and
/// the model does not choose it: the Operations Agent owns the investigation and decides to give a
/// piece of it to a peer. What comes back is that peer's answer, which then becomes context for
/// the answer this service composes for the operator - ownership never moves.
/// </para>
/// <para>
/// The card is resolved first, because that is what makes this a relationship with an agent rather
/// than a call to an endpoint: the card names who is being consulted, who runs them, and what they
/// will and will not answer.
/// </para>
/// </summary>
public sealed partial class WorkforceDelegation(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<WorkforceDelegation> logger)
{
    /// <summary>The named client addressing the Workforce Agent.</summary>
    public const string HttpClientName = "workforceagent-a2a";

    // The delegated task is a peer's whole run - search, choose, read, compose - so it is given
    // room, but never unbounded: a peer that stops answering must not hold the operator's turn.
    private static readonly TimeSpan TaskBudget = TimeSpan.FromSeconds(90);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    private readonly ILogger<WorkforceDelegation> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gives the workforce domain's agent a task and returns its answer with the card it published.
    /// </summary>
    /// <param name="question">The question to delegate.</param>
    /// <param name="correlationId">The correlation identifier spanning the operator's request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The consultation, or a failed one carrying why the peer could not be consulted.</returns>
    public async Task<OperationsAgentRemoteConsult> ConsultAsync(
        string question, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TaskBudget);

        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            #region A2A_DELEGATION
            DemoBreakpoints.Pause(DemoSnippets.A2ADelegation);

            // Discovery first: read the card at the peer's well-known location, so the consult is
            // addressed to a named agent with declared skills rather than to a bare URL.
            var resolver = new A2ACardResolver(
                httpClient.BaseAddress!, httpClient, logger: _loggerFactory.CreateLogger<A2ACardResolver>());
            var card = await resolver.GetAgentCardAsync(budget.Token);

            // The card chooses the transport: the factory builds the client for the binding the
            // peer declared, so the address and the protocol both come from discovery rather than
            // from an assumption compiled in here. The peer is then an agent this service can run,
            // not a capability it can call.
            var remoteAgent = new A2AAgent(
                A2AClientFactory.Create(card, httpClient, new A2AClientOptions()),
                new A2AAgentOptions { Name = card.Name, Description = card.Description },
                _loggerFactory);

            var session = await remoteAgent.CreateSessionAsync(budget.Token);
            var reply = await remoteAgent.RunAsync(
                new ChatMessage(ChatRole.User, question), session, cancellationToken: budget.Token);
            #endregion

            WorkforceDelegationLog.Consulted(_logger, card.Name, correlationId);

            return new OperationsAgentRemoteConsult(
                card.Name,
                card.Provider?.Organization ?? "unstated",
                card.Skills.Count > 0 ? card.Skills[0].Id : "unstated",
                $"A2A ({card.SupportedInterfaces[0].ProtocolBinding})",
                question,
                reply.Text,
                Failure: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or A2AException or OperationCanceledException or InvalidOperationException or ArgumentException)
        {
            // A peer in another process can be down, slow or refusing, and the operator is told so
            // rather than being given an answer that quietly lacks the specialist's contribution.
            var reason = exception is OperationCanceledException
                ? $"The workforce domain did not answer within {TaskBudget.TotalSeconds:0} seconds."
                : exception.Message;

            WorkforceDelegationLog.Failed(_logger, correlationId, exception);
            return new OperationsAgentRemoteConsult(
                // No card was read, so nothing here is claimed as discovered - not the provider,
                // not the skill, and not the binding the peer would have declared.
                "Caesarea Workforce Agent", "unreachable", "unstated", "A2A", question, Answer: string.Empty, Failure: reason);
        }
    }
}

internal static partial class WorkforceDelegationLog
{
    [LoggerMessage(
        EventId = 2490,
        Level = LogLevel.Information,
        Message = "Delegated a task to {AgentName} over A2A and received its answer. CorrelationId: {CorrelationId}.")]
    internal static partial void Consulted(ILogger logger, string agentName, string correlationId);

    [LoggerMessage(
        EventId = 2491,
        Level = LogLevel.Warning,
        Message = "The workforce domain could not be consulted. CorrelationId: {CorrelationId}.")]
    internal static partial void Failed(ILogger logger, string correlationId, Exception exception);
}
