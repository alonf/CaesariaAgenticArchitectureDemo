using System.Text.Json;

namespace DemoControl.Web.Services;

internal sealed class SecurityConsultApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public async Task<SecurityConsultState> GetStatusAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SecurityConsultState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Security consult response was empty.");
    }

    public async Task<SecurityConsultState> SetEnabledAsync(bool enabled, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post);
        request.Content = JsonContent.Create(new SecurityConsultState(enabled), options: SerializerOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SecurityConsultState>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Security consult response was empty.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method)
    {
        var request = new HttpRequestMessage(method, "/api/operations-agent/security-consult/");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        return request;
    }
}

internal sealed record SecurityConsultState(bool Enabled);
