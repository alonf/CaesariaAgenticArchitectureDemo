# Agentic C# — Lab 01 review package

**Operate and verify before adding an agent.** Recorded rough cut: 11:07, 1080p,
with synthetic English review narration, twelve chapters and selectable captions.

- [Local video](output/agentic-csharp-lab-01-rough-cut.mp4)
- [Companion lab](lab.md): copyable steps, expected results, recovery and reset.
- [Storyboard](storyboard.md) and [timestamped narration/screen actions](shooting-script.md).
- [English SRT](agentic-csharp-lab-01.en.srt) and [WebVTT](agentic-csharp-lab-01.en.vtt).
- [Execution evidence](execution-evidence.md) and [exact recording manifest](shots.json).

The Mac carries VS Code/Foundry Toolkit orientation, the successful repair and Hub proof.
Windows carries VS Code source navigation, a Visual Studio 2026 failure inspection through
the Director's attach controls, timeout reconciliation and reset. These are distinct teaching
beats in independent local labs. Learners can perform the lab on either platform; Visual
Studio 2026 is optional and Windows-only. No agent/model call is part of this deterministic lab.

Application baseline is `843e0d0` (.NET 11.0.100 / Aspire 17.0.0), verified on both machines.
Course documents are on `video-course/lab01-cross-platform`; application code is unchanged.
Unit 00 is unchanged. Stop after this lab for author review.

## Local reproduction of the edit

Raw recordings, exported deck images, generated cards, speech assets and rendered media are
intentionally Git-ignored. The local video link therefore works on the production PC, not a
fresh clone. Keep the existing `assets/` and `video-course/.build/` directories to rerender.
The manifest fails if source footage is missing; it never fabricates replacement app output.

With Python, FFmpeg/FFprobe and the existing course renderer dependencies available, run from
the repository root:

```powershell
python -X utf8 video-course/build_lab01.py --phase audio
python -X utf8 video-course/build_lab01.py --phase video
```

Audio generation uses the course's existing speech service with the reviewed narration.
Unchanged speech is cached. The video phase also writes captions, chapters, a detailed
timeline and a timed narration script under `output/`. The shooting script in this folder
includes the intended screen actions; `shots.json` identifies the actual selected recordings.
After a new edit, refresh the tracked captions and verify their timing against that edit.

The review cut contains normal-speed real actions and explicitly labeled explanation holds.
The separate Director attach pickup and debugger-paused versus uninterrupted failure attempts
are documented in the evidence. The SmartPole is a simulator, not a physical streetlight.
