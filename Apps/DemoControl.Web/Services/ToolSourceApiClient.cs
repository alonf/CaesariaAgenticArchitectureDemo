using System.Text.Json;

namespace DemoControl.Web.Services;

internal sealed class ToolSourceApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<ToolSourceState> GetStatusAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest("/api/operations-agent/tool-source/", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<ToolSourceState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Tool source response was empty.");
    }

    public async Task<ToolSourceState> SetSourceAsync(string source, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest("/api/operations-agent/tool-source/", correlationId);
        request.Method = HttpMethod.Post;
        request.Content = JsonContent.Create(new ToolSourceState(source), options: SerializerOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await response.EnsureSuccessAsync(cancellationToken);

        return await response.Content.ReadFromJsonAsync<ToolSourceState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Tool source response was empty.");
    }

    private static HttpRequestMessage CreateRequest(string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }
}

internal sealed record ToolSourceState(string Source);
