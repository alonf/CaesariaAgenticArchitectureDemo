using System.ComponentModel;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Exposes the governed remediation workflow to the agent as an ordinary C# function tool. From
/// the Workflow stage the agent no longer performs the physical operation itself: it starts the
/// orchestration that owns validation, policy, approval, execution, and verification, and the
/// workflow reports its own progress.
/// </summary>
public sealed partial class RemediationTools(
    RemediationWorkflowService workflowService,
    string correlationId,
    ILogger<RemediationTools> logger)
{
    private readonly RemediationWorkflowService _workflowService =
        workflowService ?? throw new ArgumentNullException(nameof(workflowService));
    private readonly string _correlationId = string.IsNullOrWhiteSpace(correlationId)
        ? throw new ArgumentException("A correlation identifier is required.", nameof(correlationId))
        : correlationId;
    private readonly ILogger<RemediationTools> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Starts the Restore Lighting Operation workflow for a streetlight.
    /// </summary>
    /// <param name="assetId">The streetlight asset identifier.</param>
    /// <returns>A short confirmation naming the run that was started.</returns>
    [Description("Starts the governed Restore Lighting Operation workflow for a streetlight. The workflow re-validates authoritative state, applies operational policy, obtains operator approval when a manual override would be cleared, executes the restore, and verifies the physical result. Returns immediately with the run identifier; it does not wait for the run to finish.")]
    public string StartRestoreLightingOperation(
        [Description("The streetlight asset identifier, for example L-417.")] string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (!_workflowService.TryStartRun(assetId, _correlationId, out var report))
        {
            return $"A Restore Lighting Operation is already in progress for {assetId}; it must finish before another can start.";
        }

        RemediationToolsLog.WorkflowStarted(_logger, report.RunId, assetId, _correlationId);

        return $"Started Restore Lighting Operation {report.RunId} for {assetId}. The workflow is now validating authoritative state, applying policy, requesting operator approval if the policy requires it, executing, and verifying the result; its progress appears in the Command Center workflow panel.";
    }
}

internal static partial class RemediationToolsLog
{
    [LoggerMessage(
        EventId = 2646,
        Level = LogLevel.Information,
        Message = "Operations Agent started remediation workflow run {RunId} for asset {AssetId}. CorrelationId: {CorrelationId}.")]
    internal static partial void WorkflowStarted(ILogger logger, string runId, string assetId, string correlationId);
}
