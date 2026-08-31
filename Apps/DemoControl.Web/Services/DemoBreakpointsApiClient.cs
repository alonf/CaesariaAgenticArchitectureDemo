using System.Text.Json;

namespace DemoControl.Web.Services;

internal sealed class DemoBreakpointsApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<DemoBreakpointsResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Get, "/api/demo-breakpoints", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoBreakpointsResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Demo breakpoints response was empty.");
    }

    public async Task<DemoBreakpointsResponse> SetArmedAsync(string snippetName, bool armed, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetName);

        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(HttpMethod.Put, $"/api/demo-breakpoints/{Uri.EscapeDataString(snippetName)}", correlationId);
        request.Content = JsonContent.Create(new DemoBreakpointArmRequest(armed), options: SerializerOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoBreakpointsResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Demo breakpoints response was empty.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }
}
