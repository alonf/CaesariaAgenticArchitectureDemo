using System.Net.Http.Json;
using Caesarea.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace EnergyHub.Api.Services;

public sealed class HttpSmartPoleGateway(HttpClient httpClient, TimeProvider timeProvider) : ISmartPoleGateway
{
    public async Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/smartpole/state/{Uri.EscapeDataString(assetId)}", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SmartPolePhysicalState>(cancellationToken)
            ?? throw new InvalidOperationException("SmartPole state response was empty.");
    }

    public async Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, "/api/smartpole/commands/lamp-state", correlationId, command);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SmartPoleCommandResult>(cancellationToken)
                ?? throw new InvalidOperationException("SmartPole command response was empty.");
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        var status = response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
            ? CommandExecutionStatus.TimedOut
            : CommandExecutionStatus.Failed;

        return new SmartPoleCommandResult(
            command.AssetId,
            null,
            status,
            correlationId,
            problem?.Detail ?? "SmartPole command failed without a response body.",
            timeProvider.GetUtcNow());
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId, object? body = null)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
