using System.Text.Json;

namespace DemoScenario.Api.Services;

/// <summary>
/// Propagates presenter-controlled demo stage changes to the Command Center boundary.
/// </summary>
public interface ICommandCenterStageClient
{
    /// <summary>
    /// Reads the authoritative current stage from Command Center.
    /// </summary>
    public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the supplied demo stage to the Command Center.
    /// </summary>
    /// <param name="stage">The demo stage to propagate.</param>
    /// <param name="correlationId">The correlation identifier spanning the stage change.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task<DemoStageStatus> ApplyStageAsync(DemoStageStatus stage, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="ICommandCenterStageClient"/>
public sealed partial class HttpCommandCenterStageClient(HttpClient httpClient, ILogger<HttpCommandCenterStageClient> logger) : ICommandCenterStageClient
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/command-center/stage");
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DemoStageStatus>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Command Center returned an empty stage response.");
    }

    /// <inheritdoc />
    public async Task<DemoStageStatus> ApplyStageAsync(DemoStageStatus stage, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/command-center/admin/stage")
        {
            Content = JsonContent.Create(stage, options: SerializerOptions)
        };
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<DemoStageStatus>(SerializerOptions, cancellationToken)
                ?? throw new InvalidOperationException("Command Center returned an empty stage response.");
        }

        StageClientLog.PropagationFailed(logger, (int)response.StatusCode, correlationId);
        throw new HttpRequestException(
            $"Command Center stage propagation failed with status code {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }
}

/// <summary>
/// Propagates presenter-controlled demo stage changes to the Operations Agent boundary.
/// </summary>
public interface IOperationsAgentStageClient
{
    /// <summary>
    /// Applies the supplied demo stage to the Operations Agent.
    /// </summary>
    /// <param name="stage">The demo stage to propagate.</param>
    /// <param name="correlationId">The correlation identifier spanning the stage change.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task ApplyStageAsync(DemoStageStatus stage, string correlationId, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IOperationsAgentStageClient"/>
public sealed partial class HttpOperationsAgentStageClient(HttpClient httpClient, ILogger<HttpOperationsAgentStageClient> logger) : IOperationsAgentStageClient
{
    private static readonly JsonSerializerOptions SerializerOptions = CaesareaJsonDefaults.CreateSerializerOptions();

    /// <inheritdoc />
    public async Task ApplyStageAsync(DemoStageStatus stage, string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/operations-agent/demo-stage")
        {
            Content = JsonContent.Create(stage, options: SerializerOptions)
        };
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, correlationId);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            StageClientLog.AgentPropagationFailed(logger, (int)response.StatusCode, correlationId);
            throw new HttpRequestException(
                $"Operations Agent stage propagation failed with status code {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
    }
}

internal static partial class StageClientLog
{
    [LoggerMessage(
        EventId = 1950,
        Level = LogLevel.Warning,
        Message = "Command Center stage propagation failed with status {StatusCode}. CorrelationId: {CorrelationId}.")]
    internal static partial void PropagationFailed(ILogger logger, int statusCode, string correlationId);

    [LoggerMessage(
        EventId = 1951,
        Level = LogLevel.Warning,
        Message = "Operations Agent stage propagation failed with status {StatusCode}. CorrelationId: {CorrelationId}.")]
    internal static partial void AgentPropagationFailed(ILogger logger, int statusCode, string correlationId);
}
