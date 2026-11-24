# Caesarea: building agentic systems in .NET

Course author and source presentations: Alon Fliess. Production branch:
`video/maf-course-introduction`. All course files and generated media stay local.

Start with the [course storyboard](storyboard.md). Unit 00 now lays the foundation **before**
the demo: course purpose, Microsoft Agent Framework, system of systems, deterministic core,
domain Hubs and digital twins, then the agentic layer. The environment walkthrough follows
**Aspire → Director → Command Center → VS Code → Visual Studio 2026**. Individual labs follow
only after learners can run and debug the system. Later units are not recorded.

The revised Unit 00 materials are:

- [Production plan and H08 recording crosswalk](unit-00-foundations/production-plan.md)
- [Timestamped shooting script](unit-00-foundations/shooting-script.md)
- [Copyable setup/debugging lab and reset](unit-00-foundations/lab.md)
- [Execution evidence and recording gaps](unit-00-foundations/execution-evidence.md)
- [Local revised rough cut](unit-00-foundations/output/caesarea-unit-00-rough-cut.mp4)
- [Captions](unit-00-foundations/output/unit-00-introduction.en.srt)

The revised cut uses original slides, edited **actual still captures**, and synthetic narration.
It is not a continuous screencast or an imitation of the presenter's voice. Aspire, both UIs,
and real VS Code and Visual Studio 2026 attach/step/inspect/detach cycles were captured.
The debugger timeouts and separate
successful read-only retry are explicitly distinguished. See the evidence file before recording
or describing this as a finished intro.

The earlier 10:45 repair-first pilot under `unit-00-introduction/` is archived review material.
Its repair walkthrough belongs in Lab 01; its deferred-debugger teaching order is superseded.

## Source and executable baseline

- [Book rationale and chapter alignment](book-alignment.md)
- [H08 deck](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/download/vslive-san-diego-2026/VSLive-SanDiego-2026-H08.pdf)
- Supplied H08 recording: transcript reviewed; interval mapping in the production plan.
- [VS Code helper](../tools/vscode-demo-attach/README.md)
- [Visual Studio helper](../tools/visualstudio-demo-attach/README.md)
- [Runtime stage catalog](../Services/DemoScenario.Api/Services/StageCatalog.cs)

Use the book for rationale, the slides for visuals, and compiled source/observed execution for
commands and signatures. Do not narrate historical date comparisons. PDF references are **page
numbers**, including animation keyframes. The course baseline is the clean .NET 11 commit
`89d5a513be97301857dd4f156ce0773103712d51`, not an assumption about `main`.
No merge to main is needed for local production. Existing unrelated working changes are preserved.

## Rebuild locally

`unit-00-foundations/lesson.py` contains narration, screen actions and source-slide references.
The renderer exports scenes.json, the timed script, captions, chapters and the MP4.

```powershell
python -X utf8 video-course/build_foundations.py --phase assets
python -X utf8 video-course/build_foundations.py --phase audio
python -X utf8 video-course/build_foundations.py --phase video
```

Requires Python with PyMuPDF/Pillow, FFmpeg/ffprobe, and edge-tts (local vendor location supported).
The audio phase sends only the reviewed narration to Microsoft's speech endpoint. It reads no
model credentials. Downloaded source PDFs are checked by SHA-256. Raw captures, source downloads,
speech and rendered media are ignored by Git. The renderer refuses missing required captures;
it does not synthesize fake app or debugger evidence. Stop after Unit 00 for review.
