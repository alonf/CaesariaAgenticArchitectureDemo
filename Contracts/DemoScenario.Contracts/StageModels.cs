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
    McpTools,

    /// <summary>
    /// The first write-capable tool arrives: restore_scheduled_mode over MCP, using Multi
    /// Round-Trip Requests (MRTR). The tool pauses input-required for explicit operator approval
    /// and produces no side effect before the input arrives.
    /// </summary>
    InteractiveInput,

    /// <summary>
    /// Remediation becomes an explicit workflow: validate, policy, an approval gate when the
    /// policy demands one, execute, verify - deterministic orchestration with visible steps and
    /// branching, instead of emergent model behavior.
    /// </summary>
    Workflow,

    /// <summary>
    /// The third human-control point: a sensitive capability the model may choose for itself.
    /// Filing a maintenance work item is wrapped so the framework intercepts the call and a
    /// supervisor approves it - reactive control, where the model picks the path and policy
    /// decides whether it may proceed.
    /// </summary>
    ToolApproval,

    /// <summary>
    /// A second agent for a real boundary: Security is a distinct permission and context
    /// boundary, so the Operations Agent consults a Security Operations Agent that reads records
    /// it cannot, and receives a sanitized judgment while keeping ownership of the answer.
    /// </summary>
    MultiAgent,

    /// <summary>
    /// A peer agent in another domain, discovered by its published card and given a task over A2A
    /// rather than called as a tool. The workforce domain holds work orders whose commercial and
    /// personal detail may not cross; its agent never receives those fields, so it can answer
    /// freely and cannot be talked into disclosing what it never held.
    /// </summary>
    A2ADelegation,

    /// <summary>
    /// The same Operations Agent, hosted by Microsoft Foundry instead of by this process. The
    /// presenter switches the Command Center between the two habitats: the hosted twin runs the
    /// same code against the cloud Energy Hub, and reaches the real work order in the presenter's
    /// own Microsoft 365 through Work IQ - asking as the signed-in person, never as an
    /// all-reading application.
    /// </summary>
    Hosting
}

/// <summary>
/// Describes a demo stage available to the presenter switchboard.
/// </summary>
/// <param name="Id">The stage identifier.</param>
/// <param name="Name">The projector-friendly stage name.</param>
/// <param name="Description">The stage description shown in the presenter UI.</param>
/// <param name="Capabilities">The capabilities enabled once the stage is active.</param>
/// <param name="Walkthrough">What the presenter actually does at this stage.</param>
public sealed record DemoStageDescriptor(
    DemoStage Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    DemoStageWalkthrough? Walkthrough = null);

/// <summary>
/// The presenter's script for one stage. The Command Center's action buttons cannot convey this on
/// their own: several stages keep the same button and change switchboard state instead - Tools
/// LOCAL/MCP, the security consult, withheld evidence - and one stage needs Tools: MCP as a silent
/// precondition, so pressing the button with the wrong state simply does nothing interesting.
/// </summary>
/// <param name="Prerequisites">Switchboard state the beat depends on, in the order to set it.</param>
/// <param name="Steps">What to do, in order.</param>
/// <param name="Point">The one line this stage exists to land.</param>
public sealed record DemoStageWalkthrough(
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<DemoStageWalkthroughStep> Steps,
    string Point);

/// <summary>
/// One step of a stage walkthrough.
/// </summary>
/// <param name="Surface">Where the step happens.</param>
/// <param name="Action">What the presenter does.</param>
/// <param name="Expect">What should appear as a result, when the step has a visible outcome.</param>
public sealed record DemoStageWalkthroughStep(
    DemoSurface Surface,
    string Action,
    string? Expect = null);

/// <summary>
/// The screen a walkthrough step happens on.
/// </summary>
public enum DemoSurface
{
    /// <summary>The Command Center operations view.</summary>
    CommandCenter,

    /// <summary>The presenter switchboard (DemoControl).</summary>
    Switchboard,

    /// <summary>The editor or the repository - a code or file step.</summary>
    Code
}

/// <summary>
/// Represents the demo stage currently applied across the Caesarea services.
/// </summary>
/// <param name="Id">The current stage identifier.</param>
/// <param name="Name">The projector-friendly stage name.</param>
/// <param name="Description">The stage description shown in the UI.</param>
/// <param name="Capabilities">The capabilities enabled while this stage is current.</param>
/// <param name="AppliedAt">The time at which the stage was applied.</param>
/// <param name="CorrelationId">The correlation identifier spanning the stage change.</param>
/// <param name="Walkthrough">What the presenter does at this stage, for the walkthrough panel.</param>
public sealed record DemoStageStatus(
    DemoStage Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset AppliedAt,
    string CorrelationId,
    DemoStageWalkthrough? Walkthrough = null);

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
