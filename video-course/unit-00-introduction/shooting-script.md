# Unit 00 — timestamped shooting script

**Status: shooting script; capture incomplete.** Planned 10-minute edit; timings are
targets, not claims of recorded footage. English synthetic narration. Stop after this
pilot for review. Use the clean application baseline in baseline.md and the copyable
steps in lab.md. All app shots must come from actual execution. SmartPole is simulated.

H08 slide pages include animation keyframes; retain Alon Fliess attribution. Book
Ch6/7 supplies the autonomy rationale, Ch14 the reproducible lab. Current code and
actual Hub responses settle discrepancies. No cloud deployment or model call here.

Record at 1080p or better. Enlarge code/JSON to readable text; hide tokens, notifications
and unrelated windows. Record the actual clicks and waits. A rough cut using edited
still captures must say so on screen; do not call it continuous footage.

## 01 · 00:00–00:25 · L-417 is on in daylight

**Screen action:** Open on actual Command Center L-417 anomaly. Frame On, daylight, schedule Off. Overlay: Local simulation / Unit 00 pilot. No title slide first.

**Shot source:** `capture:city-before`. **Check:** Cold-open footage must come from this run, with the healthy Lights On fixture.

**Narration:**

This streetlight is on in daylight. The schedule says it should be off. You are the operator: can you restore it, and prove that the command actually worked? In this unit, you will start the Caesarea lab, repair L four seventeen, check the result at the Energy Hub, and reset the incident so you can repeat it. The streetlight is simulated. The services and commands you will inspect are real local execution.

## 02 · 00:25–00:55 · What success will look like

**Screen action:** Hold a readable close view of reported, desired, schedule, override and controller state. Identify evidence without clicking restore.

**Shot source:** `capture:city-before`. **Check:** Do not present the manual override as proof of a technician's reason.

**Narration:**

Success is more than a green screen. We want the Hub to report that the lamp is off, the manual override is cleared, and the last command succeeded after confirmation from the simulator. We will also inspect the command's correlation identifier. Right now, the override tells us a condition of the system. It does not tell us why somebody left the light on. That distinction will matter when we add the Operations Agent and investigate the cause in later lessons.

## 03 · 00:55–01:30 · Where intelligence belongs

**Screen action:** Use H08 p15 briefly; return attention to the single incident. Credit Alon Fliess. No catalogue of features.

**Shot source:** `H08:15`. **Check:** Explicitly say Unit00 runs no model; identify observation, inference and authority separately.

**Narration:**

Microsoft Agent Framework gives our C sharp application agents, tools, sessions, and workflows. An agent can choose what to inspect next when the evidence changes the investigation. But the known repair path we need today is deterministic. We do not need a model to restore a schedule. The companion book starts with this architectural choice: use the least autonomy that solves the requirement. Later, the agent will observe through tools and infer an explanation. Permission to change city state still belongs at an enforced service boundary.

## 04 · 01:30–02:05 · Use the exact course revision

**Screen action:** Show copyable clone/fetch/checkout from lab.md, then actual git rev-parse HEAD and global.json from the clean worktree. Do not claim to record a fresh download.

**Shot source:** `card:clone`. **Check:** Full revision 89d5a513be97301857dd4f156ce0773103712d51; clean application worktree.

**Narration:**

Start with the companion lab beside the video. It gives you the repository URL and the exact dot net eleven revision used here. Do not assume GitHub main matches this recording. Clone the repository, fetch the upgrade branch, and check out the pinned commit. Then compare git rev parse HEAD with the baseline file. I am recording from a clean checkout of that application revision. The September book and slide deck explain the design, but their older package versions are not the installation instructions for this lab.

## 05 · 02:05–02:45 · Build before running

**Screen action:** Show actual SDK version and successful clean build/test summary, with exact commands from lab.md. Label long install/build waits as edited.

**Shot source:** `card:build`. **Check:** Only insert captured results after clean-worktree checks complete; never copy the earlier working-tree test count.

**Narration:**

You need Git, PowerShell seven, a browser, and the SDK selected by global dot json. This baseline asks for dot net eleven point zero point one hundred and permits compatible later feature bands. Run restore, build, and the test project command shown in the lab. Wait for them to succeed before continuing. You do not need Azure credentials, Docker, or Azure Functions for this lesson. If the SDK cannot be resolved, install the required SDK and reopen your shell; changing the version constraint would change the experiment.

## 06 · 02:45–03:20 · Start Aspire locally

**Screen action:** Show dotnet run command, then actual Aspire resources. Exclude the token-bearing address from the recording. Keep an interactive launch terminal alive.

**Shot source:** `card:start`. **Check:** Deterministic resources running and Hub endpoint reachable. Use actual current resource links.

**Narration:**

