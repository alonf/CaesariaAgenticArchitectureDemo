namespace DemoScenario.Contracts;

/// <summary>
/// Identifies a cumulative lecture stage that can be enabled for the running Caesarea demo.
/// </summary>
public enum DemoStage
{
    /// <summary>
    /// Only the deterministic Stage 0 capabilities are enabled.
    /// </summary>
    Deterministic,

    /// <summary>
    /// The general Caesarea Operations Agent and its first read-only Energy Hub tool are enabled.
    /// </summary>
    InvestigationAgent,

    /// <summary>
    /// The Operations Agent keeps conversational context across runs, so a follow-up question can
    /// refer to the previous turn. Session state is not authoritative operational state.
    /// </summary>
    Session,

    /// <summary>
    /// The Operations Agent can retrieve organizational work knowledge (work orders, technician
    /// notes) on demand to explain why an operational state exists.
    /// </summary>
    Knowledge,

    /// <summary>
    /// The Operations Agent recalls its own closed cases across sessions as hypotheses. Recalled
    /// memory is never evidence: live state must still be verified and real evidence searched.
    /// </summary>
    Memory,

    /// <summary>
    /// The Operations Agent discovers documented procedures (skills) and loads them on demand, so
    /// investigations follow the organization's expert-authored, auditable procedure.
    /// </summary>
    Skills,

    /// <summary>
    /// The streetlight tool can be served over the Model Context Protocol from the Energy Hub's
    /// own boundary: the agent discovers it at runtime instead of compiling it in. Same
    /// capability, new boundary - the presenter toggles between the local function and MCP.
    /// </summary>
    McpTools
}

/// <summary>
/// Describes a demo stage available to the presenter switchboard.
/// </summary>
/// <param name="Id">The stage identifier.</param>
/// <param name="Name">The projector-friendly stage name.</param>
/// <param name="Description">The stage description shown in the presenter UI.</param>
/// <param name="Capabilities">The capabilities enabled once the stage is active.</param>
public sealed record DemoStageDescriptor(
    DemoStage Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities);

/// <summary>
/// Represents the demo stage currently applied across the Caesarea services.
/// </summary>
/// <param name="Id">The current stage identifier.</param>
/// <param name="Name">The projector-friendly stage name.</param>
/// <param name="Description">The stage description shown in the UI.</param>
/// <param name="Capabilities">The capabilities enabled while this stage is current.</param>
/// <param name="AppliedAt">The time at which the stage was applied.</param>
/// <param name="CorrelationId">The correlation identifier spanning the stage change.</param>
public sealed record DemoStageStatus(
    DemoStage Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset AppliedAt,
    string CorrelationId);

/// <summary>
/// Represents the demo stage catalog returned to the presenter switchboard.
/// </summary>
/// <param name="Stages">The available demo stages.</param>
/// <param name="CurrentStage">The stage currently applied across the services.</param>
public sealed record DemoStageCatalogResponse(
    IReadOnlyList<DemoStageDescriptor> Stages,
    DemoStageStatus CurrentStage);

/// <summary>
/// Represents the result of switching the current demo stage.
/// </summary>
/// <param name="CurrentStage">The stage that is now current.</param>
/// <param name="Summary">The projector-friendly outcome summary.</param>
public sealed record DemoStageChangeResult(DemoStageStatus CurrentStage, string Summary);
