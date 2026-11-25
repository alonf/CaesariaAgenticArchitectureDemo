# Closing chapter — review notes

The local **09:09** rough cut concludes the course after Lab 17. It revisits the learner's progress through actual earlier recordings, then transfers that learning to an existing application or a new C# system. It is a course conclusion, not another application stage.

[Video](output/agentic-csharp-lab-18-rough-cut.mp4) · [Captions](output/agentic-csharp-lab-18.en.srt) · [Timed narration](output/timed-shooting-script.md) · [Design exercise](lab.md) · [Storyboard](storyboard.md)

## Review ranges

Ranges below are rounded down to whole seconds; embedded chapters and the timed script contain exact boundaries.

| Range | Subject and learner takeaway |
|---|---|
| 00:00–00:34 | Return to the daylight anomaly and recognize how much more of the system the learner can now explain. |
| 00:34–01:14 | Keep domain services authoritative: Mac restore action followed by an independent Hub read. |
| 01:14–01:58 | Revisit context engineering and inspect the model-selected tool, arguments and returned facts in VS Code. |
| 01:58–02:36 | Distinguish session, knowledge, memory, skills and current operational truth. |
| 02:36–03:20 | Distinguish three approval owners; inspect the protected call and the denied invocation. |
| 03:20–04:04 | Justify specialists, MCP and peer delegation through responsibility and data boundaries. |
| 04:04–04:48 | Revisit the actual local worker recovery and independent completion evidence. |
| 04:48–05:29 | Use evaluation and observed failures to guide improvements; preserve optional preparation limits. |
| 05:29–06:14 | Return to the Agentic Decision Map and architecture-skills resource. |
| 06:14–06:58 | Path A: add one useful agentic capability to an existing application. |
| 06:58–07:42 | Path B: architect, design and implement a new C# agentic system. |
| 07:42–08:26 | Pause and write a one-page design brief, including success and failure evidence. |
| 08:26–09:09 | Put the learning into practice, run a lab with a colleague and share Agentic C#. |

## Evidence and editorial choices

- Baseline before this chapter: `20caf4c`, branch `video-course/remaining-lessons`. The individual source recordings retain their earlier code baselines, described in the [review index](../review-index.md).
- New synthetic review narration accompanies excerpts from the reviewed final exports. Their embedded privacy masks are preserved. [Source hashes](sources.json) identify every reused export; [shots](shots.json) record exact source offsets and phrase anchors.
- The opening explicitly identifies separate recorded experiments and fixtures. The chapter does not imply a new uninterrupted run, a fresh cloud deployment, or new evaluation results.
- Large branded cards use the approved signature background and existing slide/app references. The authority review and denial are enlarged to make the selected capability, arguments and outcome readable. Narration-anchored cuts bring each piece of evidence on screen when it is discussed.
- During QA, the approval excerpt was moved past the waiting screen to the actual protected-call review. The denial excerpt now shows both “DENIED - DID NOT RUN” entries. The debugger excerpt starts at the expanded selected-tool result and its highlight.
- Durable recovery remains the local Functions worker interruption with the scheduler and city services available. Routines dispatch and generated Insights retrieval remain uncompleted cloud demonstrations; preparation is identified as preparation.

## Verification

- Visually inspected all 12 new cards and opening/middle/end samples of all 13 scenes, plus frames at every phrase-anchored cut. Compared those frames with word timings and narration. This is sampled visual/timing review, not a claim of an uninterrupted human watch and listen.
- Full video/audio decode, 1920×1080 video, audio and English subtitle streams, 13 chapters, 124 caption cues, caption ordering and bounds checked. No detected silence longer than 2.5 seconds; maximum narration tail approximately 1.15 seconds. Results are in [media validation](../production-batch/media-validation.json).
- `dotnet restore`, `dotnet format` and tests passed: 473 successful, 4 skipped. Normal build encountered the running AppHost executable lock; a build using the separate `video-course/.build/code-validation` artifacts directory passed with zero warnings/errors. The running demo was not stopped for this editorial change.
- Python syntax and `git diff --check` checked. Application code and cloud settings were not changed. Media, audio, screenshots and intermediate files remain local and git ignored; scripts, manifests and companion documents are versioned.

Review with `closing chapter — timestamp — requested change`. In particular, assess whether the two paths and the exercise give the learner a concrete next step they can carry into their own work.
