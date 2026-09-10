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
    Hosting,

    /// <summary>
    /// ASSERT evaluates the cumulative local agent against repeatable streetlight scenarios,
    /// observing evidence, approval outcomes, and verified state changes through existing APIs.
    /// </summary>
    Evaluation
}

/// <summary>
/// Describes a demo stage available to the presenter switchboard.
/// </summary>
/// <param name="Id">The stage identifier.</param>
/// <param name="Name">The projector-friendly stage name.</param>
/// <param name="Description">The stage description shown in the presenter UI.</param>
/// <param name="Capabilities">The capabilities enabled once the stage is active.</param>
/// <param name="Walkthrough">What the presenter actually does at this stage.</param>
/// <param name="Feature">The MAF or Foundry mechanism this stage exists to show.</param>
public sealed record DemoStageDescriptor(
    DemoStage Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    DemoStageWalkthrough? Walkthrough = null,
    string? Feature = null);

/// <summary>
/// What a prerequisite refers to: a scenario fixture the director can apply, a switchboard switch
/// it can set, or something only the presenter can arrange.
/// </summary>
public enum DemoPrerequisiteKind
{
    /// <summary>A deterministic scenario the beat starts from.</summary>
    Scenario,

    /// <summary>A switchboard switch and the value the beat starts with.</summary>
    Switch,

    /// <summary>A precondition only the presenter can arrange, stated for them to check.</summary>
    Manual
}

/// <summary>
/// The presenter switches a beat can depend on. Each maps to one Operations Agent endpoint.
/// </summary>
public enum DemoSwitch
{
    /// <summary>Where the streetlight tool comes from: a local function or the MCP server.</summary>
    ToolSource,

    /// <summary>Whether the Operations Agent may consult the Security Operations Agent.</summary>
    SecurityConsult,

    /// <summary>Whether the seeded work-knowledge evidence is available to the agent.</summary>
    WorkKnowledge,

    /// <summary>Which habitat answers: the local Aspire agent or the Foundry-hosted twin.</summary>
    AgentHabitat
}

/// <summary>
/// The canonical values a <see cref="DemoSwitch"/> takes in a prerequisite and in a readiness
/// report, so the catalog, the director, and both web apps compare the same words.
/// </summary>
public static class DemoSwitchValues
{
    /// <summary>The streetlight tool is a local C# function.</summary>
    public const string ToolSourceLocal = "Local";

    /// <summary>The streetlight tool is discovered from the Energy Hub's MCP server.</summary>
    public const string ToolSourceMcp = "Mcp";

    /// <summary>A two-state switch is on.</summary>
    public const string On = "On";

    /// <summary>A two-state switch is off.</summary>
    public const string Off = "Off";

    /// <summary>The seeded work-knowledge evidence is available to the agent.</summary>
    public const string EvidencePresent = "Present";

    /// <summary>The seeded work-knowledge evidence is withheld from the agent.</summary>
    public const string EvidenceAbsent = "Absent";

    /// <summary>The local Aspire agent answers.</summary>
    public const string HabitatLocal = "Local";

    /// <summary>The Foundry-hosted twin answers.</summary>
    public const string HabitatFoundryHosted = "FoundryHosted";

    /// <summary>
    /// The presenter-facing name of a switch.
    /// </summary>
    public static string Describe(DemoSwitch @switch) => @switch switch
    {
        DemoSwitch.ToolSource => "Tools",
        DemoSwitch.SecurityConsult => "Security consult",
        DemoSwitch.WorkKnowledge => "Work knowledge evidence",
        DemoSwitch.AgentHabitat => "Habitat",
        _ => @switch.ToString()
    };
}

/// <summary>
/// The problem-details extension a stage-gated endpoint adds when it refuses a request.
/// </summary>
public static class DemoStageProblemExtensions
{
    /// <summary>
    /// The name of the <see cref="DemoStage"/> the refused capability requires, so a caller can
    /// offer to move there rather than leave the presenter to read the sentence.
    /// </summary>
    public const string RequiredStage = "requiredStage";
}

