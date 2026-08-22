using System.Net;
using System.Net.Http.Json;
using Caesarea.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CommandCenter.Api.Services;

public sealed class HttpEnergyHubGateway(HttpClient httpClient, TimeProvider timeProvider) : IEnergyHubGateway
{
    public async Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/energy/assets/{Uri.EscapeDataString(assetId)}", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EnergyOperationalTwin>(cancellationToken)
            ?? throw new InvalidOperationException("Energy Hub state response was empty.");
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/energy/assets/{Uri.EscapeDataString(assetId)}/activity?limit={limit}", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ActivityRecord[]>(cancellationToken)
            ?? [];
    }

    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/energy/assets/{Uri.EscapeDataString(assetId)}/restore-scheduled-mode", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<RestoreScheduledModeResult>(cancellationToken)
                ?? throw new InvalidOperationException("Energy Hub command response was empty.");
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        var status = response.StatusCode == HttpStatusCode.GatewayTimeout
            ? CommandExecutionStatus.TimedOut
            : CommandExecutionStatus.Failed;
        var desiredIsOn = TryReadBoolean(problem, "desiredIsOn");
        var reportedIsOn = TryReadBoolean(problem, "reportedIsOn");

        return new RestoreScheduledModeResult(
            assetId,
            desiredIsOn,
            reportedIsOn,
            status,
            correlationId,
            problem?.Detail ?? "Energy Hub command failed without a response body.",
            timeProvider.GetUtcNow());
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }

    private static bool TryReadBoolean(ProblemDetails? problem, string key)
    {
        if (problem?.Extensions.TryGetValue(key, out var value) != true)
        {
            return false;
        }

        return value switch
        {
            bool booleanValue => booleanValue,
            System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind is System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind is System.Text.Json.JsonValueKind.False => false,
            _ => false
        };
    }
}
