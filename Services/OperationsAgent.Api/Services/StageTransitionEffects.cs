namespace OperationsAgent.Api.Services;

/// <summary>
/// Applies the side effects of a demo stage transition. Going backward fully restores the earlier
/// composition: pending interactive-input requests composed at a higher stage are cancelled (they
/// can never be approved and executed at a lower one), and the tool source returns to the local
/// function below the MCP stage, so re-entering MCP Tools starts from LOCAL as the lecture beat
/// expects.
/// </summary>
public sealed partial class StageTransitionEffects(
    PendingApprovalStore pendingApprovals,
    ToolSourceSwitch toolSourceSwitch,
    ILogger<StageTransitionEffects> logger)
{
    /// <summary>
    /// Applies the transition effects when the stage moved backward; forward moves are no-ops.
    /// </summary>
    /// <param name="previous">The stage before the transition.</param>
    /// <param name="current">The stage after the transition.</param>
    public void Apply(DemoStage previous, DemoStage current)
    {
        if (current >= previous)
        {
            return;
        }

        pendingApprovals.CancelAll();

        if (current < DemoStage.McpTools && toolSourceSwitch.Current != OperationsAgentToolSource.Local)
        {
            toolSourceSwitch.Current = OperationsAgentToolSource.Local;
            StageTransitionLog.ToolSourceReset(logger, current);
        }
    }
}

internal static partial class StageTransitionLog
{
    [LoggerMessage(
        EventId = 2633,
        Level = LogLevel.Information,
        Message = "Tool source reset to Local by the stage downgrade to {Stage}.")]
    internal static partial void ToolSourceReset(ILogger logger, DemoStage stage);
}
