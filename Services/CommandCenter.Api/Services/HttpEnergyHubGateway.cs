using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace CommandCenter.Api.Services;

/// <inheritdoc cref="IEnergyHubGateway"/>
public sealed partial class HttpEnergyHubGateway(HttpClient httpClient, TimeProvider timeProvider, ILogger<HttpEnergyHubGateway> logger) : IEnergyHubGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"/api/energy/assets/{Uri.EscapeDataString(assetId)}", correlationId);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await TryReadProblemDetailAsync(response, cancellationToken);
                HttpEnergyHubGatewayLog.StateRequestFailed(logger, assetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"Energy Hub state request failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<EnergyOperationalTwin>(SerializerOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Energy Hub state response was empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Energy Hub state response payload was invalid.", exception);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            HttpEnergyHubGatewayLog.StateRequestTimedOut(logger, assetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            HttpEnergyHubGatewayLog.StateRequestHttpError(logger, assetId, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            HttpEnergyHubGatewayLog.StateRequestInvalid(logger, assetId, correlationId, exception);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"/api/energy/assets/{Uri.EscapeDataString(assetId)}/activity?limit={limit}", correlationId);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await TryReadProblemDetailAsync(response, cancellationToken);
                HttpEnergyHubGatewayLog.ActivityRequestFailed(logger, assetId, limit, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"Energy Hub activity request failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<ActivityRecord[]>(SerializerOptions, cancellationToken)
                    ?? [];
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Energy Hub activity response payload was invalid.", exception);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            HttpEnergyHubGatewayLog.ActivityRequestTimedOut(logger, assetId, limit, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            HttpEnergyHubGatewayLog.ActivityRequestHttpError(logger, assetId, limit, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            HttpEnergyHubGatewayLog.ActivityRequestInvalid(logger, assetId, limit, correlationId, exception);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"/api/energy/assets/{Uri.EscapeDataString(assetId)}/restore-scheduled-mode", correlationId);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return await response.Content.ReadFromJsonAsync<RestoreScheduledModeResult>(SerializerOptions, cancellationToken)
                        ?? throw new InvalidOperationException("Energy Hub command response was empty.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidOperationException("Energy Hub command response payload was invalid.", exception);
                }
            }

            var problem = await TryReadProblemAsync(response, cancellationToken);
            var status = response.StatusCode == HttpStatusCode.GatewayTimeout
                ? CommandExecutionStatus.TimedOut
                : CommandExecutionStatus.Failed;
            var desiredIsOn = TryReadBoolean(problem, "desiredIsOn");
            var reportedIsOn = TryReadBoolean(problem, "reportedIsOn");

            HttpEnergyHubGatewayLog.CommandRequestFailed(
                logger,
                assetId,
                correlationId,
                (int)response.StatusCode,
                status,
                problem?.Detail ?? "No response detail was provided.");

            return new RestoreScheduledModeResult(
                assetId,
                desiredIsOn,
                reportedIsOn,
                status,
                correlationId,
                problem?.Detail ?? "Energy Hub command failed without a response body.",
                timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            HttpEnergyHubGatewayLog.CommandRequestTimedOut(logger, assetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception)
        {
            HttpEnergyHubGatewayLog.CommandRequestHttpError(logger, assetId, correlationId, exception);
            throw;
        }
        catch (InvalidOperationException exception)
        {
            HttpEnergyHubGatewayLog.CommandRequestInvalid(logger, assetId, correlationId, exception);
            throw;
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }

    private static bool? TryReadBoolean(ProblemDetails? problem, string key)
    {
        if (problem?.Extensions.TryGetValue(key, out var value) != true)
        {
            return null;
        }

        return value switch
        {
            bool booleanValue => booleanValue,
            JsonElement jsonElement when jsonElement.ValueKind is JsonValueKind.True => true,
            JsonElement jsonElement when jsonElement.ValueKind is JsonValueKind.False => false,
            _ => null
        };
    }

    private static async Task<ProblemDetails?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(detail)
                ? null
                : new ProblemDetails
                {
                    Detail = detail,
                    Status = (int)response.StatusCode
                };
        }
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await TryReadProblemAsync(response, cancellationToken);
        return problem?.Detail ?? problem?.Title;
    }
}

internal static partial class HttpEnergyHubGatewayLog
{
    [LoggerMessage(
        EventId = 1700,
        Level = LogLevel.Warning,
        Message = "Energy Hub state request for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void StateRequestFailed(ILogger logger, string assetId, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 1701,
        Level = LogLevel.Warning,
        Message = "Energy Hub state request for asset {AssetId} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1702,
        Level = LogLevel.Error,
        Message = "Energy Hub state request for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestHttpError(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1703,
        Level = LogLevel.Error,
        Message = "Energy Hub state request for asset {AssetId} returned an invalid payload. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestInvalid(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1704,
        Level = LogLevel.Warning,
        Message = "Energy Hub activity request for asset {AssetId} with limit {Limit} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void ActivityRequestFailed(ILogger logger, string assetId, int limit, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 1705,
        Level = LogLevel.Warning,
        Message = "Energy Hub activity request for asset {AssetId} with limit {Limit} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void ActivityRequestTimedOut(ILogger logger, string assetId, int limit, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1706,
        Level = LogLevel.Error,
        Message = "Energy Hub activity request for asset {AssetId} with limit {Limit} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void ActivityRequestHttpError(ILogger logger, string assetId, int limit, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1707,
        Level = LogLevel.Error,
        Message = "Energy Hub activity request for asset {AssetId} with limit {Limit} returned an invalid payload. CorrelationId: {CorrelationId}.")]
    internal static partial void ActivityRequestInvalid(ILogger logger, string assetId, int limit, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1708,
        Level = LogLevel.Warning,
        Message = "Energy Hub Restore Scheduled Mode for asset {AssetId} returned status code {StatusCode} with command status {Status}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void CommandRequestFailed(ILogger logger, string assetId, string correlationId, int statusCode, CommandExecutionStatus status, string detail);

    [LoggerMessage(
        EventId = 1709,
        Level = LogLevel.Warning,
        Message = "Energy Hub Restore Scheduled Mode for asset {AssetId} timed out before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandRequestTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1710,
        Level = LogLevel.Error,
        Message = "Energy Hub Restore Scheduled Mode for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandRequestHttpError(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 1711,
        Level = LogLevel.Error,
        Message = "Energy Hub Restore Scheduled Mode for asset {AssetId} returned an invalid payload. CorrelationId: {CorrelationId}.")]
    internal static partial void CommandRequestInvalid(ILogger logger, string assetId, string correlationId, Exception exception);
}
