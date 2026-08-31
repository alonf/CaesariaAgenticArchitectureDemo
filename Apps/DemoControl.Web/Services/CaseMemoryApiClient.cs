using System.Text.Json;

namespace DemoControl.Web.Services;

internal sealed class CaseMemoryApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<CaseMemoryState> GetStatusAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Get, "/api/operations-agent/cases/", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CaseMemoryState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Case memory response was empty.");
    }

    public async Task<CaseMemoryState> ClearAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Post, "/api/operations-agent/cases/clear", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CaseMemoryState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Case memory response was empty.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }
}

internal sealed record ClosedCaseSummary(string CaseId, string AssetId, string Symptom, string Resolution, DateTimeOffset ClosedAt);

internal sealed record CaseMemoryState(IReadOnlyList<ClosedCaseSummary> Cases);
