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

    public async Task<OperationsAgentOutcome> AskAsync(
        string question,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        var agentRequest = new OperationsAgentRequest(question, sessionId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/operations-agent/ask")
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
