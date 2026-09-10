# H08 runbook - Developing Agentic Systems in .NET

The code lecture walks the deck's APIs in slide order, and the demo stages accumulate in the same
order. Every live row below is a beat on the switchboard: choose it, confirm what the director will
apply, then follow the steps the Command Center's Walkthrough panel shows. The "shows" column names
the mechanism the beat exists to show; the slide anchor is the `#region` the deck's code comes from.

## Live beats

| Slides | Beat | Shows | Fixture the director applies | What you do |
|---:|---|---|---|---|
| 13-17 | Investigation Agent | `AIProjectClient.AsAIAgent` with a C# method as its tool | Lights On | Ask agent. One tool call, marked ran. Show `FoundryOperationsAgent.cs`, then `Capabilities/StreetlightToolsCapability.cs` and `EnergyTools.cs`. |
| 18-19 | Session | `AgentSession`, serialized between requests | Lights On | Ask agent, then Ask "Why?" (same session). |
| 20-21 | Knowledge | `TextSearchProvider` as an `AIContextProvider` | Lights On, evidence present | Ask, then "Why?": evidence cards with citation badges. Optional: withhold the evidence on the switchboard and re-ask. |
| 22-23 | Memory | A custom `AIContextProvider` recalling closed cases | Lights On; arrive from Knowledge | Close case, then Ask about L-528: the case returns as a hypothesis. |
| 24-25 | Skills | `AgentSkillsProvider` and a `SKILL.md` | Lights On | Investigate L-417: `load_skill` runs first and the answer is the branded brief. Show and, optionally, edit `SKILL.md`. |
| 26-29 | MCP Tools | `McpClient` discovery against the Energy Hub's MCP server | Lights On, Tools: LOCAL | Ask, flip Tools to MCP on the switchboard, ask again: same answer, MCP badge. |
| 30-31 | Interactive Input | MCP elicitation, a tool pausing for operator input | Lights On, Tools: MCP | Restore L-417 (agent): deny, then approve. |
| 32-33 | Workflow | `Microsoft.Agents.AI.Workflows` graph with a human gate | Lights On | Open the definition, run and deny at the gate, run and approve. Optional: apply Security Operation and run again - nothing happens. |
| 34-35 | Tool Approval | `ApprovalRequiredAIFunction` | Controller Fault; Existing Incident for the last step | Investigate the fault: deny, then approve. Then apply Existing Incident and ask again: the agent finds INC-L417-001 and files nothing. |
| 40 | Multi-Agent | A second agent consulted as a tool over MCP | Security Operation, consult OFF | Ask why the lamp is on; turn the consult ON; ask again. Only agent-as-tool runs - see below. |
| 41-42 | A2A Delegation | Agent-card discovery and task delegation | Lights On | Ask the workforce domain about L-417, then for the technician cost; show the work order in full on the switchboard. |
| 43-44 | Hosting | Foundry's hosted runtime with Work IQ | Habitat: LOCAL; the hosted agent deployed; the work order in your OneDrive | Ask about the work records, flip Habitat to FOUNDRY HOSTED, ask again. Closing beat: in Copilot, Teams or the Foundry UI - the Command Center has no free-text question - ask the hosted agent to turn the light off. |
| 49 | Evaluation | ASSERT: requirement-derived behavioral evaluation over the public API's execution evidence | Applied per case by the harness, not by the director | Show a requirement in `cases.json`, run `scripts/Invoke-AssertDemo.ps1 -Arm both -Case forgotten-override`, then open the generated `report.html` and expand the baseline and the governed row. |

The Hosting beat needs the cloud half. Without it, run everything else and show a hosted answer
you captured during rehearsal: the repository ships no such capture (the hosting guide's only
screenshot is the consent prompt). See "Fallback artifacts" in the [runbook index](README.md).

The Evaluation beat needs its own setup - the ASSERT environment and a judge deployment - and it
drives the running demo itself: it resets each fixture, switches compositions, and answers its own
approvals, so leave the switchboard alone until it prints its artifact directory. One case in both
arms takes about two minutes; the full suite takes about twenty and belongs in rehearsal. Set it up
from the [ASSERT guide](../../evaluation/assert_demo/README.md).

## Slide-only

| Slides | Topic | Why |
|---:|---|---|
| 9 | Coding-agent skills setup | Repository onboarding, not a runtime beat. |
| 15 | Harness agent | Not implemented. |
| 40 | Handoff and group chat | Not implemented; the slide compares four modes and the demo runs two. |
| 45 | Protocols | Decision documents in the hosting guide, not code. |
| 47 | Governance middleware | Not implemented as middleware. Point at the three approval mechanisms and the stage gate instead. |

Agent 365 (segment 13) is a terminal-and-portal walkthrough that depends on the tenant; keep the
screenshots ready.
