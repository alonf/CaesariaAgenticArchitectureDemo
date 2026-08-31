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
    /// Initializes a new instance of the <see cref="DemoStageGate"/> class.
    /// </summary>
    /// <param name="timeProvider">The clock used to stamp the startup stage.</param>
    /// <param name="initialStage">The stage assumed until the switchboard propagates one.</param>
    public DemoStageGate(TimeProvider timeProvider, DemoStage initialStage)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _currentStage = new DemoStageStatus(
            initialStage,
            initialStage.ToString(),
            "Stage assumed at service startup until the presenter switchboard propagates a stage.",
            [],
            timeProvider.GetUtcNow(),
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
    /// Replaces the current demo stage with the supplied value.
    /// </summary>
    /// <param name="stage">The new current stage.</param>
    /// <returns>The stored stage.</returns>
    public DemoStageStatus SetCurrent(DemoStageStatus stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        lock (_gate)
        {
            _currentStage = stage;
            return _currentStage;
        }
    }
}
