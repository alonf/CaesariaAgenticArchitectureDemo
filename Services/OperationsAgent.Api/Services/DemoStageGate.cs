namespace OperationsAgent.Api.Services;

/// <summary>
/// Tracks the presenter-controlled demo stage propagated to the Operations Agent and gates agent
/// invocation: requests cannot reach Microsoft Foundry while the gate holds the Deterministic
/// stage. The gate reflects the authoritative stage once it has been pushed by the switchboard or
/// successfully reconciled from the Command Center; until then it holds the configured startup
/// stage or the last known one.
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

    /// <summary>
    /// Runs a capability's side effect only while the stage still permits it, with the check and
    /// the effect in one critical section. Checking the stage and then acting leaves a window in
    /// which a downgrade lands between the two, and for a write that window is the difference
    /// between "the capability was withdrawn" and "the capability was withdrawn but it wrote
    /// anyway". The effect must be short and must not block: it runs while the gate is held.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="minimum">The stage the capability requires.</param>
    /// <param name="effect">The side effect to perform while the stage holds.</param>
    /// <param name="result">The effect's result, when it ran.</param>
    /// <returns><see langword="false"/> when the stage no longer permits the capability.</returns>
    public bool TryExecuteAtLeast<T>(DemoStage minimum, Func<T> effect, out T result)
    {
        ArgumentNullException.ThrowIfNull(effect);

        lock (_gate)
        {
            if (_currentStage.Id < minimum)
            {
                result = default!;
                return false;
            }

            result = effect();
            return true;
        }
    }
}
