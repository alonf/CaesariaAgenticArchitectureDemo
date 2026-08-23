using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace EnergyHub.Api.Services;

/// <inheritdoc cref="ISmartPoleGateway"/>
public sealed partial class HttpSmartPoleGateway(HttpClient httpClient, TimeProvider timeProvider, ILogger<HttpSmartPoleGateway> logger) : ISmartPoleGateway
{
    /// <inheritdoc />
    public async Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"/api/smartpole/state/{Uri.EscapeDataString(assetId)}", correlationId);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await TryReadProblemDetailAsync(response, cancellationToken);
                HttpSmartPoleGatewayLog.StateRequestFailed(logger, assetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"SmartPole state request failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<SmartPolePhysicalState>(cancellationToken)
                    ?? throw new InvalidOperationException("SmartPole state response was empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("SmartPole state response payload was invalid.", exception);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            HttpSmartPoleGatewayLog.StateRequestTimedOut(logger, assetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            HttpSmartPoleGatewayLog.StateRequestHttpError(logger, assetId, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            HttpSmartPoleGatewayLog.StateRequestInvalid(logger, assetId, correlationId, exception);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.AssetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        try
        {
            using var request = CreateRequest(HttpMethod.Post, "/api/smartpole/commands/lamp-state", correlationId, command);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync<SmartPoleCommandResult>(cancellationToken)
                        ?? throw new InvalidOperationException("SmartPole command response was empty.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidOperationException("SmartPole command response payload was invalid.", exception);
                }
            }

            var detail = await TryReadProblemDetailAsync(response, cancellationToken);
            var status = response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
                ? CommandExecutionStatus.TimedOut
                : CommandExecutionStatus.Failed;

            HttpSmartPoleGatewayLog.CommandRequestFailed(logger, command.AssetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");

            return new SmartPoleCommandResult(
                command.AssetId,
                null,
                status,
                correlationId,
                detail ?? "SmartPole command failed without a response body.",
                timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            HttpSmartPoleGatewayLog.CommandRequestTimedOut(logger, command.AssetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception)
        {
            HttpSmartPoleGatewayLog.CommandRequestHttpError(logger, command.AssetId, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            HttpSmartPoleGatewayLog.CommandRequestInvalid(logger, command.AssetId, correlationId, exception);
            throw;
        }
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

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            return problem?.Detail ?? problem?.Title;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(rawContent) ? null : rawContent;
        }
    }
}

internal static partial class HttpSmartPoleGatewayLog
{
    [LoggerMessage(
        EventId = 1600,
        Level = LogLevel.Warning,
        Message = "SmartPole state request for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void StateRequestFailed(ILogger logger, string assetId, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 1601,
        Level = LogLevel.Warning,
        Message = "SmartPole state request for asset {AssetId} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1602,
        Level = LogLevel.Error,
        Message = "SmartPole state request for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestHttpError(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1603,
        Level = LogLevel.Error,
        Message = "SmartPole state request for asset {AssetId} returned an invalid payload. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1604,
        Level = LogLevel.Warning,
        Message = "SmartPole command for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void CommandRequestFailed(ILogger logger, string assetId, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 1605,
        Level = LogLevel.Warning,
        Message = "SmartPole command for asset {AssetId} timed out before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandRequestTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1606,
        Level = LogLevel.Error,
        Message = "SmartPole command for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandRequestHttpError(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1607,
        Level = LogLevel.Error,
        Message = "SmartPole command for asset {AssetId} returned an invalid payload. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandRequestInvalid(ILogger logger, string assetId, string correlationId, Exception exception);
}
