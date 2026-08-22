using System.Net.Http.Json;
using Caesarea.Contracts;

namespace DemoScenario.Api.Services;

public interface ISmartPoleScenarioClient
{
    Task ResetAsync(string correlationId, CancellationToken cancellationToken);

    Task ApplyScenarioAsync(SmartPoleScenarioState scenarioState, string correlationId, CancellationToken cancellationToken);
}

public interface IEnergyScenarioClient
{
    Task ResetAsync(string correlationId, CancellationToken cancellationToken);

    Task ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken);
}

public interface ICommandCenterScenarioClient
{
    Task ResetAsync(string correlationId, CancellationToken cancellationToken);

    Task ApplyScenarioAsync(CommandCenterScenarioContext scenarioContext, string correlationId, CancellationToken cancellationToken);
}

public sealed class HttpSmartPoleScenarioClient(HttpClient httpClient) : ISmartPoleScenarioClient
{
    public async Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var request = ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/smartpole/reset", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyScenarioAsync(SmartPoleScenarioState scenarioState, string correlationId, CancellationToken cancellationToken)
    {
        using var request = ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/smartpole/scenario", correlationId, scenarioState);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class HttpEnergyScenarioClient(HttpClient httpClient) : IEnergyScenarioClient
{
    public async Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var request = ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/energy/admin/reset", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyScenarioAsync(EnergyScenarioSyncRequest requestModel, string correlationId, CancellationToken cancellationToken)
    {
        using var request = ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/energy/admin/scenario", correlationId, requestModel);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class HttpCommandCenterScenarioClient(HttpClient httpClient) : ICommandCenterScenarioClient
{
    public async Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        using var request = ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/command-center/admin/reset", correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyScenarioAsync(CommandCenterScenarioContext scenarioContext, string correlationId, CancellationToken cancellationToken)
    {
        using var request = ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/command-center/admin/scenario", correlationId, scenarioContext);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

file static class ScenarioHttpRequestFactory
{
    public static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId, object? body = null)
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
