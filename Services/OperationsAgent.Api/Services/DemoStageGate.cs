namespace OperationsAgent.Api.Services;

/// <summary>
/// Tracks the presenter-controlled demo stage propagated to the Operations Agent and gates agent
/// invocation so the Deterministic stage can never reach Microsoft Foundry.
/// </summary>
public sealed class DemoStageGate
{
    private readonly object _gate = new();
    private DemoStageStatus _currentStage;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoStageGate"/> class. The startup stage is
    /// stamped with <see cref="DateTimeOffset.MinValue"/> so any authoritative stage - pushed by the
    /// switchboard or read from the Command Center - supersedes it.
    /// </summary>
    /// <param name="initialStage">The stage assumed until an authoritative stage arrives.</param>
    public DemoStageGate(DemoStage initialStage)
    {
        _currentStage = new DemoStageStatus(
            initialStage,
            initialStage.ToString(),
            "Stage assumed at service startup until the presenter switchboard propagates a stage.",
            [],
            DateTimeOffset.MinValue,
            "startup");
    }

    /// <summary>
    /// Gets a value indicating whether the Operations Agent may be invoked in the current stage.
    /// </summary>
    public bool IsAgentEnabled => GetCurrent().Id >= DemoStage.InvestigationAgent;

    /// <summary>
    /// Gets the current demo stage.
    /// </summary>
    /// <returns>The current stage.</returns>
    public DemoStageStatus GetCurrent()
    {
        lock (_gate)
        {
            return _currentStage;
        }
    }

    /// <summary>
    /// Replaces the current demo stage with the supplied value, unless the supplied stage is older
    /// than the stored one - a stale startup read must never overwrite a newer pushed stage.
    /// </summary>
    /// <param name="stage">The stage to apply.</param>
    /// <returns>The stage that is current after the call.</returns>
    public DemoStageStatus SetCurrent(DemoStageStatus stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        lock (_gate)
        {
            if (stage.AppliedAt >= _currentStage.AppliedAt)
            {
                _currentStage = stage;
            }

            return _currentStage;
        }
    }
}
