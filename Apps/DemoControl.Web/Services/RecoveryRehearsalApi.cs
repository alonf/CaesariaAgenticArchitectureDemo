using System.Text.Json;
using DurableOperations.Contracts;
using Energy.Contracts;

namespace DemoControl.Web.Services;

/// <summary>The narrow local boundaries used by the presenter rehearsal.</summary>
internal interface IRecoveryRehearsalApi
{
    /// <summary>Checks both city boundaries, prepares the scenario and retains a capable stage.</summary>
    public Task PrepareCityAsync(CancellationToken cancellationToken);
    /// <summary>Allocates an identity before model execution.</summary>
    public Task<string> CreateSessionAsync(CancellationToken cancellationToken);
    /// <summary>Runs a turn with operator authority only.</summary>
    public Task<DurableAgentReply> RunAsync(string sessionId, string message, CancellationToken cancellationToken);
    /// <summary>Queries the actual orchestration index for this session.</summary>
    public Task<IReadOnlyList<string>> FindOperationsAsync(string sessionId, CancellationToken cancellationToken);
    /// <summary>Reads authoritative progress.</summary>
    public Task<RecoveryOperationStatus> GetAsync(string operationId, CancellationToken cancellationToken);
    /// <summary>Submits the exact displayed review using separate supervisor authority.</summary>
    public Task DecideAsync(RecoveryReview review, bool approved, CancellationToken cancellationToken);
    /// <summary>Reads fresh authoritative Hub evidence.</summary>
    public Task<EnergyOperationalTwin> ReadHubAsync(CancellationToken cancellationToken);
}

internal sealed class RecoveryRehearsalApi(IHttpClientFactory clients, RecoveryLocalSettings settings,
    DemoStageApiClient stages, DemoScenarioApiClient scenarios, IHttpContextAccessor? requestContext = null) : IRecoveryRehearsalApi
{
    private static readonly JsonSerializerOptions JsonOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task PrepareCityAsync(CancellationToken cancellationToken)
    {
        await ReadHubAsync(cancellationToken);
        // Check the same Command Center used by Functions, not just the scenario controller.
        var values = settings.Read();
        await SendAsync<DemoStageStatus>(new Uri(new Uri(values["COMMANDCENTER_BASE_URI"]), "/api/command-center/stage"), HttpMethod.Get, null, null, cancellationToken);
        var catalog = await stages.GetCatalogAsync(cancellationToken);
        if (catalog.CurrentStage.Id < DemoStage.Workflow)
        {
            var changed = await stages.ApplyStageAsync(DemoStage.Workflow, cancellationToken);
            if (!changed.Succeeded)
            {
                throw new InvalidOperationException(changed.Message);
            }
        }

        var result = await scenarios.ApplyScenarioAsync(ScenarioId.ForgottenOverride, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }
    }

    public async Task<string> CreateSessionAsync(CancellationToken cancellationToken) =>
        (await FunctionsAsync<RecoverySessionIdentity>("durable-agent/sessions", HttpMethod.Post, null, false, cancellationToken)).SessionId;

    public Task<DurableAgentReply> RunAsync(string sessionId, string message, CancellationToken cancellationToken) =>
        FunctionsAsync<DurableAgentReply>("durable-agent/run", HttpMethod.Post, new DurableAgentTurn(message, sessionId), false, cancellationToken);

    public async Task<IReadOnlyList<string>> FindOperationsAsync(string sessionId, CancellationToken cancellationToken) =>
        await FunctionsAsync<string[]>($"durable-agent/sessions/{Uri.EscapeDataString(sessionId)}/operations", HttpMethod.Get, null, false, cancellationToken);

    public Task<RecoveryOperationStatus> GetAsync(string operationId, CancellationToken cancellationToken) =>
        FunctionsAsync<RecoveryOperationStatus>($"durable-operations/{Uri.EscapeDataString(operationId)}", HttpMethod.Get, null, false, cancellationToken);

    public async Task DecideAsync(RecoveryReview review, bool approved, CancellationToken cancellationToken) =>
        await FunctionsAsync<JsonElement>($"durable-operations/{Uri.EscapeDataString(review.InstanceId)}/decision", HttpMethod.Post,
            new RecoveryDecisionRequest(review, approved), true, cancellationToken);

    public Task<EnergyOperationalTwin> ReadHubAsync(CancellationToken cancellationToken) =>
        SendAsync<EnergyOperationalTwin>(new Uri(new Uri(settings.Read()["ENERGYHUB_BASE_URI"]), "/api/energy/assets/L-417"),
            HttpMethod.Get, null, null, cancellationToken);

    private Task<T> FunctionsAsync<T>(string path, HttpMethod method, object? body, bool supervisor, CancellationToken cancellationToken) =>
        SendAsync<T>(new Uri(RecoveryLocalSettings.FunctionsAddress, "api/" + path), method, body,
            settings.Read()[supervisor ? "CAESAREA_SUPERVISOR_KEY" : "CAESAREA_OPERATOR_KEY"], cancellationToken);

    private async Task<T> SendAsync<T>(Uri uri, HttpMethod method, object? body, string? credential, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient("recovery-rehearsal");
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, InvocationCorrelation.Resolve(requestContext?.HttpContext));
        if (credential is not null)
        {
            request.Headers.Add("X-Caesarea-Key", credential);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{method} {uri.AbsolutePath} returned HTTP {(int)response.StatusCode}. Check readiness and local worker logs; no automatic resubmission was made.", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The recovery endpoint returned an empty result.");
    }
}
