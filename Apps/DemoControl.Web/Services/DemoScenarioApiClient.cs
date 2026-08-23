using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DemoControl.Web.Services;

internal sealed class DemoScenarioApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<ScenarioCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Get, "/api/demo-scenarios", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ScenarioCatalogResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Scenario catalog response was empty.");
    }

    public async Task<ScenarioApiCommandResult> ApplyScenarioAsync(ScenarioId scenarioId, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Post, $"/api/demo-scenarios/apply/{scenarioId}", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var effectiveCorrelationId = TryGetCorrelationId(response) ?? correlationId;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ScenarioApplicationResult>(SerializerOptions, cancellationToken);
            return new ScenarioApiCommandResult(true, result?.Summary ?? "Scenario applied.", effectiveCorrelationId);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
        return new ScenarioApiCommandResult(false, problem?.Detail ?? "Scenario apply failed.", effectiveCorrelationId);
    }

    public async Task<ScenarioApiCommandResult> ResetAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Post, "/api/demo-scenarios/reset", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var effectiveCorrelationId = TryGetCorrelationId(response) ?? correlationId;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ScenarioApplicationResult>(SerializerOptions, cancellationToken);
            return new ScenarioApiCommandResult(true, result?.Summary ?? "Scenario reset completed.", effectiveCorrelationId);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
        return new ScenarioApiCommandResult(false, problem?.Detail ?? "Scenario reset failed.", effectiveCorrelationId);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }

    private static string? TryGetCorrelationId(HttpResponseMessage response) =>
        response.Headers.TryGetValues(CorrelationHeaderNames.XCorrelationId, out var values)
            ? values.FirstOrDefault()
            : null;
}

internal sealed record ScenarioApiCommandResult(bool Succeeded, string Message, string CorrelationId);
