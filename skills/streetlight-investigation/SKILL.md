---
name: streetlight-investigation
description: Documented procedure for investigating a streetlight operating against its schedule, with triage heuristics and the mandated Caesarea Incident Brief report format.
---

# Streetlight Investigation Procedure (v1)

Follow these steps in order. Do not skip a step, even when the cause seems obvious.

1. **Verify live state.** Call `get_streetlight_state` for the asset. Never rely on conversation
   context or memory for current state.
2. **Search for evidence.** Search work knowledge for maintenance records, work orders, and
   technician notes about this asset. Cite evidence identifiers you rely on. If nothing is found,
   state that explicitly.
3. **Consider recalled cases.** A recalled closed case is a hypothesis from another asset, never
   evidence. Label any analogy as unconfirmed.
4. **Classify using the triage guide below.** Pick the single best-matching classification.

## Triage guide

| Pattern | Classification | Next action |
| --- | --- | --- |
| Override active + maintenance evidence found | Forgotten maintenance override | Confirm with the maintenance team, then restore scheduled mode |
| Override active + no evidence anywhere | Unexplained override | Escalate to the duty supervisor before any command |
| On during daylight + active security context requiring lighting | Intentional operation | Do not restore; note the requirement source |
| Controller faulted | Hardware fault path | Commands will fail; dispatch field service |

## Mandated report format

Present the result as a **Caesarea Incident Brief**, exactly this structure:

- Title line: `## Caesarea Incident Brief — <asset id>`
- One-sentence **bold executive summary**.
- `### Observed state` — a markdown table of the live state fields.
- `### Evidence` — cited identifiers with one-line summaries, or "No evidence found."
- `### Assessment` — the triage classification, plus any recalled-case analogy clearly labeled
  as an unconfirmed hypothesis.
- `### Recommended next action` — one action with its preconditions.
- Final line: `Procedure: streetlight-investigation v1`