/// <summary>
/// One thing a beat needs before its first step. <see cref="Text"/> is the presenter-facing line;
/// the structured fields let the director check and, where it can, satisfy it. A prerequisite
/// with <see cref="AppliesAtStart"/> false belongs to a later step of the beat: it is shown, never
/// applied by preparation, and never counted against readiness.
/// </summary>
public sealed record DemoPrerequisite(
    DemoPrerequisiteKind Kind,
    string Text,
    ScenarioId? ScenarioId = null,
    DemoSwitch? Switch = null,
    string? RequiredValue = null,
    bool AppliesAtStart = true)
{
    /// <summary>A scenario fixture the beat starts from, or - when <paramref name="appliesAtStart"/> is false - one a later step applies.</summary>
    public static DemoPrerequisite ForScenario(ScenarioId scenarioId, string text, bool appliesAtStart = true) =>
        new(DemoPrerequisiteKind.Scenario, text, ScenarioId: scenarioId, AppliesAtStart: appliesAtStart);

    /// <summary>A switch and the value the beat starts with.</summary>
    public static DemoPrerequisite ForSwitch(DemoSwitch @switch, string requiredValue, string text) =>
        new(DemoPrerequisiteKind.Switch, text, Switch: @switch, RequiredValue: requiredValue);

    /// <summary>A precondition only the presenter can arrange.</summary>
    public static DemoPrerequisite ByHand(string text) => new(DemoPrerequisiteKind.Manual, text);
}

/// <summary>
/// A prerequisite checked against the live demo. <see cref="Satisfied"/> is <see langword="null"/>
/// when it cannot be checked: a manual precondition, or a switch that could not be read.
/// </summary>
public sealed record DemoPrerequisiteStatus(DemoPrerequisite Prerequisite, bool? Satisfied, string? CurrentValue);

/// <summary>
/// Whether a beat can be presented now: the stage is current and every prerequisite it starts
/// from is met or unknowable.
/// </summary>
public sealed record DemoStageReadiness(
    DemoStage Stage,
    string StageName,
    DemoStage CurrentStage,
    IReadOnlyList<DemoPrerequisiteStatus> Prerequisites)
{
    /// <summary>Whether the beat's stage is the one the demo is at.</summary>
    public bool StageIsCurrent => Stage == CurrentStage;

    /// <summary>
    /// The starting prerequisites that could be checked but were not - a switch or a fixture that
    /// could not be read. Unknown is never counted as met, so these keep a beat from reading as ready.
    /// </summary>
    public IReadOnlyList<DemoPrerequisiteStatus> Unverified =>
        [.. Prerequisites.Where(static status => IsCheckable(status.Prerequisite) && status.Satisfied is null)];

    /// <summary>
    /// Whether the beat can be presented now: its stage is current and every prerequisite it starts
    /// from that can be checked is verified met. A manual precondition is the presenter's to confirm.
    /// </summary>
    public bool Ready => StageIsCurrent
        && Prerequisites.All(static status => !IsCheckable(status.Prerequisite) || status.Satisfied == true);

    private static bool IsCheckable(DemoPrerequisite prerequisite) =>
        prerequisite is { AppliesAtStart: true, Kind: not DemoPrerequisiteKind.Manual };
}

/// <summary>
/// The outcome of preparing a beat: what the director did, in order, and where the beat stands now.
/// </summary>
public sealed record DemoStagePrepareResult(DemoStageReadiness Readiness, IReadOnlyList<string> Actions, string Summary);

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
    IReadOnlyList<DemoPrerequisite> Prerequisites,
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
    Code,

    /// <summary>
    /// A chat client to the hosted agent - Copilot, Teams, or the Foundry UI - for a question in
    /// the presenter's own words. Neither demo app has a free-text question box.
    /// </summary>
    HostedChat
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
/// <param name="Feature">The MAF or Foundry mechanism this stage exists to show.</param>
public sealed record DemoStageStatus(
    DemoStage Id,
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset AppliedAt,
    string CorrelationId,
    DemoStageWalkthrough? Walkthrough = null,
    string? Feature = null);

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
