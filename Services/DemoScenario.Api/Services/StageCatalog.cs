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
            "First Agent",
            "The general Caesarea Operations Agent can answer a simple question using one authoritative Energy Hub tool.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool"]),
        new(
            DemoStage.Session,
            "Session",
            "The agent keeps conversational context, so a follow-up like \"Why?\" refers to the previous question. Session state is not authoritative operational state.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups"]),
        new(
            DemoStage.Knowledge,
            "Knowledge",
            "The agent retrieves organizational work knowledge on demand - work orders and technician notes - so \"Why?\" gets an evidence-grounded explanation instead of a guess.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges"]),
        new(
            DemoStage.Memory,
            "Memory",
            "The agent recalls its own closed cases across sessions as hypotheses. Memory is never evidence: live state is still verified and real evidence still searched.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)"])
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
