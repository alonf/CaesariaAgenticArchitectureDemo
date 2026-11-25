# Lab 01 — timestamped shooting script

**Recorded rough cut: 11:07.** Ranges below match the edit (rounded to seconds).
Real actions are aligned with narration and markers. Full-frame app footage; no permanent title strips.
See shots.json for exact source ranges, crops and labeled explanation holds.
The Director attach close-up is a separate pickup; the failure, debugger values and HTTP evidence
come from actual execution. Narration uses a synthetic review voice.

## 00:00–00:40 — Recall the foundation

**Picture:** Brief view of L-417 On in daylight. Slide 12, then slide 14. Mark the Hub on 12
and the tool-result return path on 14. These are recap visuals from the introduction.

**Narration:** Welcome back to Agentic C sharp. In the introduction we separated three
responsibilities: the operational system, the Agent Framework that coordinates model and tool
interaction, and Foundry as the platform. We also followed a real request in a debugger. The model
selected a function tool, ordinary C sharp code read the Energy Hub, and the returned facts became
context for the next model call. The agent answered, but the lamp did not change. Here is our
continuing incident: L-417 is on in daylight. Before we give an agent a way to request a repair,
we need to understand the operation itself and how to prove its result. That is our first lab.

## 00:40–01:46 — The same project on a Mac

**Picture:** Actual Mac VS Code. Show clone/open, README, project tree and `global.json`.
Show the checked commit and SDK without unrelated terminal history. Pause on each folder.
The application baseline is published on main at 843e0d0. Show the already cloned checkout;
put copyable clone/setup commands in the companion lab rather than staging another clone.

**Narration:** We are now in Visual Studio Code on macOS. The repository is the same cumulative
C sharp solution. We select a runtime stage; we do not maintain a different branch for each
lesson. Start with the README, then locate Apps, Services, Contracts, the Aspire AppHost and
Tests. Apps contains the two web interfaces. Services contains the operational services and the
agent services. Each boundary has focused contracts. AppHost starts and connects the local
processes. The tests check behavior that must remain reliable as we add intelligence.

The lab code is dot net eleven. The same Agent Framework concepts and patterns apply on dot net
ten LTS, but this checkout needs the SDK declared here. Check the version before troubleshooting
application errors. C sharp Dev Kit supplies the C sharp development experience in VS Code.
On a Mac we use VS Code; on Windows you can also choose Visual Studio 2026. We already introduced
debugging in Unit zero. Today we will use the editor to follow the deterministic command path.

## 01:46–02:45 — Foundry from inside the editor

**Picture:** Foundry Toolkit → existing project → Agents → Hosted Agent. Point to project,
agent name, version and actual displayed status. Read only. Do not open credentials or deploy.

**Narration:** In the introduction we visited Foundry in a browser. Foundry Toolkit brings
supported project resources into VS Code, so we can orient ourselves without leaving the editor.
Here is our existing project, and here is the hosted Operations Agent. These are cloud resources;
opening this view does not create another local agent. The portal and Toolkit are clients for
the same platform, although their available views and features are not identical.

This distinction matters for today's exercise. We are about to operate the local deterministic
system. We are not invoking this hosted agent, and the exercise needs no model credentials.
If you do not have access to a Foundry project, follow the recorded orientation and continue
with the local lab. We will return to hosting when the architecture needs it. For now, remember
the division: the editor is our development environment, Foundry supplies platform services,
and the Energy Hub remains responsible for the operational state we are about to change.

## 02:45–03:18 — Define success before clicking

**Picture:** Large outcome card beside the Hub portion of slide 12. Four short lines:
trace the request; distinguish desired/reported; verify the receipt; recover from failure.

**Narration:** By the end of this module, you should be able to trace one command from the
operator interface to the Hub and controller, distinguish an intended state from a confirmed
state, and prove both success and failure. You should also be able to reset the lab for another
attempt. This is why we start with deterministic code. Restoring a known schedule is a bounded
operation with explicit rules. A language model does not improve the definition of success.
Later an agent may help investigate why the lamp is on; that does not transfer ownership of the
repair to the model.

