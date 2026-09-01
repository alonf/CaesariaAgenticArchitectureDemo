using System.Net;
using System.Text.Json;

namespace OperationsAgent.Api.Services;

/// <summary>
/// The outcome of one Energy Hub command, distinguishing a refused precondition from a genuine
/// downstream failure: the first means "your picture is stale, look again", the second means
/// "the city did not do what you asked".
/// </summary>
/// <param name="Status">The command status reported by the Energy Hub.</param>
/// <param name="Summary">The projector-friendly summary.</param>
/// <param name="PreconditionFailed">Whether the command was refused because the validated state revision is no longer current.</param>
public sealed record EnergyCommandOutcome(CommandExecutionStatus Status, string Summary, bool PreconditionFailed);

/// <summary>
/// Provides correlated command access to the authoritative Energy Hub boundary for the explicit
/// remediation workflow. Reads stay on <see cref="IEnergyReadGateway"/>; this gateway exists so
/// the workflow's execute step is the only place a command can originate.
/// </summary>
public interface IEnergyCommandGateway
{
    /// <summary>
    /// Restores the supplied asset to its scheduled mode, but only while the state the caller
    /// validated is still current.
    /// </summary>
    /// <param name="assetId">The asset identifier to restore.</param>
    /// <param name="correlationId">The correlation identifier spanning the end-to-end request.</param>
    /// <param name="expectedStateRevision">The authoritative state revision the decision was made against.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The command outcome, including a refused precondition.</returns>
    public Task<EnergyCommandOutcome> RestoreScheduledModeAsync(
        string assetId,
        string correlationId,
        long expectedStateRevision,
        CancellationToken cancellationToken);
}

/// <inheritdoc cref="IEnergyCommandGateway"/>
public sealed partial class HttpEnergyCommandGateway(HttpClient httpClient, ILogger<HttpEnergyCommandGateway> logger) : IEnergyCommandGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<EnergyCommandOutcome> RestoreScheduledModeAsync(
        string assetId,
        string correlationId,
        long expectedStateRevision,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/energy/assets/{Uri.EscapeDataString(assetId)}/restore-scheduled-mode?expectedStateRevision={expectedStateRevision}");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RestoreScheduledModeResult>(SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Energy Hub restore response was empty.");
            return new EnergyCommandOutcome(result.Status, result.Summary, PreconditionFailed: false);
        }

        var problem = await TryReadProblemAsync(response, cancellationToken);
        var preconditionFailed = response.StatusCode == HttpStatusCode.Conflict
            && problem?.Extensions.TryGetValue(EnergyCommandProblem.PreconditionFailedExtension, out var flag) == true
            && flag is JsonElement { ValueKind: JsonValueKind.True };
        var summary = problem?.Detail ?? problem?.Title ?? $"Energy Hub restore failed with status code {(int)response.StatusCode}.";

        if (preconditionFailed)
        {
            HttpEnergyCommandGatewayLog.PreconditionRefused(logger, assetId, correlationId, expectedStateRevision);
        }
        else
        {
            HttpEnergyCommandGatewayLog.RestoreFailed(logger, assetId, correlationId, (int)response.StatusCode, summary);
        }

        return new EnergyCommandOutcome(CommandExecutionStatus.Failed, summary, preconditionFailed);
    }

    private static async Task<Microsoft.AspNetCore.Mvc.ProblemDetails?> TryReadProblemAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
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

    [LoggerMessage(
        EventId = 2211,
        Level = LogLevel.Warning,
        Message = "Energy Hub refused the restore for asset {AssetId}: state revision {ExpectedRevision} is no longer current. CorrelationId: {CorrelationId}.")]
    internal static partial void PreconditionRefused(ILogger logger, string assetId, string correlationId, long expectedRevision);
}
