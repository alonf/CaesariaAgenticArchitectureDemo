using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CommandCenter.Web.Services;

internal sealed class OperationsAgentApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();
    private readonly HttpClient _httpClient;

    public OperationsAgentApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<OperationsAgentOutcome> AskAsync(
        string question,
        string? sessionId,
        CancellationToken cancellationToken) =>
        PostQuestionAsync("/api/operations-agent/ask", question, sessionId, cancellationToken);

    /// <summary>
    /// Asks the Operations Agent to put a question to the workforce domain's own agent over A2A.
    /// It is a route of its own because the peer is not a tool: this is the service deciding to
    /// delegate, not a model picking a capability out of its toolbox.
    /// </summary>
    public Task<OperationsAgentOutcome> ConsultWorkforceAsync(
        string question,
        CancellationToken cancellationToken) =>
        PostQuestionAsync("/api/operations-agent/workforce-consult", question, sessionId: null, cancellationToken);

    private async Task<OperationsAgentOutcome> PostQuestionAsync(
        string route,
        string question,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        var agentRequest = new OperationsAgentRequest(question, sessionId);
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(agentRequest, options: SerializerOptions)
        };
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<OperationsAgentResponse>(SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Operations Agent response was empty.");
            return new OperationsAgentOutcome(true, result, null);
        }

        string? problemDetail;

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
            problemDetail = problem?.Detail;
        }
        catch (JsonException)
        {
            problemDetail = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
        }

        return new OperationsAgentOutcome(false, null, problemDetail ?? "The Operations Agent request could not be completed.");
    }

    /// <summary>
    /// Reads the presenter-selected agent habitat. The Command Center polls this to badge the
    /// panel and route the ask; the switch itself is flipped from the switchboard.
    /// </summary>
    public async Task<OperationsAgentHabitatStatus> GetHabitatAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations-agent/habitat/");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OperationsAgentHabitatStatus>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Agent habitat response was empty.");
    }

    public async Task<IReadOnlyList<OperationsAgentPendingApproval>> GetPendingApprovalsAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations-agent/approvals/");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<OperationsAgentPendingApproval>>(SerializerOptions, cancellationToken)
            ?? [];
    }

    public async Task RespondToApprovalAsync(string approvalId, bool approved, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/operations-agent/approvals/{Uri.EscapeDataString(approvalId)}")
        {
            Content = JsonContent.Create(new OperationsAgentApprovalDecision(approved), options: SerializerOptions)
        };
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<OperationsAgentWorkflowDefinition> GetWorkflowDefinitionAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/operations-agent/remediation/definition");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OperationsAgentWorkflowDefinition>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Workflow definition response was empty.");
    }

    public async Task<OperationsAgentWorkflowRunReport> StartWorkflowRunAsync(string assetId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/operations-agent/remediation/")
        {
            Content = JsonContent.Create(new OperationsAgentRemediationRequest(assetId), options: SerializerOptions)
        };
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OperationsAgentWorkflowRunReport>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Workflow start response was empty.");
    }

    public async Task<OperationsAgentWorkflowRunReport?> FindWorkflowRunByCorrelationAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/operations-agent/remediation/runs?correlationId={Uri.EscapeDataString(correlationId)}");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        // No run for this correlation is the ordinary case - most asks start no workflow - and the
        // service reports it as No Content. Deserializing an empty body would throw, so the absence
        // is read from the response itself rather than from a parse failure.
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength is 0)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OperationsAgentWorkflowRunReport>(SerializerOptions, cancellationToken);
    }

    public async Task<OperationsAgentWorkflowRunReport> GetWorkflowRunAsync(string runId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/operations-agent/remediation/runs/{Uri.EscapeDataString(runId)}");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OperationsAgentWorkflowRunReport>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Workflow run response was empty.");
    }

    public async Task<OperationsAgentRecalledCase> CloseCaseAsync(
        string assetId,
        string symptom,
        string resolution,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/operations-agent/cases/")
        {
            Content = JsonContent.Create(new OperationsAgentCloseCaseRequest(assetId, symptom, resolution), options: SerializerOptions)
        };
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OperationsAgentRecalledCase>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Close-case response was empty.");
    }
}

internal sealed record OperationsAgentOutcome(bool Succeeded, OperationsAgentResponse? Result, string? FailureMessage);
