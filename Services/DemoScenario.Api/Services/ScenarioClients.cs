using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DemoScenario.Api.Services;

/// <summary>
/// Coordinates reset and scenario application calls to the SmartPole simulator boundary.
/// </summary>
public interface ISmartPoleScenarioClient
{
    /// <summary>
    /// Resets the SmartPole simulator to its deterministic baseline.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the scenario orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ResetAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the supplied scenario state to the SmartPole simulator.
    /// </summary>
    /// <param name="scenarioState">The simulator state to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the scenario orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ApplyScenarioAsync(SmartPoleScenarioState scenarioState, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the current simulator behavior configuration.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the presenter request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task<SmartPoleBehaviorConfiguration> GetBehaviorAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the simulator behavior configuration used for subsequent commands.
    /// </summary>
    /// <param name="configuration">The behavior configuration to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the presenter request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task<SmartPoleBehaviorConfiguration> UpdateBehaviorAsync(SmartPoleBehaviorConfiguration configuration, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// Coordinates reset and scenario synchronization calls to the Energy Hub boundary.
/// </summary>
public interface IEnergyScenarioClient
{
    /// <summary>
    /// Resets the Energy Hub to its deterministic baseline.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the scenario orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ResetAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the supplied synchronization request to the Energy Hub.
    /// </summary>
    /// <param name="request">The Energy Hub synchronization request.</param>
    /// <param name="correlationId">The correlation identifier spanning the scenario orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken);
}

/// <summary>
/// Coordinates reset and scenario synchronization calls to the Command Center boundary.
/// </summary>
public interface ICommandCenterScenarioClient
{
    /// <summary>
    /// Resets the Command Center to its deterministic baseline.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the scenario orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ResetAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the supplied scenario context to the Command Center.
    /// </summary>
    /// <param name="scenarioContext">The scenario context to apply.</param>
    /// <param name="correlationId">The correlation identifier spanning the scenario orchestration request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ApplyScenarioAsync(CommandCenterScenarioContext scenarioContext, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ISmartPoleScenarioClient"/>
public sealed partial class HttpSmartPoleScenarioClient(HttpClient httpClient, ILogger<HttpSmartPoleScenarioClient> logger) : ISmartPoleScenarioClient
{
    /// <inheritdoc />
    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return ScenarioHttpRequestSender.SendAsync(httpClient, logger, "SmartPole", "reset", ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/smartpole/reset", correlationId), correlationId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyScenarioAsync(SmartPoleScenarioState scenarioState, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenarioState);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return ScenarioHttpRequestSender.SendAsync(httpClient, logger, "SmartPole", "apply scenario", ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/smartpole/scenario", correlationId, scenarioState), correlationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SmartPoleBehaviorConfiguration> GetBehaviorAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = ScenarioHttpRequestFactory.CreateRequest(
            HttpMethod.Get,
            $"/api/smartpole/state/{DemoAssets.StreetlightAssetId}",
            correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var state = await response.Content.ReadFromJsonAsync<SmartPolePhysicalState>(BehaviorSerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("SmartPole returned an empty state response.");
        return state.Configuration;
    }

    /// <inheritdoc />
    public async Task<SmartPoleBehaviorConfiguration> UpdateBehaviorAsync(SmartPoleBehaviorConfiguration configuration, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = ScenarioHttpRequestFactory.CreateRequest(
            HttpMethod.Post,
            "/api/smartpole/configuration",
            correlationId,
            configuration);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var state = await response.Content.ReadFromJsonAsync<SmartPolePhysicalState>(BehaviorSerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("SmartPole returned an empty configuration response.");
        return state.Configuration;
    }

    private static readonly JsonSerializerOptions BehaviorSerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();
}

/// <inheritdoc cref="IEnergyScenarioClient"/>
public sealed partial class HttpEnergyScenarioClient(HttpClient httpClient, ILogger<HttpEnergyScenarioClient> logger) : IEnergyScenarioClient
{
    /// <inheritdoc />
    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return ScenarioHttpRequestSender.SendAsync(httpClient, logger, "Energy Hub", "reset", ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/energy/admin/reset", correlationId), correlationId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return ScenarioHttpRequestSender.SendAsync(httpClient, logger, "Energy Hub", "apply scenario", ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/energy/admin/scenario", correlationId, request), correlationId, cancellationToken);
    }
}

/// <inheritdoc cref="ICommandCenterScenarioClient"/>
public sealed partial class HttpCommandCenterScenarioClient(HttpClient httpClient, ILogger<HttpCommandCenterScenarioClient> logger) : ICommandCenterScenarioClient
{
    /// <inheritdoc />
    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return ScenarioHttpRequestSender.SendAsync(httpClient, logger, "Command Center", "reset", ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/command-center/admin/reset", correlationId), correlationId, cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyScenarioAsync(CommandCenterScenarioContext scenarioContext, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenarioContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return ScenarioHttpRequestSender.SendAsync(httpClient, logger, "Command Center", "apply scenario", ScenarioHttpRequestFactory.CreateRequest(HttpMethod.Post, "/api/command-center/admin/scenario", correlationId, scenarioContext), correlationId, cancellationToken);
    }
}

file static class ScenarioHttpRequestSender
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public static async Task SendAsync(
        HttpClient httpClient,
        ILogger logger,
        string targetService,
        string operation,
        HttpRequestMessage request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            using (request)
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                var detail = await TryReadProblemDetailAsync(response, cancellationToken);
                ScenarioClientLog.RequestFailed(logger, targetService, operation, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"{targetService} {operation} request failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            ScenarioClientLog.RequestTimedOut(logger, targetService, operation, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            ScenarioClientLog.RequestHttpError(logger, targetService, operation, correlationId, exception);
            throw;
        }
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
            return problem?.Detail ?? problem?.Title;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(rawContent) ? null : rawContent;
        }
    }
}

file static class ScenarioHttpRequestFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    public static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId, object? body = null)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SerializerOptions);
        }

        return request;
    }
}

internal static partial class ScenarioClientLog
{
    [LoggerMessage(
        EventId = 1900,
        Level = LogLevel.Warning,
        Message = "{TargetService} scenario {Operation} request failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void RequestFailed(ILogger logger, string targetService, string operation, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 1901,
        Level = LogLevel.Warning,
        Message = "{TargetService} scenario {Operation} request timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void RequestTimedOut(ILogger logger, string targetService, string operation, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1902,
        Level = LogLevel.Error,
        Message = "{TargetService} scenario {Operation} request failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void RequestHttpError(ILogger logger, string targetService, string operation, string correlationId, Exception exception);
}
