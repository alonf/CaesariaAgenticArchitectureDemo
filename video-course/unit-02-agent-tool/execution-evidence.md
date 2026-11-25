# Lab 02 execution evidence

Baseline `843e0d0`, Windows, local Aspire and real Foundry model inference; SmartPole simulated. Media remains local. Source footage: `../production-batch/assets/windows-02-03.mp4`, 900 seconds, started 04:42:52 local. Ignore setup/error footage before approximately385 seconds except explicitly described troubleshooting.

- First UI success correlation `a90c385f8f48487d830f3c7fcc3bc9e2`: one tool, two model exchanges; correlated log saved in production-batch/evidence.
- Debugger request `9e6f69944ae246b8ab57b2d3419be95b`: actual FUNCTION_TOOL hit, gateway step, response breakpoint, Watch drilldown and Debug Console values. Caller timed out after100sec while paused. This is not presented as a successful UI round trip.
- Separate uninterrupted request at04:56:27 answered by04:56:45, correlation prefix `07e35eee8c8c`, one tool/two round trips.
- `lab02-recording-before.json`, `lab02-after.json`, `lab02-final-hub.json`: Hub revision3, reportedOn=true, override=true, same scenario receipt. No repair was executed.
- `lab02-config-check.json` is an off-camera API smoke check, not substituted for UI evidence. `lab02-before.json` is from the prior AppHost process and is not comparable to the restarted fixture.

Post-production: synthetic review narration; cuts remove waits; held frames are labeled. Automated decoding/caption/chapter checks and visual scene checks completed. The cut awaits Alon's full content review; see [review index](../review-index.md).

## Review correction — debugger synchronization and course background

- Recut 03:38.4–04:28.4 against the existing narration's word timings: Watch values, first received tool call, second sent tool result, then second received answer. The old final source clip crossed into the browser at raw 791 seconds and held that wrong screen. All replacement clips finish before that transition; holds remain labeled.
- Highlight boxes now follow the spoken values and the actual debugger layout. The browser returns only at 04:28.4, when the timeout explanation begins. Narration, total duration and later timestamps are unchanged.
- Rebuilt the exercise card at 05:16.4–06:19.4 (including the reported 05:24 frame) with the approved `signature-background.png`, keeping the signature clear of the text. Reproduce it with `python video-course/unit-02-agent-tool/build-exercise.py` before rendering.
- Reviewed exported frames through the debugger section and into the browser, and the card at 05:24. Full audio/video decoding, chapter boundaries and 84 caption cues pass. Other lessons are unchanged pending review.
