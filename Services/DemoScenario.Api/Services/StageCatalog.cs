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
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)"]),
        new(
            DemoStage.Skills,
            "Skills",
            "The agent discovers documented procedures and loads them on demand (load_skill), so an investigation follows the organization's expert-authored, auditable procedure - including the mandated incident-brief format.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure"]),
        new(
            DemoStage.McpTools,
            "MCP Tools",
            "The streetlight tool can be served over the Model Context Protocol from the Energy Hub's own boundary. Flip Tools: LOCAL to MCP and re-ask: same capability, same behavior, new boundary.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)"]),
        new(
            DemoStage.InteractiveInput,
            "Interactive Input",
            "The first write-capable tool: restore_scheduled_mode over MCP with Multi Round-Trip Requests. The tool pauses input-required for explicit operator approval - no side effect before the input arrives. Requires Tools: MCP.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)", "restore_scheduled_mode with MRTR operator approval"]),
        new(
            DemoStage.Workflow,
            "Workflow",
            "Remediation becomes an explicit code-built workflow: validate, policy, an operator-approval gate when a manual override would be cleared, execute, verify. Deterministic orchestration with visible steps and branching - the graph renders its own diagram.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)", "restore_scheduled_mode with MRTR operator approval", "Explicit remediation workflow (validate / policy / approval / execute / verify)"]),
        new(
            DemoStage.ToolApproval,
            "Tool Approval",
            "The third human-control point, and the reactive one: the agent may decide by itself to file a maintenance work item, and the framework intercepts that call so a supervisor approves before it runs. The model chooses the path; policy decides whether it may proceed.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)", "restore_scheduled_mode with MRTR operator approval", "Explicit remediation workflow (validate / policy / approval / execute / verify)", "create_maintenance_work_item behind ApprovalRequiredAIFunction", "get_incident lookup - existing work found before new work is filed"]),
        new(
            DemoStage.MultiAgent,
            "Multi-Agent",
            "A second agent for a real boundary: Security owns records the Operations Agent may not read, so it consults the Security Operations Agent and receives a sanitized judgment while keeping ownership of the answer. Turn the consult off and the same question is answered without attribution - the lamp is intentional, but the reason belongs to a domain that will not disclose it.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)", "restore_scheduled_mode with MRTR operator approval", "Explicit remediation workflow (validate / policy / approval / execute / verify)", "create_maintenance_work_item behind ApprovalRequiredAIFunction", "get_incident lookup - existing work found before new work is filed", "Security consult ON / OFF (delegation to a second agent)"]),
        new(
            DemoStage.A2ADelegation,
            "A2A Delegation",
            "A peer agent in another domain, discovered by its agent card and given a task over A2A rather than called as a tool. The workforce domain's work orders carry commercial and personal detail that may not cross; its agent is handed only the shareable projection, so it answers freely and cannot disclose what it never held.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)", "restore_scheduled_mode with MRTR operator approval", "Explicit remediation workflow (validate / policy / approval / execute / verify)", "create_maintenance_work_item behind ApprovalRequiredAIFunction", "get_incident lookup - existing work found before new work is filed", "Security consult ON / OFF (delegation to a second agent)", "Workforce Agent consulted over A2A (agent card discovery, delegated task)"]),
        new(
            DemoStage.Hosting,
            "Hosting",
            "The same Operations Agent, hosted by Microsoft Foundry. Flip Habitat: LOCAL to FOUNDRY HOSTED and ask again: the platform owns the runtime, the identity and the endpoint, the Energy Hub it reads is the deployed one, and the work-order evidence comes from the presenter's own OneDrive through Work IQ - retrieved as the signed-in person.",
            ["Deterministic scenarios", "Manual operator actions", "Caesarea Operations Agent", "get_streetlight_state tool", "AgentSession follow-ups", "search_work_knowledge retrieval", "Retrieved-evidence trace with citation badges", "Case-memory recall (hypotheses)", "Skill discovery and load_skill procedure", "Tools: LOCAL / MCP toggle (runtime tool discovery)", "restore_scheduled_mode with MRTR operator approval", "Explicit remediation workflow (validate / policy / approval / execute / verify)", "create_maintenance_work_item behind ApprovalRequiredAIFunction", "get_incident lookup - existing work found before new work is filed", "Security consult ON / OFF (delegation to a second agent)", "Workforce Agent consulted over A2A (agent card discovery, delegated task)", "Habitat: LOCAL / FOUNDRY HOSTED toggle (same agent, platform-owned runtime, Work IQ evidence)"]),
        new(
            DemoStage.Evaluation,
            "Evaluation",
            "ASSERT compares the capability-limited Session baseline with the cumulative local agent on the same frozen L-417 cases. Inspect behavioral judgments alongside deterministic execution evidence.",
            ["All local agent capabilities", "ASSERT behavior specification and frozen cases", "Correlated execution evidence", "Baseline / governed comparison"])
    ];

    // The presenter's script per stage, kept beside the capability list it belongs to. The Command
    // Center's buttons cannot convey this alone: several stages keep the same button and change
    // switchboard state instead, and InteractiveInput needs Tools: MCP as a silent precondition.
    private static readonly Dictionary<DemoStage, DemoStageWalkthrough> Walkthroughs = new()
    {
        [DemoStage.Evaluation] = new(
            [],
            [
                new(DemoSurface.Switchboard, "Open evaluation/assert_demo/cases.json and the ASSERT evaluation guide.", "The required and forbidden behavior is reviewable before any model runs."),
                new(DemoSurface.Switchboard, "Run scripts/Invoke-AssertDemo.ps1 -Arm both in a terminal against the local demo.", "The harness resets each fixture, scripts its supervisor decision, and runs the same cases through both compositions."),
                new(DemoSurface.CommandCenter, "Open the generated report.html and expand a failed case's evidence.", "Compare the answer, ordered tool outcomes, approval decisions, and verified Hub state; errors are never passes.")
            ],
            "Unit tests prove the operation works. Behavioral evaluation measures whether the agent requested the right operation, using the right evidence and authority."),
        [DemoStage.Deterministic] = new(
            [DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client")],
            [
                new(DemoSurface.CommandCenter, "Show L-417 on the map: reported On, schedule Off.", "Assets requiring attention: 1, and the incoming customer report."),
                new(DemoSurface.CommandCenter, "Click Restore Scheduled Mode.", "SmartPole confirms, the lamp goes off, the timeline records the correlated command."),
                new(DemoSurface.CommandCenter, "Point at the Active incident panel.", "No incident: the anomaly is exposed, but nothing investigated why.")
            ],
            "The deterministic platform operates the city. Everything agentic we add later participates in this architecture rather than bypassing it."),

        [DemoStage.InvestigationAgent] = new(
            [DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client")],
            [
                new(DemoSurface.CommandCenter, "Click Ask agent - \"Is streetlight L-417 on?\".", "One tool call, get_streetlight_state, marked ran."),
                new(DemoSurface.Code, "Show FoundryOperationsAgent.cs, then Capabilities/StreetlightToolsCapability.cs and EnergyTools.cs.", "One general agent; the tool is an ordinary C# method, registered by the one capability this stage has."),
                new(DemoSurface.CommandCenter, "Point out that the deterministic panels are unchanged.", null)
            ],
            "An agent is a model, tools and instructions. The model chose the tool; the Energy Hub still owns the answer."),

        [DemoStage.Session] = new(
            [DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client")],
            [
                new(DemoSurface.CommandCenter, "Click Ask agent.", "One tool call over 2 round trips."),
                new(DemoSurface.CommandCenter, "Click Ask \"Why?\" (same session).", "Zero tool calls, one round trip - resolved from session context alone.")
            ],
            "Conversation continuity is useful context, not evidence about reality: the agent did not re-check the city."),

        [DemoStage.Knowledge] = new(
            [
                DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client"),
                DemoPrerequisite.ForSwitch(DemoSwitch.WorkKnowledge, DemoSwitchValues.EvidencePresent, "Work knowledge: evidence present")
            ],
            [
                new(DemoSurface.CommandCenter, "Click Ask agent, then Ask \"Why?\".", "The trace adds search_work_knowledge; evidence cards appear with Cited in answer badges."),
                new(DemoSurface.CommandCenter, "Open the WO-8732 technician note.", "\"Left the light ON for post-maintenance verification.\""),
                new(DemoSurface.Switchboard, "Optional: uncheck Work Knowledge evidence, then re-ask in a new session.", "The agent reports the search found nothing and never invents a ticket.")
            ],
            "Retrieval is not citation: the cards show what the search returned, the badge shows what the agent relied on."),

        [DemoStage.Memory] = new(
            [
                DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client"),
                DemoPrerequisite.ByHand("Arrive from the Knowledge beat, with L-417 already explained")
            ],
            [
                new(DemoSurface.CommandCenter, "Click Close case (record to memory).", "Recorded as CASE-1."),
                new(DemoSurface.CommandCenter, "Click Ask about L-528 (new session).", "CASE-1 recalled as a hypothesis; L-528 still read live, work knowledge still searched, nothing found.")
            ],
            "Memory is not evidence: green cards are the organization's records, the amber card is the agent's own past conclusion."),

        [DemoStage.Skills] = new(
            [DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client")],
            [
                new(DemoSurface.CommandCenter, "Click Investigate L-417 (new session).", "load_skill runs first; the answer arrives as the branded incident brief."),
                new(DemoSurface.Code, "Show SKILL.md - the triage table and the mandated format.", null),
                new(DemoSurface.Code, "Optional: edit one rule in the markdown and re-run.", "The brief obeys the edit.")
            ],
            "Same model, same tools, same question. The difference is a markdown file in git - skills are ops-owned, auditable configuration."),

        [DemoStage.McpTools] = new(
            [
                DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client"),
                DemoPrerequisite.ForSwitch(DemoSwitch.ToolSource, DemoSwitchValues.ToolSourceLocal, "Tools: LOCAL to start")
            ],
            [
                new(DemoSurface.CommandCenter, "Click Ask agent.", "The familiar local trace."),
                new(DemoSurface.Switchboard, "Flip Tools: LOCAL to MCP.", null),
                new(DemoSurface.CommandCenter, "Click the same button again.", "Identical answer and tool name, now with the MCP remote badge.")
            ],
            "Same capability, same behavior, new boundary: the tool was discovered over the protocol at runtime."),

        [DemoStage.InteractiveInput] = new(
            [
                DemoPrerequisite.ForSwitch(DemoSwitch.ToolSource, DemoSwitchValues.ToolSourceMcp, "Tools: MCP - required, the restore tool only arrives over MCP"),
                DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client")
            ],
            [
                new(DemoSurface.CommandCenter, "Click Restore L-417 to scheduled mode (agent).", "The Operator input required panel appears: the tool is paused mid-execution."),
                new(DemoSurface.CommandCenter, "Deny first.", "The answer reports the cancellation; the map still shows the override."),
                new(DemoSurface.CommandCenter, "Click again and approve.", "The restore executes, SmartPole confirms, the map updates.")
            ],
            "MRTR is the tool asking for input mid-execution. The next stage shows the client gating the call before the tool runs."),

        [DemoStage.Workflow] = new(
            [DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client")],
            [
                new(DemoSurface.CommandCenter, "Open the workflow definition card.", "The diagram is generated from the code that runs - two policy branches and the work-item branch."),
                new(DemoSurface.CommandCenter, "Click Run remediation workflow, then deny at the gate.", "Gate Declined, execute Skipped, verify Unresolved - a refusal never paints green and raises no work item."),
                new(DemoSurface.CommandCenter, "Run again and approve.", "The timeline fills node by node, the outcome reads SUCCEEDED, the map updates."),
                new(DemoSurface.Switchboard, "Optional: arm the WORKFLOW breakpoint before running.", "The debugger lands at the graph builder."),
                new(DemoSurface.Switchboard, "Optional: apply Security Operation and run again.", "Nothing happens - required lighting is not an anomaly.")
            ],
            "The agent recommends; the workflow decides and acts. Orchestration is explicit and inspectable, not emergent."),

        [DemoStage.ToolApproval] = new(
            [
                DemoPrerequisite.ForScenario(ScenarioId.ControllerFault, "Scenario: Controller Fault - the controller really is faulted, so the agent can verify the report before acting"),
                // The last step's fixture, not the first: shown to the presenter, never applied by preparation.
                DemoPrerequisite.ForScenario(ScenarioId.ExistingIncident, "Scenario: Existing Incident for the last step - the same fault, already tracked by INC-L417-001", appliesAtStart: false)
            ],
            [
                new(DemoSurface.CommandCenter, "Click Investigate the reported controller fault on L-417.", "The request never names a tool - the agent checks the state, finds the fault, and picks create_maintenance_work_item itself."),
                new(DemoSurface.CommandCenter, "Deny first.", "The trace marks the call denied - did not run, and the work-item list stays empty."),
                new(DemoSurface.CommandCenter, "Ask again and approve.", "The same call runs and exactly one work item appears."),
                new(DemoSurface.Switchboard, "Apply Existing Incident, then click the same button again.", "The state names INC-L417-001 and get_incident finds a dispatch already pending - no approval prompt, nothing filed. The agent looked before it asked.")
            ],
            "Three control points, one operator experience: the tool asked, then a node we drew, now the model chose and policy intercepted - and when the work already exists, the model finds it and asks for nothing."),

        [DemoStage.A2ADelegation] = new(
            [DemoPrerequisite.ForScenario(ScenarioId.ForgottenOverride, "Scenario: Lights On Reported by a Client - the open work order explains the override")],
            [
                new(DemoSurface.CommandCenter, "Click Ask the workforce domain about L-417.", "The agent card is resolved first, then a task is delegated: the peer searches its work orders, picks the open one and explains the override."),
                new(DemoSurface.CommandCenter, "Read the Consulted peer block: who answered, which domain runs them, and the skill's own description of what it does not hold.", "A named agent with a provider - not an anonymous endpoint, and not a tool in this agent's toolbox. The card states the limit; the tool is what enforces it."),
                new(DemoSurface.CommandCenter, "Now click Ask the workforce domain for the technician cost.", "It does not refuse on policy - it answers that the cost is not visible in the records it can access, because those fields never entered its context."),
                new(DemoSurface.Switchboard, "Show the work order in full on the Workforce Hub view.", "Technician name, badge, labour cost and rate - all of it withheld, none of it ever sent to the agent.")
            ],
            "Do not ask a model to keep a secret it holds. Hand it only what may cross, and there is nothing left to extract - that is a tool-boundary decision, not a prompt."),

        [DemoStage.MultiAgent] = new(
            [
                DemoPrerequisite.ForScenario(ScenarioId.SecurityOperation, "Scenario: Security Operation"),
                DemoPrerequisite.ForSwitch(DemoSwitch.SecurityConsult, DemoSwitchValues.Off, "Security consult: OFF to start")
            ],
            [
                new(DemoSurface.CommandCenter, "Click Why is L-417 on during daylight? (new session).", "A correct but thin answer: an external directive requires lighting, requesting domain not disclosed."),
                new(DemoSurface.Switchboard, "Turn the Security consult ON.", null),
                new(DemoSurface.CommandCenter, "Click the same button again.", "assess_lighting_requirement marked ran, plus a Consulted specialist block with the verdict, the recommendation and \"Operational details are withheld\".")
            ],
            "The second agent did not add a capability - it added authority to know something. The Operations Agent has no route to the Security Hub at all."),

        [DemoStage.Hosting] = new(
            [
                DemoPrerequisite.ByHand("The hosted agent is deployed (deploy-hosted-agent.yml) and the Command Center knows its project endpoint (CommandCenterWeb:HostedAgent)"),
                DemoPrerequisite.ByHand("The work order exists in YOUR OneDrive: ./scripts/New-CaesareaWorkOrder.ps1, indexed by Microsoft 365"),
                DemoPrerequisite.ForSwitch(DemoSwitch.AgentHabitat, DemoSwitchValues.HabitatLocal, "Habitat: LOCAL to start")
            ],
            [
                new(DemoSurface.CommandCenter, "Click Ask about L-417's work records.", "The familiar local answer: evidence cards from the simulated store, labelled Simulated work knowledge."),
                new(DemoSurface.Switchboard, "Flip Habitat: LOCAL to FOUNDRY HOSTED.", null),
                new(DemoSurface.CommandCenter, "Click the same button again.", "The answer now arrives from Foundry's hosted runtime and cites the OneDrive work order - source of record: Microsoft 365, with the diffuser detail the simulated store never contained. First time only: a Work IQ consent link appears instead - open it, consent as yourself, ask again."),
                new(DemoSurface.CommandCenter, "Point at the footnote under the answer.", "The hosted agent read the CLOUD Energy Hub and the presenter's own Microsoft 365 - not this laptop's city. The cloud city is not switchboard-driven: it permanently shows the forgotten-override situation WO-8732 explains, so the two cities agree at the start of the beat and diverge the moment the local one is restored."),
                new(DemoSurface.CommandCenter, "Closing beat: ask the hosted agent to TURN THE LIGHT OFF (any phrasing).", "It investigates, looks for a control tool, reports that none is available to it, and escalates to a human - because the hosted composition carries no write capability at all. The strongest control on this ladder: not an instruction, not an approval gate, but a capability that was never composed and cannot be exploited.")
            ],
            "The agent code did not change - the habitat did. The platform owns the runtime and the identity, and Work IQ answers as the person asking, which is why your OneDrive is the evidence. And the safest capability is the one the composition never granted.")
    };

    // The MAF or Foundry mechanism each beat exists to show, in the presenter's words. Kept
    // beside the walkthrough so the switchboard and the Command Center name the same thing.
    private static readonly Dictionary<DemoStage, string> Features = new()
    {
        [DemoStage.Deterministic] = "No agent yet: the deterministic platform operates the city",
        [DemoStage.InvestigationAgent] = "AIProjectClient.AsAIAgent with an ordinary C# method as its tool (AIFunctionFactory)",
        [DemoStage.Session] = "AgentSession, serialized between requests and restored for the follow-up",
        [DemoStage.Knowledge] = "TextSearchProvider as an AIContextProvider: retrieval as an on-demand tool",
        [DemoStage.Memory] = "A custom AIContextProvider that recalls the agent's own closed cases as hypotheses",
        [DemoStage.Skills] = "AgentSkillsProvider: a SKILL.md discovered and loaded on demand",
        [DemoStage.McpTools] = "McpClient tool discovery against the Energy Hub's MCP server",
        [DemoStage.InteractiveInput] = "MCP elicitation: a tool pausing mid-execution for operator input (MRTR)",
        [DemoStage.Workflow] = "Microsoft.Agents.AI.Workflows: an explicit graph with a human-in-the-loop gate",
        [DemoStage.ToolApproval] = "ApprovalRequiredAIFunction: the framework intercepts a tool the model chose",
        [DemoStage.MultiAgent] = "A second agent consulted as a tool over MCP, holding authority this agent lacks",
        [DemoStage.A2ADelegation] = "A2A: agent-card discovery and task delegation to a peer domain's agent",
        [DemoStage.Hosting] = "Foundry's hosted runtime with Work IQ: the same code under the platform's identity",
        [DemoStage.Evaluation] = "ASSERT: requirement-derived behavioral evaluation with public execution evidence",
    };

    private static readonly DemoStageDescriptor[] ScriptedDescriptors =
        [.. Descriptors.Select(descriptor => descriptor with
        {
            Capabilities = descriptor.Id == DemoStage.Evaluation
                ? [.. Descriptors.Single(stage => stage.Id == DemoStage.Hosting).Capabilities, .. descriptor.Capabilities]
                : descriptor.Capabilities,
            Walkthrough = Walkthroughs.GetValueOrDefault(descriptor.Id),
            Feature = Features.GetValueOrDefault(descriptor.Id)
        })];

    /// <summary>
    /// Gets the full demo stage catalog.
    /// </summary>
    /// <returns>The available demo stage descriptors.</returns>
    public IReadOnlyList<DemoStageDescriptor> GetAll() => ScriptedDescriptors;

    /// <summary>
    /// Gets the presenter-facing descriptor for the supplied demo stage.
    /// </summary>
    /// <param name="stage">The demo stage to resolve.</param>
    /// <returns>The matching descriptor.</returns>
    public DemoStageDescriptor GetDescriptor(DemoStage stage) =>
        ScriptedDescriptors.FirstOrDefault(candidate => candidate.Id == stage)
        ?? throw new ArgumentOutOfRangeException(nameof(stage), stage, "The requested demo stage is not defined.");
}
