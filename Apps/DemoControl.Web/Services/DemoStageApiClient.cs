using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DemoControl.Web.Services;

internal sealed class DemoStageApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<DemoStageCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Get, "/api/demo-stage", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoStageCatalogResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Demo stage catalog response was empty.");
    }

    public async Task<StageApiCommandResult> ApplyStageAsync(DemoStage stage, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Post, $"/api/demo-stage/apply/{stage}", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var effectiveCorrelationId = TryGetCorrelationId(response) ?? correlationId;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<DemoStageChangeResult>(SerializerOptions, cancellationToken);
            return new StageApiCommandResult(true, result?.Summary ?? "Stage applied.", effectiveCorrelationId);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
        return new StageApiCommandResult(false, problem?.Detail ?? "Stage change failed.", effectiveCorrelationId);
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

internal sealed record StageApiCommandResult(bool Succeeded, string Message, string CorrelationId);