Start the AppHost with the command in the lab and keep that terminal open. For this local exercise we start it directly. The presenter's convenience script also prepares hosted demonstrations and may refresh a shared work order, which we do not need here. Aspire coordinates the processes and gives you their addresses. Open your own printed dashboard login link, then use the resource links for Demo Control and Command Center. Keep the login token out of screenshots you share. A dashboard saying started is not enough: wait for the city services to respond.

## 07 · 03:20–03:50 · Two screens, two responsibilities

**Screen action:** Use the architecture visual, followed by Director and main-app captures when editing. Point to the Energy Hub and SmartPole boundary.

**Shot source:** `H08:13`. **Check:** Describe SmartPole as a simulator and distinguish preparation from operation.

**Narration:**

There are two browser surfaces. Demo Director is our lab bench: it creates the starting conditions and selects a stage. Command Center is the operator's application: that is where we perform the repair. Behind it, the Command Center API calls the Energy Hub, and only the Hub talks to SmartPole. SmartPole simulates the physical device. We will read the Hub directly to check what the operational system knows, rather than treating the browser's presentation as independent proof.

## 08 · 03:50–04:30 · Prepare the starting fixture

**Screen action:** Click Director 1 Deterministic, show preparation preview, confirm, then show readiness. Capture the actual interaction sequence.

**Shot source:** `capture:director`. **Check:** Lights On Reported by a Client / ForgottenOverride, Deterministic, healthy controller.

**Narration:**

In the Director, choose beat one, Deterministic. Read the preparation preview before confirming. We want Lights On Reported by a Client, which the code calls Forgotten Override. These names refer to the same starting fixture. The stage decides which capabilities are available; the scenario decides the condition of the city. Confirm preparation and check readiness. If you previously repaired the light, explicitly reapply this scenario. Merely selecting a stage that is already active does not prove that you recreated the anomaly.

## 09 · 04:30–05:00 · Read the operator's facts

**Screen action:** Switch to Command Center. Read L-417, North Promenade, customer report, On, Off schedule, override, healthy controller.

**Shot source:** `capture:city-before`. **Check:** No agent question or cloud result is implied; facts match fresh Hub read next.

**Narration:**

Switch to Command Center. Find L four seventeen on the North Promenade. Read the customer report and the operational fields before touching the action. The lamp is reported on, it is daylight, the schedule expects off, and the override is active. The controller is healthy. At this stage we have no agent investigation. The system can detect the discrepancy and execute a predefined correction. It cannot explain the maintenance decision merely because we gave the scenario a suggestive name.

## 10 · 05:00–05:40 · Check the Energy Hub before acting

**Screen action:** Execute lab step4 in a second terminal; show the actual selected fields and save before snapshot. Copy the Hub URL from Aspire.

**Shot source:** `capture:hub-before`. **Check:** Actual JSON: reportedIsOn true, expectedScheduledState false, manualOverride true; retain real revision.

**Narration:**

Now make the independent check. In a second terminal, set hub base to the HTTPS Energy Hub address from Aspire, then run the read command in the lab. This calls the Hub's asset endpoint for L four seventeen. Compare reported state, schedule, override, and revision with the browser. Store this response as before. The revision is a version of authoritative state, not a value you should copy from my recording. Our read has its own correlation identifier; the repair will have another one. We will use the repair identifier when joining its events.

## 11 · 05:40–06:15 · Restore scheduled mode

**Screen action:** Record the actual Command Center click Restore Scheduled Mode and its pending/completed transition. Preserve meaningful wait; label any cut.

**Shot source:** `capture:restore`. **Check:** Command is issued from main UI, not secretly from a test/API script substituted for this shot.

**Narration:**

Return to Command Center and click Restore Scheduled Mode once. Watch the request complete. The operator is initiating a narrow deterministic operation. There is no model deciding whether to press the button, and no claim here of a production identity deployment. The Hub computes the target, sends the device command, and waits for a result. A desired target can change while that work is pending. We must wait for confirmation before saying that the reported lamp state has changed.

## 12 · 06:15–07:00 · Verify the receipt and reported state

**Screen action:** Execute lab step5 read/assertions. Show actual lastCommand and correlated activity. Hold readable fields; no invented receipt or fixed timestamp.

**Shot source:** `capture:hub-after`. **Check:** Reported/desired/override false; receipt Succeeded; newer revision; matching correlation and simulator confirmation.

**Narration:**

Read the Hub again. The expected outcome is reported off, desired off, and manual override false. Inspect last command: it must say Succeeded, with a completion time and a correlation identifier. Check that the revision advanced from the snapshot you saved. Then filter the Hub activity by this command's identifier and read the confirmation. The lab includes checks that fail if these conditions are missing. This combination is our evidence. The browser changing color, or an HTTP request being accepted, would not be enough. We have proven the local simulated repair, not delivery to a real streetlight.

## 13 · 07:00–07:40 · Why this code waits for confirmation

**Screen action:** Readable actual EnergyHubService.cs excerpt, RestoreScheduledModeAsync: SetLampStateAsync and success branch. Show filename and real line numbers.

