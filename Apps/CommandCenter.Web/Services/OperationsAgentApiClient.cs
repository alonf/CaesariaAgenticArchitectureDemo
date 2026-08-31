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
}

internal sealed record OperationsAgentOutcome(bool Succeeded, OperationsAgentResponse? Result, string? FailureMessage);
