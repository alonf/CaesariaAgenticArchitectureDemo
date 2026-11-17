using System.Net;
using System.Text.Json;
using Caesarea.ServiceDefaults;
using DurableOperations.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace OperationsAgent.Functions;

/// <summary>Authenticated local rehearsal endpoints. Generated agent HTTP and MCP endpoints are disabled.</summary>
public sealed class RecoveryHttpFunctions
{
    internal const string AgentName = "CaesareaRecovery";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RecoveryAuthorization _authorization;

    /// <summary>Creates the authenticated local endpoint boundary.</summary>
    public RecoveryHttpFunctions(RecoveryAuthorization authorization) => _authorization = authorization;

    /// <summary>Runs or resumes a persisted agent conversation using the operator credential.</summary>
    [Function(nameof(RunRecoveryAgent))]
    public async Task<HttpResponseData> RunRecoveryAgent(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "durable-agent/run")] HttpRequestData request,
        [DurableClient] DurableTaskClient client, FunctionContext context)
    {
        if (!_authorization.IsOperator(Key(request)))
        {
            return request.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var turn = await ReadAsync<DurableAgentTurn>(request);
        if (turn is null || string.IsNullOrWhiteSpace(turn.Message) || turn.Message.Length > 8000)
        {
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        var proxy = client.AsDurableAgentProxy(context, AgentName);
        AgentSession session;
        try
        {
            if (turn.SessionId is not null && !string.Equals(AgentSessionId.Parse(turn.SessionId).Name, AgentName, StringComparison.OrdinalIgnoreCase))
            {
                return request.CreateResponse(HttpStatusCode.BadRequest);
            }

            session = turn.SessionId is null ? await proxy.CreateSessionAsync(CancellationToken.None)
                : await proxy.DeserializeSessionAsync(JsonSerializer.SerializeToElement(new { sessionId = turn.SessionId }), cancellationToken: CancellationToken.None);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException)
        {
            return request.CreateResponse(HttpStatusCode.BadRequest);
        }

        // The durable proxy rejects cancellable tokens. A caller disconnect does not cancel
        // the persisted turn; request outcome retention is a separate lifecycle.
        var result = await proxy.RunAsync(turn.Message, session, cancellationToken: CancellationToken.None);
        return await JsonAsync(request, HttpStatusCode.OK, new DurableAgentReply(result.Text, session.ToString()!));
    }

    /// <summary>Retrieves authoritative process status for the local presenter.</summary>
    [Function(nameof(GetRecoveryOperation))]
    public async Task<HttpResponseData> GetRecoveryOperation(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "durable-operations/{instanceId}")] HttpRequestData request,
        string instanceId, [DurableClient] DurableTaskClient client)
    {
        if (!_authorization.IsOperator(Key(request)) && !_authorization.IsSupervisor(Key(request)))
        {
            return request.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var status = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);
        return status is null ? request.CreateResponse(HttpStatusCode.NotFound) : await JsonAsync(request, HttpStatusCode.OK, new
        {
            instanceId,
            runtimeStatus = status.RuntimeStatus.ToString(),
            progress = status.ReadCustomStatusAs<RecoveryProgress>(),
            outcome = status.SerializedOutput
        });
    }

    /// <summary>Authenticates the supervisor and signs the exact displayed review before raising the durable event.</summary>
    [Function(nameof(DecideRecovery))]
    public async Task<HttpResponseData> DecideRecovery(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "durable-operations/{instanceId}/decision")] HttpRequestData request,
        string instanceId, [DurableClient] DurableTaskClient client)
    {
        if (!_authorization.IsSupervisor(Key(request)))
        {
            return request.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var decision = await ReadAsync<RecoveryDecisionRequest>(request);
        var status = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);
        var progress = status?.ReadCustomStatusAs<RecoveryProgress>();
        var now = DateTimeOffset.UtcNow;
        if (decision?.Review is null || decision.Review.InstanceId != instanceId
            || status?.RuntimeStatus != OrchestrationRuntimeStatus.Running
            || progress is not { State: "AwaitingApproval", Review: not null }
            || decision.Review != progress.Review || now >= decision.Review.ExpiresAt)
        {
            return request.CreateResponse(HttpStatusCode.Conflict);
        }

        await client.RaiseEventAsync(instanceId, RecoveryOrchestration.DecisionEvent, _authorization.Sign(decision, now));
        // A queued decision is not a command result. The single durable wait consumes the first
        // event; duplicate submissions cannot start another execution activity.
        return await JsonAsync(request, HttpStatusCode.Accepted, new { instanceId, state = "DecisionQueued" });
    }

    private static string? Key(HttpRequestData request) => request.Headers.TryGetValues("X-Caesarea-Key", out var values) ? values.SingleOrDefault() : null;

    private static async Task<T?> ReadAsync<T>(HttpRequestData request)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static async Task<HttpResponseData> JsonAsync(HttpRequestData request, HttpStatusCode status, object body)
    {
        var response = request.CreateResponse(status);
        response.Headers.Add(CorrelationHeaderNames.XCorrelationId, request.FunctionContext.InvocationId);
        await response.WriteAsJsonAsync(body);
        return response;
    }
}
