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

        return new StageApiCommandResult(false, problemDetail ?? "Stage change failed.", effectiveCorrelationId);
    }

    /// <summary>
    /// Checks a beat's prerequisites against the live demo, without changing anything.
    /// </summary>
    public async Task<DemoStageReadiness> GetReadinessAsync(DemoStage stage, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/demo-stage/readiness/{stage}", CorrelationIds.Create());
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoStageReadiness>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Demo stage readiness response was empty.");
    }

    /// <summary>
    /// Prepares a beat: the scenario it starts from, its stage, and the switches it starts with.
    /// </summary>
    public async Task<StageApiCommandResult> PrepareAsync(DemoStage stage, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Post, $"/api/demo-stage/prepare/{stage}", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var effectiveCorrelationId = TryGetCorrelationId(response) ?? correlationId;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<DemoStagePrepareResult>(SerializerOptions, cancellationToken);
            var message = result is null
                ? "Beat prepared."
                : string.Join(" ", result.Actions.Append(result.Summary));
            return new StageApiCommandResult(result?.Readiness.Ready ?? true, message, effectiveCorrelationId);
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

        return new StageApiCommandResult(false, problemDetail ?? "The beat could not be prepared.", effectiveCorrelationId);
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
