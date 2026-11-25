# Lab 01 execution and recording evidence

Application baseline: `843e0d0a1b6da79180036d84b92c9e2c98542707` on GitHub main,
Windows and macOS. Course-only work is on `video-course/lab01-cross-platform`.
.NET 11.0.100; Aspire 17.0.0. Both machines passed restore, format, build and
469 tests (four opt-in live IDE tests skipped), plus eleven HTTPS health checks.
See [Aspire validation](../../docs/product-status/aspire17-validation.md).

## Scope and provenance

The companion book supplies rationale; branded deck slides 12, 14 and 6 supply recap visuals.
Current source and actual local execution resolve differences. The physical SmartPole is a
simulator. UI clicks, HTTP responses, debugger pauses and variable values are real recordings.
The narration is a synthetic review voice, not Alon's recorded voice. Video stays local.
No cloud deployment, shared configuration change, hosted-agent invocation or media publication.

Mac preparation included SDK 11 installation, explicit SDK selection and a user-completed
repair of ownership under ~/.aspnet plus development-certificate trust. The explicit SDK 11
certificate check succeeded; HTTPS reads used normal certificate validation. The earlier
Aspire 13.6.5 port collision did not recur after a full restart and upgrade. Its exact cause
is not proven. README documents setup and port diagnosis without claiming an upstream fix.

## Mac recording

- VS Code project, README, Apps/Services/Contracts and global.json captured from the cloned
  `/Users/alon/Dev/caesariaagenticarchitecturedemo` checkout.
- Foundry Toolkit showed existing project `caesarea-dev`, hosted `caesarea-operations`,
  version 13; the details view reported Running. This was read-only orientation.
- Initial Hub read: revision 8, reported/desired On, daylight true, scheduled Off,
  override true and healthy controller.
- Main Command Center Restore Scheduled Mode at 03:40:30 local. Hub revision 9:
  reported=false, desired=false, manualOverride=false, Succeeded.
  Correlation `baeb6ab54e0249cf9384c92e79d907cc`.
  The fresh Hub GET and activity are captured in `assets/mac-proof.mp4`.
- Main result arrived too quickly to narrate Pending as an observed UI wait. Hub activity
  does show Pending then Succeeded. The code walkthrough explains that transition.
- Visible Director reset at 03:44:52, correlation `0a7805057fc0487fb99a0717d112cf48`.
  Fresh Hub read: revision 11, reported/desired On, override true, controller Healthy.

The default Deterministic stage initially lacked walkthrough metadata. Selecting another
stage and then explicitly selecting Deterministic restored the main UI Walkthrough. No model
call was made. Companion lab documents this workaround; no application fix is claimed.

## Windows recording

Source navigation follows CommandCenterService, HttpEnergyHubGateway and EnergyHubService.
Director attached Visual Studio 2026 to EnergyHub.Api. An ordinary breakpoint at baseline
line 370 stopped after SetLampStateAsync. Expanded commandResult showed Failed, ActualIsOn
null and the controller-rejection summary. F10 skipped the success update and reached failure.

The initial paused request outlived the caller timeout. After Continue, the Hub completed
Failed with reported On. [Saved state and activity](evidence/windows-debugger-failure.json)
record correlation `a929e0c2fce64c9f8f4a3b8de52643b3`. This is explicitly explained in the edit.
Director detach left the service running. A separate uninterrupted main-UI request returned
controller rejection; [Hub response](evidence/windows-failure.json) records
`6e25ccc36c5d43aa8cb7c96926b55e90`, revision 19, Failed, reported On and desired Off.
The [reset read](evidence/windows-reset.json) records the restored healthy override fixture.

The earlier [API rehearsal](evidence/api-rehearsal.json) predates the recording baseline and
is supporting rehearsal evidence only. It is not substituted for main-UI footage.

## Edit validation

The 11:07 rough cut is 1920×1080, H.264/AAC with embedded English captions and twelve
chapter markers. External SRT/VTT captions are included. FFmpeg decoded the complete final
file without errors. Caption timing is monotonic and contained within the video duration.
Selected frames were visually inspected for readable source, actual expanded Locals values,
receipt fields, framing and highlight placement; action/narration alignment was checked at
the repair click, debugger stop, expansion, step and terminal outcome.

Mac footage uses the real remote desktop recording. Windows browser/editor footage uses a
correctly bounded desktop capture; Visual Studio uses an individual-window capture. Failed
black-window/multi-monitor takes are excluded. Actions run at normal speed; freeze holds are
labeled. The Director attach close-up is a separate real pickup, edited into the explanation;
it is not represented as one uninterrupted session. The long debugger pause and separate
unpaused retry are explicitly distinguished. No tokens/sign-in are included in selected shots.

The earlier application quality results above remain the baseline results. This course-only
change was checked with Python compilation, manifest/render validation, full media decoding,
caption timing and git diff --check; it does not introduce application behavior changes.
Unit 00 remains unchanged. Raw media and the edit stay local and are ignored by Git.