## 03:18–04:05 — Prepare, then use the main application

**Picture:** Aspire resources. Director selects Deterministic and prepares Lights On Reported
by a Client. Switch to Command Center and open top-right Walkthrough. Show initial Hub JSON.

**Narration:** Start the AppHost and use its printed dashboard URL. Aspire shows the processes
and their endpoints. Open the Director and Command Center. The Director prepares our fixture;
Command Center is the operator's application. Select the Deterministic stage and prepare
Lights On Reported by a Client. Now read the walkthrough in the main application.

Check the facts before acting. L-417 is reported on. It is daylight, the scheduled target is
off, and this fixture has a manual override with a healthy controller. The Hub read provides
our independent starting checkpoint. These are seeded conditions for this exercise. We are
not concluding that every daylight anomaly is an abandoned override. In the later investigation
lessons, explanations will need their own evidence.

## 04:05–04:54 — Execute a bounded operation

**Picture:** Main UI Restore Scheduled Mode click at normal speed. Point to changed state
only after response. Include Pending only if captured; never fabricate a delayed response.

**Narration:** Before we click, notice what Restore Scheduled Mode means: restore the schedule,
not set an arbitrary Boolean selected by a model. The Hub reads the controller state and computes
the scheduled target. It records a pending command and sends the request through its controller
boundary. Only a confirmed result allows it to update the reported state. We will locate the
pending transition in the code, because this operation completes too quickly to inspect that
intermediate state in the interface.

Now the operator requests Restore Scheduled Mode. The response arrives, and the reported state
changes to off. Read the outcome in Command Center, then verify it at the Hub. The interface is
useful feedback, but it is not our only evidence. This discipline will stay the same when an agent
starts selecting tools: a convincing sentence is not a command receipt.

## 04:54–05:42 — Prove what changed

**Picture:** Fresh Hub JSON and activity after the actual click. Timed markers on
`desiredIsOn`, `reportedIsOn`, `manualOverride`, receipt status and correlation ID.

**Narration:** Read these fields with me. Desired is off: that is the target we requested.
Reported is off: that is the state confirmed by the controller response. Manual override is
cleared, and the latest command receipt says Succeeded. Follow its correlation identifier into
the Hub activity to connect the request with its outcome. Do not compare an unrelated earlier
receipt with the new request.

Our physical device in this lab is a simulator. We have verified a real software command path
and a real response from that simulator; we have not claimed to operate a streetlight in a
deployed city. The architectural lesson is transferable. Keep the source of truth clear and
make the confirmation boundary visible. We have now moved from a requested target to a
confirmed outcome, with evidence independent of the operator's display.

## 05:42–07:06 — Read the C# that makes the claim true

**Picture:** Actual Windows VS Code source navigation through CommandCenterService, HttpEnergyHubGateway
and EnergyHubService.RestoreScheduledModeAsync. Highlight short blocks, not whole files.
Finish at the guard requiring `Succeeded` and a Boolean `ActualIsOn`.

**Narration:** We are switching to VS Code on Windows to inspect the same code, not to repeat
the successful demonstration. Each machine runs its own local lab. You can follow this entire
lesson on either platform. Let us find the mechanism behind that result. Command Center receives the
operator's request and calls its Energy Hub gateway. The gateway crosses the focused HTTP
boundary, carrying the correlation identifier. Command Center does not reach around the Hub
and write the simulator directly.

Inside the Hub, find RestoreScheduledModeAsync. The optional expected revision is checked
under the same lock as the new revision claim. The method rereads physical state, computes
the scheduled target, and records a pending command. Now follow the call to SetLampStateAsync.
That is the controller boundary. The confirmation guard below it is the critical part:
the result must be Succeeded and must include the actual state. Only then does this path
assign reported state and clear the override. A success label without confirmation is handled
as failure. A superseded operation must not overwrite a newer revision.

These are ordinary C sharp rules. The language model is not responsible for remembering them.
When we later expose a capability to an agent, the deterministic service still enforces its
own contract. The debugger and source view help us identify where an outcome becomes true,
rather than trusting what the caller hoped would happen.

