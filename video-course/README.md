# Caesarea: building agentic systems in .NET

Course author and source presentations: Alon Fliess. Video production branch:
`video/maf-course-introduction`. Narration is synthetic and is not an imitation of the presenter.

Start with [the course storyboard](storyboard.md). The first deliverable is
[Unit 00: MAF and the Caesarea lab](unit-00-introduction/storyboard.md).
Subsequent units are storyboarded, not recorded yet.

**Current status:** Unit00's revised 10:45 shooting script, lab, narration and draft captions are
ready. Local API rehearsal passed. Screen capture is blocked, so no pilot MP4 is complete;
see [exact evidence and missing shots](unit-00-introduction/execution-evidence.md).

The source decks are the unchanged September 2026 H08 and W20 release PDFs. This course uses
the pinned November .NET 11 baseline. PDF **page numbers**, including animation keyframes, are
the slide references throughout this folder. Old runbook slide numbers are not interchangeable.

The branch was created from `upgrade/net11-foundry-insights-routines`, preserving its existing
uncommitted durable-demo changes. Unit00 uses a separate clean worktree at
`89d5a513be97301857dd4f156ce0773103712d51`; see its [baseline](unit-00-introduction/baseline.md).
No merge to main is needed. Do not tell viewers a local-only course branch is available remotely.

The revised course is a self-paced lab around L-417, not a feature checklist. Start with the
[timestamped shooting script](unit-00-introduction/shooting-script.md) and
[companion lab](unit-00-introduction/lab.md). Produce only Unit00 and stop for review.
Draft captions are `unit-00-introduction/unit-00-draft.en.srt`; retime them after recording.

Production sources, narration, chapter metadata and rebuild tools live here. Downloaded decks,
raw captures, generated speech and rendered videos are ignored to keep large binaries and local
screen content out of Git. The video is delivered locally; it is not uploaded or published.

## Source material

- [H08 release PDF](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/download/vslive-san-diego-2026/VSLive-SanDiego-2026-H08.pdf)
- [W20 release PDF](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/download/vslive-san-diego-2026/VSLive-SanDiego-2026-W20.pdf)
- [Companion book and chapter alignment](book-alignment.md)
- [Repository setup](../README.md#run-locally)
- [Deployment runbook](../docs/deployment.md)
- [Current stage catalog](../Services/DemoScenario.Api/Services/StageCatalog.cs)
- [VS Code helper](../tools/vscode-demo-attach/README.md)
- [Visual Studio helper](../tools/visualstudio-demo-attach/README.md)
- [Durable recovery runbook](../docs/runbooks/durable-recovery.md)
- [Microsoft Agent Framework overview](https://learn.microsoft.com/agent-framework/overview)
- [Visual Studio stepping](https://learn.microsoft.com/visualstudio/debugger/navigating-through-code-with-the-debugger)

The September decks illustrate architecture and historical API examples. Use the compiled
repository for current signatures. Harness agents, group chat, handoffs and generic governance
middleware are conceptual comparisons where the repository does not implement them.

## Local production tools

The editable narration and actions are `unit-00-introduction/scenes.json`.
Run `python -X utf8 video-course/build_intro.py --phase audio` to create synthetic narration;
then `python -X utf8 video-course/prepare_script.py` exports the timed script and draft captions.
The audio phase sends only the reviewed narration to Microsoft's speech endpoint via edge-tts.
It reads no model credentials. Python requires PyMuPDF; voice generation requires edge-tts;
rendering requires FFmpeg/ffprobe. Downloaded source PDFs are checked by SHA-256.

`--phase assets` renders original slide frames and code/setup cards. Actual app captures must be
supplied under the ignored unit assets folder using the names in scenes.json. `--phase video`
refuses to render when any required capture is missing. Its still-capture edit is explicitly named
`caesarea-unit-00-rough-cut.mp4`; a finished motion edit still needs recorded actions and review.
Do not fill missing captures with generated app output.
