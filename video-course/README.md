# Agentic C#

**Building C# Agentic Systems with MAF and Foundry by Alon Fliess**.
A self-paced course for C# developers architecting, designing and implementing new agentic
systems or adding agentic capabilities to existing applications. No prior agent knowledge required.
The smart city is the recurring example; engineering judgment is the goal.

The improved Unit 00 preserves Alon's branded introduction and architecture foundation. It adds
**GitHub/README/code structure → Microsoft Learn → the actual Foundry project and hosted agent →
the simplest C# agent call**. The local walkthrough follows **Aspire → Director → Command Center →
VS Code → Visual Studio 2026**. Both IDEs attach and detach through Director. Actual motion shows
variable drill-down, the model-selected function call, and returned Hub data. The edit uses the
full frame without permanent title/credit strips, with larger teaching-card text and relevant
deck diagrams. Visual Studio 2026 is Windows-only; Mac learners use VS Code. The main app's
top-right Walkthrough is shown. Only Unit 00 is produced.

- [Watch the local Unit 00 rough cut](unit-00-agentic-csharp/output/agentic-csharp-unit-00-rough-cut.mp4)
- [Timestamped shooting script](unit-00-agentic-csharp/shooting-script.md)
- [Companion lab: setup, debugger steps, checkpoints, reset and investigation questions](unit-00-agentic-csharp/lab.md)
- [Captions — SRT](unit-00-agentic-csharp/output/agentic-csharp-unit-00.en.srt) · [WebVTT](unit-00-agentic-csharp/output/agentic-csharp-unit-00.en.vtt)
- [Course storyboard and end-of-lab questions](storyboard.md)
- [Production plan](unit-00-agentic-csharp/production-plan.md) · [Execution evidence](unit-00-agentic-csharp/execution-evidence.md)
- [Book alignment](book-alignment.md) · [Architecture skills](https://github.com/alonf/agentic-architecture-skills)

The executable source is **local main**, with .NET 11 and the durable demo merged at
`9d2f766cc19050495c52a601075df15870c4c0af`. Restore, format, build and tests passed: 469 passed,
4 skipped, zero build errors/warnings. The merge and course remain local. Remote main has not
been updated by this production task; see the lab's release gate before distributing clone steps.

This is an edited rough cut with synthetic Andrew Neural narration, not Alon's recorded voice.
Captured motion runs at normal speed; idle waits are cut and freeze holds are labeled. Debugger
timeouts and the separate successful retry are explicitly distinguished. The operational device
is simulated; model inference is real. No cloud deployment or shared settings change was made.
The Foundry portal visit is read-only orientation to an existing deployment. It does not switch
the local debugging exercise to cloud hosting. The preceding branded cut is preserved locally
under `unit-00-agentic-csharp/output/prior-branded-cut/`.

The older `unit-00-introduction/` and `unit-00-foundations/` pilots remain archived review
material. Their branding, pinned-branch setup and still-capture approach are superseded here.

## Rebuild locally

Narration and screen actions: `unit-00-agentic-csharp/lesson.py`. Exact motion trims:
`build_agentic_csharp.py`. Raw video, exported slide images, voice tracks and outputs are ignored
by Git and remain on this machine.

```powershell
python -X utf8 video-course/build_agentic_csharp.py --phase audio
python -X utf8 video-course/build_agentic_csharp.py --phase video
```

Requires Python, Pillow, FFmpeg/ffprobe and the existing local edge-tts dependency. The audio
phase sends narration only to Microsoft's speech endpoint; it does not read model credentials.
The supplied PowerPoint is unchanged. Its local PDF export and hashes are recorded in the unit's
evidence folder. Stop after this pilot for author review.