## 07:06–08:36 — Make failure visible

**Picture:** Director Controller Fault; main UI restore; fresh Hub read. Mark failed receipt
and reported On. Keep unsuccessful result visible long enough to read.

**Narration:** A successful demonstration is only half the lesson. On Windows, attach Visual
Studio 2026 to the Energy Hub through the Director. This is an optional debugger choice; you
can inspect the same C sharp with VS Code. Set an ordinary breakpoint at the confirmation guard.
Prepare Controller Fault, return to Command Center, and request Restore Scheduled Mode.

The breakpoint has stopped after the controller call. Expand command Result in Locals.
Status is Failed. Actual Is On is null. The summary says the controller rejected the command.
Step over the condition. Execution skips the successful update and reaches the failure path.
This is the value of debugging: we can connect the observed behavior to the exact rule that
enforces it. A selected operation is not a confirmed repair.

There is another useful observation in this take. Holding the Hub at a breakpoint caused the
caller to time out. After continuing, the Hub still recorded the controller's failed result.
A timeout alone does not establish the outcome. Read the receipt before retrying. Now detach
through the Director. Having verified the previous failure, make another request without the
debugger pause. Command Center reports the rejection immediately. Here is the freshly saved
Hub response: reported is still on, desired is off, and the new receipt says Failed. Its
correlation identifier differs from the paused attempt. A later agent must preserve this
distinction too: requesting the right operation does not guarantee that it succeeded.

## 08:36–09:16 — Recover and reset explicitly

**Picture:** Reapply Lights On fixture; verify healthy controller, On and override. If showing
another repair, reset once more on camera. No hidden reset between lessons.

**Narration:** Recover through a known fixture. Reapply Lights On Reported by a Client. Check
that the controller is healthy and the original on-in-daylight condition is back. This reset
is a lab operation; it is not our advice for repairing real infrastructure. It makes the
exercise repeatable and keeps the next lesson's starting point honest.

If a request appears stuck, check service health and whether a debugger is paused before
clicking again. Inspect the latest receipt before repeating a write with an unknown outcome.
We end with the original anomaly ready for the next investigation. A repair in one lesson
does not silently disappear between videos; we show the reset that recreates the problem.

## 09:16–10:12 — Pause and do

**Picture:** Large exercise card with four steps; relevant Hub view alongside. Hold for an
explicit pause, not a fabricated UI animation. Companion lab provides copyable commands.

**Narration:** Pause the video and do the lab yourself. Prepare the deterministic starting
fixture. Restore the schedule in Command Center and verify the Hub's state and receipt.
Then prepare Controller Fault, repeat the request and prove that the failure did not become
a reported repair. Finally reset the original anomaly.

Your evidence note should contain starting facts, a successful command's correlation identifier,
a failed command's correlation identifier, the corresponding reported states and the source
location that enforces confirmation. The expected successful result is desired off, reported
off, override cleared and Succeeded. The failure keeps reported on and records Failed.
Your final reset should leave the healthy controller, reported on and override enabled.
If your results differ, use the receipt detail and Aspire logs to explain the difference
before moving on. Resume when you can explain what each field proves.

## 10:12–11:07 — Investigate and connect to the next lesson

**Picture:** Slide 6 decision map plus large question text. End with L-417 ready for Lab 02.

**Narration:** Now investigate the code. Where is the scheduled target computed? What happens
if a response says success but omits actual-state confirmation? Where does a stale revision
get rejected? You can consult documentation or ask an AI assistant, but require file locations
and compare its claims with your run.

Finally, ask which new requirement would justify an agent here. We did not need one to perform
a fixed, well-defined operation. We may need help interpreting a report, finding evidence,
choosing among capabilities or coordinating a longer investigation. That is the decision map
in practice. This lab established the operational foundation: requests cross a controlled
boundary, outcomes are confirmed, failures remain visible and the exercise can be reset.
In the next module we add an investigation agent to this same system. The agent gains a
focused way to observe the world; the Energy Hub keeps ownership of what is true.
