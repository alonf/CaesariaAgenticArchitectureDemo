namespace SecurityHub.Api.Services;

/// <summary>
/// Owns the authoritative record of active security operations. This is a deterministic city
/// system like any other hub - the difference is who may read it: the records carry restricted
/// operational detail, so only the Security domain's own agent is given access.
/// </summary>
public sealed partial class SecurityHubService(TimeProvider timeProvider, ILogger<SecurityHubService> logger)
{
    private readonly Lock _gate = new();
    private List<SecurityOperationRecord> _operations = [];

    /// <summary>
    /// Gets the operations currently active in an area.
    /// </summary>
    /// <param name="area">The area to query.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns>The area's security status.</returns>
    public SecurityAreaStatus GetAreaStatus(string area, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var now = timeProvider.GetUtcNow();

        lock (_gate)
        {
            var active = _operations
                .Where(operation => string.Equals(operation.Area, area, StringComparison.OrdinalIgnoreCase))
                .Where(operation => operation.StartedAt <= now && now <= operation.EndsAt)
                .ToArray();

            SecurityHubLog.AreaQueried(logger, area, active.Length, correlationId);
            return new SecurityAreaStatus(area, active, now);
        }
    }

    /// <summary>
    /// Replaces the active operations with the ones a deterministic scenario asserts.
    /// </summary>
    /// <param name="request">The scenario synchronization request.</param>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns>The resulting status for the affected area, or an empty status when none was supplied.</returns>
    public SecurityAreaStatus ApplyScenario(SecurityScenarioSyncRequest request, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_gate)
        {
            _operations = [.. request.Operations];
            SecurityHubLog.ScenarioApplied(logger, request.Operations.Count, correlationId);
        }

        var area = request.Operations.Count > 0 ? request.Operations[0].Area : DemoAssets.NorthPromenadeArea;
        return GetAreaStatus(area, correlationId);
    }

    /// <summary>
    /// Clears every recorded operation.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the request.</param>
    /// <returns>The resulting status for the demo's area.</returns>
    public SecurityAreaStatus Reset(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_gate)
        {
            _operations = [];
            SecurityHubLog.Reset(logger, correlationId);
        }

        return GetAreaStatus(DemoAssets.NorthPromenadeArea, correlationId);
    }
}

internal static partial class SecurityHubLog
{
    [LoggerMessage(
        EventId = 1700,
        Level = LogLevel.Information,
        Message = "Security Hub reported {OperationCount} active operation(s) in {Area}. CorrelationId: {CorrelationId}.")]
    internal static partial void AreaQueried(ILogger logger, string area, int operationCount, string correlationId);

    [LoggerMessage(
        EventId = 1701,
        Level = LogLevel.Information,
        Message = "Security Hub synchronized to a scenario with {OperationCount} operation(s). CorrelationId: {CorrelationId}.")]
    internal static partial void ScenarioApplied(ILogger logger, int operationCount, string correlationId);

    [LoggerMessage(
        EventId = 1702,
        Level = LogLevel.Information,
        Message = "Security Hub reset. CorrelationId: {CorrelationId}.")]
    internal static partial void Reset(ILogger logger, string correlationId);
}
