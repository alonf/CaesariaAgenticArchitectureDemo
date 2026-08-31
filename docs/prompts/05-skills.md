# Stage 5 — Skills

Deck anchor: `AGENT_SKILLS` (agent-skills demo).

## Goal

Add `DemoStage.Skills`: the agent discovers documented procedures and loads them on demand, so an
investigation follows the organization's expert-authored, auditable procedure. The progression
line: Session gave the agent this conversation, Knowledge the organization's records, Memory its
own experience - Skills gives it documented procedure: how the organization says the job is done.

## Scope

- The SDK's `AgentSkillsProvider` (Agents.AI 1.19 implements the Agent Skills specification's
  progressive disclosure natively - **no API drift**: the requirements' `AgentSkillsProvider` name
  is exact). Skill names/descriptions are advertised in the system prompt; the model loads a full
  procedure via the `load_skill` tool, which therefore appears in the capability trace with zero
  extra plumbing.
- `skills/streetlight-investigation/SKILL.md` - YAML frontmatter plus the procedure: fixed
  investigation steps, a triage decision table (forgotten maintenance override / unexplained
  override / intentional security operation / hardware fault), and the mandated **Caesarea
  Incident Brief** output format (bold executive summary, observed-state markdown table, cited
  evidence, labeled hypotheses, one recommended action with preconditions, procedure version
  footer).
- The provider is constructed per request from the repository's `skills/` directory (located by
  walking up to `Caesarea.slnx`; `OperationsAgentApi:SkillsDirectory` overrides), so presenter
  edits to the markdown take effect on the next ask - the live-edit encore.
- `load_skill` requires approval by default in the SDK; `DisableLoadSkillApproval = true` here,
  and approval returns as its own beat in the ToolApproval stage.
- The response carries a `Skills` trace (advertised skills + whether each was loaded); Command
  Center renders a Procedure panel and an **Investigate L-417 (new session)** action. A missing
  skills directory degrades gracefully: warning logged, stage runs without skills.
- New `AGENT_SKILLS` snippet region, registered with the demo breakpoints.

## Lecture beat

1. **A/B**: at the Memory stage, click **Investigate L-417** - improvised prose. Switch to
   Skills, click it again - the trace shows `load_skill(streetlight-investigation)` before the
   familiar tools, and the answer arrives as the branded incident brief with a state table and a
   triage classification. Same model, same tools, same question; the difference is a markdown
   file in git.
2. Show the SKILL.md on screen (Show code); point at the triage table the classification came
   from and the format section the brief follows.
3. **Live-edit encore**: add one rule to the markdown (for example a new final line), re-run the
   investigation, and the brief obeys - skills are ops-owned, auditable configuration, not code.

## Verification

- Deterministic tests cover the skill catalog (frontmatter parse, repo-directory resolution,
  missing-directory behavior), SDK frontmatter validation of the shipped skill, the contract
  tool-name pin against `AgentSkillsProvider.LoadSkillToolName`, the stage catalog, and snippet
  registration.
- The brief format, triage classification, and live-edit behavior are model-dependent and
  verified live.
