using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Provides read-only, correlated access to the authoritative Energy Hub boundary for the Operations Agent.
/// </summary>
public interface IEnergyReadGateway
{
    /// <summary>
    /// Reads the current authoritative Energy Hub state for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The authoritative operational twin.</returns>
    public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads recent authoritative Energy Hub activity for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="limit">The maximum number of activity records to return.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The recent activity ordered from newest to oldest.</returns>
    public Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IEnergyReadGateway"/>
public sealed partial class HttpEnergyReadGateway(HttpClient httpClient, ILogger<HttpEnergyReadGateway> logger) : IEnergyReadGateway
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
                HttpEnergyReadGatewayLog.StateRequestFailed(logger, assetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"Energy Hub state read failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
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
            HttpEnergyReadGatewayLog.StateRequestTimedOut(logger, assetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            HttpEnergyReadGatewayLog.StateRequestHttpError(logger, assetId, correlationId, exception);
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
                HttpEnergyReadGatewayLog.ActivityRequestFailed(logger, assetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"Energy Hub activity read failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
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
            HttpEnergyReadGatewayLog.ActivityRequestTimedOut(logger, assetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            HttpEnergyReadGatewayLog.ActivityRequestHttpError(logger, assetId, correlationId, exception);
            throw;
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUri, string correlationId)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        return request;
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(SerializerOptions, cancellationToken);
            return problem?.Detail ?? problem?.Title;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(rawContent) ? null : rawContent;
        }
    }
}

internal static partial class HttpEnergyReadGatewayLog
{
    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Warning,
        Message = "Energy Hub state read for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void StateRequestFailed(ILogger logger, string assetId, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Warning,
        Message = "Energy Hub state read for asset {AssetId} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Error,
        Message = "Energy Hub state read for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void StateRequestHttpError(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Warning,
        Message = "Energy Hub activity read for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void ActivityRequestFailed(ILogger logger, string assetId, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 2204,
        Level = LogLevel.Warning,
        Message = "Energy Hub activity read for asset {AssetId} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void ActivityRequestTimedOut(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2205,
        Level = LogLevel.Error,
        Message = "Energy Hub activity read for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void ActivityRequestHttpError(ILogger logger, string assetId, string correlationId, Exception exception);
}
