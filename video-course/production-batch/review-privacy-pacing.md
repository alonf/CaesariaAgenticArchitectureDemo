# Course revision: privacy, pacing and visual alignment

Reviewed current local exports: Unit 00 and Labs 01–17. Media remains local and ignored by git. The current files are identified by [SHA-256 hashes](video-hashes.json). Reopen the files linked in the [review index](../review-index.md).

## Changes

- Removed or concealed browser autocomplete/history and incidental personal new-tab content, including the Lab 02 navigation reported around 00:30.
- Concealed Windows editor status bars containing AWS profile/account information, browser account controls, and incidental Mac terminal/account identifiers. Redactions replace pixels before cropping, zooming and freeze holds; private strings are not stored in the redaction manifest.
- Trimmed navigation tails and synchronized code, UI evidence, debugger results and highlights with spoken cues. Existing recorded failures remain failures.
- Rebuilt the plain exercise cards in Labs 03–06 using the approved course background and signature. Lab 02 retains its corrected branded card. Actual application/report backgrounds are preserved.
- Made narration determine chapter length, eliminating excess silent visual tails. Deliberate explanatory freezes remain labeled. Captions and chapters were regenerated.

## Per-lesson review

| Lab | Revised length | Main correction |
| --- | --- | --- |
| 00 | 33:28 | Privacy redactions and narration-based cuts; the accepted introduction content is retained. |
| 01 | 11:04 | Trim command-palette and unrelated-window tails in project orientation, walkthrough, verification, confirmation code and reset; preserve durations with labeled holds. Start the first Hub verification at the returned JSON, removing the preceding blank dashboard. |
| 02 | 06:13 | Keep main-app trace on screen for trace narration; align setup, ask, breakpoint, gateway and detach transitions; retain already-correct two-exchange section. |
| 03 | 04:00 | Hold fresh-state answer through verification; show session restoration at its narration cue and remove held Quick Open overlay. |
| 04 | 03:59 | Show the completed evidence trace at the narration cue; open and hold the source note rather than Director; remove browser/editor-navigation tails. Apply branded exercise card. |
| 05 | 04:07 | Show actual Memory preparation in Director; keep the no-memory comparison visible through its explanation instead of holding the next setup screen. Apply branded exercise card. |
| 06 | 03:28 | Align the procedure and SkillsCapability code with their narration; remove the frozen Quick Open overlay and show Director setup. Apply branded exercise card. |
| 07 | 03:35 | Align local/remote comparison, tool adoption, breakpoint arrival and twin expansion; include the recorded Director detach at its narration cue. |
| 08 | 04:06 | Show the pending operator decision before discussing it, retain the denial action, and align server continuation/client-handler code instead of freezing navigation. |
| 09 | 04:12 | Keep the denial result visible; show fresh approval when requested; bring the expanded command result forward with a close-up and highlight; show Hub JSON at its narration cue. |
| 10 | 04:03 | Hold the denied-call trace and actual work-item record through their explanations; align capability wrapper, resolver and deterministic write code; remove Quick Open and wrong-record tails. |
| 11 | 03:25 | Separate Director preparation, main-app walkthrough and off/on evidence; show the actual specialist agent construction before its sanitizer, and keep sanitizer code visible until the Hub checkpoint. |
| 12 | 04:02 | Show agent-card JSON when introduced and hold it, move the full fixture and selected-field proof to their cues, and remove command-palette/wrong-JSON tails. |
| 13 | 03:28 | Align completed hosted response and composition/authorization code with narration; remove navigation tails. |
| 14 | 03:33 | Retain baseline failure during its explanation, align target and checks code, and remove Quick Open. |
| 15 | 04:26 | Bring restart continuation, terminal evidence, independent Hub proof and durable component code forward to their spoken cues; eliminate crop that drifted into unrelated text. |
| 16 | 03:08 | Keep main-app investigation visible and align WhatIf success and local rejection with spoken explanation. |
| 17 | 03:07 | Show the invalid endpoint rejection at its narration cue. |

## Lab 02 current chapter ranges

These are the revised timestamps; earlier feedback timestamps refer to the previous export.

| Range | Chapter |
| --- | --- |
| 00:00–00:40 | From an operation to a tool |
| 00:40–01:23 | Recall the context loop |
| 01:23–02:09 | Prepare and ask |
| 02:09–02:51 | Attach at the tool boundary |
| 02:51–03:38 | Metadata and deterministic execution |
| 03:38–04:28 | Read the two exchanges |
| 04:28–05:10 | A pause can outlive its caller |
| 05:10–06:13 | Pause and prove the boundary |

## Verification and limits

- Approximately 6.1 minutes of excess visual tails removed across Labs 01–17. Narration was not regenerated for this revision.
- All 18 exports passed full video/audio decoding, 1920×1080 stream checks, embedded subtitle/chapter checks, and monotonic caption timing.
- All 18 passed a full-duration silence scan at −40 dB: no silence interval longer than 2.5 seconds. The automated narration-tail check is also below 2.5 seconds for every chapter. See [results](media-validation.json).
- Visual review compared narration word cues with chapter/shot boundaries and source footage. Source privacy review sampled moving footage at half-second intervals with static duplicates removed, plus held/end frames; targeted revised frames were then checked at full size. This is a sampled visual review, not a claim that every frame was manually inspected.
- Current exports were corrected for the leaks found. Original captures, cached intermediate images and older exports remain local and unmodified; they may still contain private information and are not reviewed release copies.
- Preserve the existing lesson boundaries: local durable-worker recovery is not cloud availability or exactly-once proof; optional Routines/Insights lessons do not show successful cloud execution. No new execution or deployment was fabricated.
- Repository checks: restore, format and git diff checks passed. A fresh build was blocked by DLLs held by the running demo. Tests against existing binaries passed: 473 passed, 4 skipped. Application code was unchanged.

## Future recording and rendering

Use a dedicated browser profile without personal history, conceal editor cloud-profile/account controls, and avoid recording login/setup secrets. Review source footage before reuse. Add source masks to `privacy-redactions.json` where needed; the renderer refuses unknown video sources. Run `python video-course/production-batch/validate_media.py` after rendering and refresh export hashes.
