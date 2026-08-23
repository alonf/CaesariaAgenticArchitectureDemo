namespace DemoScenario.Api.Services;

/// <summary>
/// Stores the demo stage definitions used by the presenter switchboard.
/// </summary>
public sealed class StageCatalog
{
    private static readonly DemoStageDescriptor[] Descriptors =
    [
        new(
            DemoStage.Deterministic,
            "Deterministic",
            "Only the deterministic Stage 0 capabilities are enabled. No agent, model, or AI credential is used.",
            ["Deterministic scenarios", "Manual operator actions"]),
        new(
            DemoStage.InvestigationAgent,
            "Investigation Agent",
            "The read-only Operations Agent can investigate the current anomaly using authoritative evidence.",
            ["Deterministic scenarios", "Manual operator actions", "Read-only Operations Agent investigation"])
    ];

    /// <summary>
    /// Gets the full demo stage catalog.
    /// </summary>
    /// <returns>The available demo stage descriptors.</returns>
    public IReadOnlyList<DemoStageDescriptor> GetAll() => Descriptors;

    /// <summary>
    /// Gets the presenter-facing descriptor for the supplied demo stage.
    /// </summary>
    /// <param name="stage">The demo stage to resolve.</param>
    /// <returns>The matching descriptor.</returns>
    public DemoStageDescriptor GetDescriptor(DemoStage stage) =>
        Descriptors.FirstOrDefault(candidate => candidate.Id == stage)
        ?? throw new ArgumentOutOfRangeException(nameof(stage), stage, "The requested demo stage is not defined.");
}
