using System.Text.Json;

namespace DemoControl.Web.Services;

internal sealed class AgentHabitatApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<AgentHabitatState> GetStatusAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest("/api/operations-agent/habitat/", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<AgentHabitatState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Agent habitat response was empty.");
    }

    public async Task<AgentHabitatState> SetHabitatAsync(string habitat, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest("/api/operations-agent/habitat/", correlationId);
        request.Method = HttpMethod.Post;
        request.Content = JsonContent.Create(new AgentHabitatState(habitat), options: SerializerOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<AgentHabitatState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Agent habitat response was empty.");
    }

    private static HttpRequestMessage CreateRequest(string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }
}

internal sealed record AgentHabitatState(string Habitat);
