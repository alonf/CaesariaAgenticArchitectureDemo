using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Provides read-only, correlated access to the Command Center boundary for the Operations Agent.
/// </summary>
public interface ICommandCenterReadGateway
{
    /// <summary>
    /// Reads the current customer-report evidence context for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current customer-report context.</returns>
    public Task<CustomerReportContext> GetCustomerReportContextAsync(string assetId, string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the current open-incident evidence context for the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The current incident context.</returns>
    public Task<IncidentContext> GetIncidentContextAsync(string assetId, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ICommandCenterReadGateway"/>
public sealed partial class HttpCommandCenterReadGateway(HttpClient httpClient, ILogger<HttpCommandCenterReadGateway> logger) : ICommandCenterReadGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public Task<CustomerReportContext> GetCustomerReportContextAsync(string assetId, string correlationId, CancellationToken cancellationToken) =>
        GetAsync<CustomerReportContext>(
            $"/api/command-center/customer-report/{Uri.EscapeDataString(assetId)}",
            "customer report",
            assetId,
            correlationId,
            cancellationToken);

    /// <inheritdoc />
    public Task<IncidentContext> GetIncidentContextAsync(string assetId, string correlationId, CancellationToken cancellationToken) =>
        GetAsync<IncidentContext>(
            $"/api/command-center/incidents/current/{Uri.EscapeDataString(assetId)}",
            "incident context",
            assetId,
            correlationId,
            cancellationToken);

    private async Task<T> GetAsync<T>(string relativeUri, string evidenceName, string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
            request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await TryReadProblemDetailAsync(response, cancellationToken);
                HttpCommandCenterReadGatewayLog.RequestFailed(logger, evidenceName, assetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
                throw new HttpRequestException($"Command Center {evidenceName} read failed with status code {(int)response.StatusCode}.", null, response.StatusCode);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken)
                    ?? throw new InvalidOperationException($"Command Center {evidenceName} response was empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"Command Center {evidenceName} response payload was invalid.", exception);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            HttpCommandCenterReadGatewayLog.RequestTimedOut(logger, evidenceName, assetId, correlationId, exception);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            HttpCommandCenterReadGatewayLog.RequestHttpError(logger, evidenceName, assetId, correlationId, exception);
            throw;
        }
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

internal static partial class HttpCommandCenterReadGatewayLog
{
    [LoggerMessage(
        EventId = 2250,
        Level = LogLevel.Warning,
        Message = "Command Center {EvidenceName} read for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void RequestFailed(ILogger logger, string evidenceName, string assetId, string correlationId, int statusCode, string detail);

    [LoggerMessage(
        EventId = 2251,
        Level = LogLevel.Warning,
        Message = "Command Center {EvidenceName} read for asset {AssetId} timed out. CorrelationId: {CorrelationId}.")]
    internal static partial void RequestTimedOut(ILogger logger, string evidenceName, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2252,
        Level = LogLevel.Error,
        Message = "Command Center {EvidenceName} read for asset {AssetId} failed before a response was received. CorrelationId: {CorrelationId}.")]
    internal static partial void RequestHttpError(ILogger logger, string evidenceName, string assetId, string correlationId, Exception exception);
}
