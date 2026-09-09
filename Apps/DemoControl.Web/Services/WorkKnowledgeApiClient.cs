using System.Text.Json;

namespace DemoControl.Web.Services;

internal sealed class WorkKnowledgeApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<WorkKnowledgeState> GetStatusAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Get, "/api/operations-agent/work-knowledge/", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<WorkKnowledgeState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Work knowledge response was empty.");
    }

    public async Task<WorkKnowledgeState> SetEvidencePresentAsync(bool evidencePresent, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Post, "/api/operations-agent/work-knowledge/", correlationId);
        request.Content = JsonContent.Create(new WorkKnowledgeState(evidencePresent), options: SerializerOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<WorkKnowledgeState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Work knowledge response was empty.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }
}

internal sealed record WorkKnowledgeState(bool EvidencePresent);
