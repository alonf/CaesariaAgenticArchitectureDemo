# Lab 16 execution and edit evidence

Code baseline: `4e5a6d3`. Actual Windows capture; synthetic review narration. Source recording(s), retained locally under `../production-batch/assets/`: `windows-16-17.mp4`.

WhatIf and invalid schedule only. Routine inventory blocked by azd authentication; no dispatch.

The [companion lab](lab.md) records checkpoints, correlations where applicable, exact code, recovery and reset. [shots.json](shots.json) maps each scene to real source footage. The local `output/timeline.json` and `output/timed-shooting-script.md` contain final edit ranges; captions are both sidecar SRT/VTT and embedded in the MP4. Paused frames are labeled. Cuts remove wait/navigation time; separate failed/successful invocations are not presented as one success.

Raw local service snapshots and correlation logs are retained under `../production-batch/evidence/` and ignored by git. The evaluation report is under `evaluation/assert_demo/artifacts/20261125T044722Z-5fd3b0/`; durable original/terminal evidence is `15-durable-rehearsal.json` and `15-hub-after.json`. These are local evidence locations, not publicly published fixtures.

Automated media verification checks decoding, 1080p video/audio/subtitle streams, caption bounds and chapter/timeline alignment. Visual contact-sheet review checks scene content and shot boundaries. These checks do not replace Alon's full pedagogical and audio review. See [batch review notes](../review-index.md).