**Shot source:** `card:hub-code`. **Check:** Excerpt taken from clean baseline; emphasize actual reported-state assignment after succeeded confirmation.

**Narration:**

Open Energy Hub Service dot cs, in Restore Scheduled Mode Async. Look at the call to Set Lamp State Async and the success branch immediately after it. Reported Is On is assigned from the confirmed device result, and the override is cleared there. Earlier in the method, a pending command can record the desired target without inventing a reported success. This is why the Energy Hub remains the operational authority even after we add an agent. The agent may propose or request work; a confident explanation cannot replace this confirmation boundary.

## 14 · 07:40–08:10 · A repair is not an investigation

**Screen action:** Return to completed main UI; briefly highlight absence of a newly established causal explanation.

**Shot source:** `capture:city-after`. **Check:** Do not imply incident closure or evidence-backed diagnosis merely from restore.

**Narration:**

Back in Command Center, the lamp is off. We have corrected the condition, but we still have not established the technician's reason. In the next lessons, the Operations Agent will read current state, follow the conversation, and retrieve a work order. That work order can be evidence. A similar remembered incident is only a lead to investigate. Keeping those categories separate prevents yesterday's explanation from becoming today's operational truth. The same L four seventeen case will let us see the difference.

## 15 · 08:10–08:45 · Reset is part of the lab

**Screen action:** Show Director reapply Lights On Reported by a Client while Deterministic stays selected. Return to main UI and fresh Hub snapshot.

**Shot source:** `capture:reset`. **Check:** Actual On/override on restored; acknowledge reset mutates the revision and clears prior command context as implemented.

**Narration:**

Before repeating the exercise, reset the lab deliberately. Go back to the Director and reapply Lights On Reported by a Client, with Deterministic still selected. Return to the main app and read the Hub again. The lamp should be on with the override active and the controller healthy. Save a new before snapshot. Do not compare a new repair with a snapshot from an earlier scenario. This visible reset is how we reuse the incident in every lesson without hiding a preparation step from you.

## 16 · 08:45–09:25 · Pause and do

**Screen action:** Exercise card with three short tasks and lab link. Hold a quiet 5-second pause after narration if needed.

**Shot source:** `card:exercise`. **Check:** Learner repeats prepared fixture, main-app restore and Hub verification independently.

**Narration:**

Pause the video now. Starting from the reset fixture, read and save the Hub state. Perform the restore from Command Center. Then verify the Hub's reported state, override, command receipt, and revision without following my clicks. Explain three things out loud: which service owns reported state; what distinguishes accepting a command from completing it; and what you still do not know about the cause. The companion lab contains copyable commands and checkpoints. Take the time you need, then return for the expected result and a recovery tip.

## 17 · 09:25–10:10 · Expected result and recovery

**Screen action:** Show expected-result checklist clearly labeled Expected, alongside earlier verified receipt. Explain wrong-fixture recovery; no fabricated fault run.

**Shot source:** `card:recovery`. **Check:** A failure case described rather than executed is labeled explanation. Actual receipt remains distinguishable from expectation.

**Narration:**

Your answer should name the Energy Hub as owner of the operational twin. Success requires the confirmed off state and a succeeded correlated command, not just a changed desired target. We still need evidence for the cause. If your result differs, check the fixture first. Controller Fault is designed to fail the healthy-device expectation, while Normal Operation is already off. Reapply Lights On Reported by a Client, verify a healthy controller and Deterministic stage, reread the Hub, and retry. Do not keep clicking against an unexplained failed receipt. Inspect it and restore the starting conditions.

## 18 · 10:10–10:45 · Ready for the first investigation

**Screen action:** Show source path and reading references, brief VS2026/VSCode note, stop command. End on reset anomaly, not feature catalogue.

**Shot source:** `card:next`. **Check:** No claims of recorded debugging/cloud deployment. Stop course production after Unit00 review.

**Narration:**

You can read this solution in Visual Studio 2026 or VS Code. We will attach a debugger and single step when the first agent's tool call gives us a useful decision to inspect; you do not need that setup to finish today's lab. Read chapters six and fourteen of the companion book for the rationale and lab pattern. Reset the anomaly for the next lesson, or stop the AppHost using the lab's cleanup instructions. You can now operate and verify the city. Next we will ask what the agent actually observed, and what it is justified in concluding.

## Recording completion checklist

- [ ] Cold open, Director preparation and Command Center restore recorded.
- [ ] Actual Hub before/after and correlated receipt captured legibly.
- [ ] Clean-baseline code excerpt and explicit scenario reset captured.
- [ ] Exercise, expected outcome and failure recovery edited in.
- [ ] Captions retimed to final edit; no overlapping cues or clipped words.
- [ ] MP4 inspected for readable screens, audio, privacy and correct claims.
- [ ] Pilot delivered locally for review; no subsequent unit produced.
