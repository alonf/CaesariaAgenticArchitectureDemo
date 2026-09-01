using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Provides correlated command access to the authoritative Energy Hub boundary for the explicit
/// remediation workflow. Reads stay on <see cref="IEnergyReadGateway"/>; this gateway exists so
/// the workflow's execute step is the only place a command can originate.
/// </summary>
public interface IEnergyCommandGateway
{
    /// <summary>
    /// Restores the supplied asset to its scheduled mode through the Energy Hub's deterministic
    /// restore workflow.
    /// </summary>
    /// <param name="assetId">The asset identifier to restore.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The command outcome, including failures reported by the Energy Hub.</returns>
    public Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IEnergyCommandGateway"/>
public sealed partial class HttpEnergyCommandGateway(HttpClient httpClient, ILogger<HttpEnergyCommandGateway> logger) : IEnergyCommandGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/energy/assets/{Uri.EscapeDataString(assetId)}/restore-scheduled-mode");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        // The Energy Hub reports command failures (timeout, superseded) as problem responses with
        // the command context attached; the workflow completes with that truth instead of
        // treating a failed restore as an unhandled exception.
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RestoreScheduledModeResult>(SerializerOptions, cancellationToken);
            return result ?? throw new InvalidOperationException("Energy Hub restore response was empty.");
        }

        var detail = await TryReadProblemDetailAsync(response, cancellationToken);
        HttpEnergyCommandGatewayLog.RestoreFailed(logger, assetId, correlationId, (int)response.StatusCode, detail ?? "No response detail was provided.");
        return new RestoreScheduledModeResult(
            assetId,
            null,
            null,
            CommandExecutionStatus.Failed,
            correlationId,
            detail ?? $"Energy Hub restore failed with status code {(int)response.StatusCode}.",
            DateTimeOffset.UtcNow);
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

internal static partial class HttpEnergyCommandGatewayLog
{
    [LoggerMessage(
        EventId = 2210,
        Level = LogLevel.Warning,
        Message = "Energy Hub restore for asset {AssetId} failed with status {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}")]
    internal static partial void RestoreFailed(ILogger logger, string assetId, string correlationId, int statusCode, string detail);
}
