# Lab 06 — procedures as versioned context

Actual local Windows execution, baseline843e0d0. Synthetic review narration. Procedure unchanged in this recording.

## 01 — Knowing facts is not knowing a procedure
**Picture:** Context-engineering slide, skills walkthrough.

**Narration:** We have given the agent a live-state tool, organizational evidence, and a store of past conclusions. Now we give it a documented way to investigate. A skill supplies procedural context: what to check, how to classify the result, and how to report it. It does not add authority to change a system. Our outcome is to connect a versioned Markdown procedure to an actual loaded skill and an evidence-grounded incident brief. We will inspect both the instructions and what the model actually did.

## 02 — Run an investigation
**Picture:** Skills preparation, main-app walkthrough, Investigate click.

**Narration:** Prepare Skills in Director. Keep the daylight anomaly and work evidence present, and use the empty case-memory fixture from the previous comparison. Open Walkthrough in the main app, then click Investigate L-417, new session. This is a broader question than simply asking whether the lamp is on. Watch for the skill-load call in the trace. A skill being advertised does not prove that the model loaded it, and a loaded skill does not prove that every instruction was followed. We will check the resulting behavior.

## 03 — Compare the brief with its evidence
**Picture:** Actual brief title, state table, evidence, assessment and recommended action.

**Narration:** The result is a Caesarea Incident Brief with an executive summary, observed-state table, evidence, assessment, and recommended next action. It classifies a forgotten maintenance override, while the technician note explains that verification is still relevant. Read the recommendation carefully: confirm with the maintenance team that the inspection is complete before restoring scheduled mode. This is a recommendation with preconditions, not a completed command. The lamp remains on. A polished report format can improve usability, but the evidence and authority checks still matter more than the formatting.

## 04 — Confirm the procedure was loaded
**Picture:** load_skill first in trace; procedure Loaded badge; real source preview.

**Narration:** The trace shows load skill for streetlight investigation, followed by the Hub read and work-knowledge search. The procedure card says Loaded. Now open skills, streetlight investigation, Skill dot M D in the repository. Its ordered steps say to verify live state, search for evidence, consider recalled cases as hypotheses, and classify using the triage guide. Its report format matches what we just saw. This connects a version-controlled artifact to observable execution. We did not edit the procedure or rebuild the application during this recording.

## 05 — Discover first, load on demand
**Picture:** Skill preview triage and format, then SkillsCapability provider construction.

**Narration:** In SkillsCapability, AgentSkillsProvider discovers the configured directory. Names and descriptions are advertised first; the model can load the full procedure through a tool. This is progressive disclosure: detailed instructions enter context when needed. The capability snapshots what was advertised and checks the trace for an executed load with a returned result before marking it loaded. This lesson uses a documented procedure only. It does not demonstrate a script runner, and the Markdown does not bypass stage limits, Hub rules, or later approval controls.

## 06 — Pause, vary one condition, verify
**Picture:** Exercise card with absent-evidence classification and reset.

**Narration:** Pause and run the investigation yourself. Compare each report section with the Markdown and each factual claim with the returned evidence. As a failure-path exercise, withhold work evidence in Director and start a fresh investigation. The procedure calls that an unexplained override and says to escalate before a command. Check what your actual run does; do not mark it correct just because the heading looks right. Restore evidence afterward. If the procedure is not loaded, inspect the advertised skills, configured directory, exact name, and tool trace. Investigate how you would review procedure changes and test them before deployment. We have added a procedure as context. Next we will move a tool across an MCP boundary while preserving the deterministic owner.
