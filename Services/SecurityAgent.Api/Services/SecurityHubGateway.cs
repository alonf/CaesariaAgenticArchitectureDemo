using System.Text.Json;

namespace SecurityAgent.Api.Services;

/// <summary>
/// Reads the authoritative Security Hub. This client exists in exactly one service: the Security
/// Agent's. No other agent, and no other domain, is given a way to read these records - which is
/// the boundary that justifies a second agent at all.
/// </summary>
public interface ISecurityHubGateway
{
    /// <summary>
    /// Reads the operations currently active in an area.
    /// </summary>
    /// <param name="area">The area to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The area's security status, including restricted operational detail.</returns>
    public Task<SecurityAreaStatus> GetAreaStatusAsync(string area, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ISecurityHubGateway"/>
public sealed class HttpSecurityHubGateway(HttpClient httpClient) : ISecurityHubGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<SecurityAreaStatus> GetAreaStatusAsync(string area, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/security/areas/{Uri.EscapeDataString(area)}/operations");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SecurityAreaStatus>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("The Security Hub returned an empty area status.");
    }
}
