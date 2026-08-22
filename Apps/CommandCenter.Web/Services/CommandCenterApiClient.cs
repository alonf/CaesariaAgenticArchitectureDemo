using System.Net.Http.Json;
using Caesarea.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CommandCenter.Web.Services;

public sealed class CommandCenterApiClient(HttpClient httpClient)
{
    public async Task<CommandCenterSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(
            HttpMethod.Get,
            $"/api/command-center/snapshot/{DemoAssets.StreetlightAssetId}?limit=16",
            correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CommandCenterSnapshot>(cancellationToken)
            ?? throw new InvalidOperationException("Command Center snapshot response was empty.");
    }

    public async Task<CommandInvocationResult> RestoreScheduledModeAsync(CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIds.Create();
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/command-center/assets/{DemoAssets.StreetlightAssetId}/restore-scheduled-mode",
            correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var effectiveCorrelationId = TryGetCorrelationId(response) ?? correlationId;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RestoreScheduledModeResult>(cancellationToken);
            return new CommandInvocationResult(true, result?.Summary ?? "Restore Scheduled Mode completed.", effectiveCorrelationId);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        return new CommandInvocationResult(false, problem?.Detail ?? "Restore Scheduled Mode failed.", effectiveCorrelationId);
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

public sealed record CommandInvocationResult(bool Succeeded, string Message, string CorrelationId);
