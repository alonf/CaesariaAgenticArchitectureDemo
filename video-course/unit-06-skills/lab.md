# Lab 06 — a versioned investigation procedure

Outcome: connect the skill artifact, executed load and resulting report. Allow 20 minutes. Baseline `843e0d0`; setup from Lab 01/README; book chapter 8. No deployment or skill edit is required.

## Prepare and execute

1. Director → **Skills** → confirm. Fixture: **Lights On Reported by a Client**, work evidence present, case memory empty for the recorded comparison.
2. Main app → top-right **Walkthrough** → **Investigate L-417 (new session)**.
3. Inspect the trace. Recording: `load_skill({"skillName":"streetlight-investigation"})`, Hub read, work search; three tools/three model round trips, correlation prefix `f2f49bdc98ff`.
4. Check **Procedure → Loaded** and compare the report sections with `skills/streetlight-investigation/SKILL.md`.
5. Verify that the recommendation retains maintenance-confirmation preconditions. No command was executed in this lesson.

```bash
curl --fail --silent --show-error https://localhost:7089/api/energy/assets/L-417 > after-investigation.json
```

Expected: On, manual override true, unchanged command identity/revision from the prepared fixture. The SmartPole and local work records are simulated; this is real app/model execution.

## Code

Open `skills/streetlight-investigation/SKILL.md` (preview helps read the triage table). Then inspect `Services/OperationsAgent.Api/Services/Capabilities/SkillsCapability.cs`, region `AGENT_SKILLS`: directory discovery, `AgentSkillsProvider`, `ContextProviders.Add`, advertised-skill snapshot, exact load-name/result check. A procedure is context, not authorization. No script execution is supplied by this lesson.

## Pause exercise: missing evidence

This variation is an exercise, not a claimed recorded result: uncheck **Work Knowledge** in Director, start a fresh investigation, and compare the actual assessment against the **Unexplained override** row. Expect explicit missing evidence and escalation before any command. Exact wording/order may vary; document deviations rather than forcing a pass. Restore evidence afterward.

If no skill loads, inspect its discovery/name/directory and the actual call trace. An advertised skill is not necessarily loaded. If the report contains old evidence, use a new session. Reapply **Lights On Reported by a Client** if another lesson repaired the lamp.

## Investigation questions

1. What enters the initial context, and what enters only after `load_skill`?
2. What conditions make the UI mark a skill as loaded?
3. Which constraints remain deterministic even if the model ignores the procedure?
4. Propose a harmless report-format change in a disposable local copy. How would you test and review it? Do not change approval or authority rules for this exercise.
5. Which evaluations would detect a convincing report that skipped a required fresh read?

## Framework reference in the revised video

Read [Agent Skills](https://learn.microsoft.com/en-us/agent-framework/agents/skills), then return to the repository. Find discovery, instruction loading and the loaded-skill trace. Explain why procedural guidance does not confer write authority or prove the incident cause.
