# Unit 00 narration

Voice: Microsoft Andrew Neural, synthetic English narration.

## 01. L-417 is on in daylight

This streetlight is on in daylight. The schedule says it should be off. You are the operator: can you restore it, and prove that the command actually worked? In this unit, you will start the Caesarea lab, repair L four seventeen, check the result at the Energy Hub, and reset the incident so you can repeat it. The streetlight is simulated. The services and commands you will inspect are real local execution.

## 02. What success will look like

Success is more than a green screen. We want the Hub to report that the lamp is off, the manual override is cleared, and the last command succeeded after confirmation from the simulator. We will also inspect the command's correlation identifier. Right now, the override tells us a condition of the system. It does not tell us why somebody left the light on. That distinction will matter when we add the Operations Agent and investigate the cause in later lessons.

## 03. Where intelligence belongs

Microsoft Agent Framework gives our C sharp application agents, tools, sessions, and workflows. An agent can choose what to inspect next when the evidence changes the investigation. But the known repair path we need today is deterministic. We do not need a model to restore a schedule. The companion book starts with this architectural choice: use the least autonomy that solves the requirement. Later, the agent will observe through tools and infer an explanation. Permission to change city state still belongs at an enforced service boundary.

## 04. Use the exact course revision

Start with the companion lab beside the video. It gives you the repository URL and the exact dot net eleven revision used here. Do not assume GitHub main matches this recording. Clone the repository, fetch the upgrade branch, and check out the pinned commit. Then compare git rev parse HEAD with the baseline file. I am recording from a clean checkout of that application revision. The September book and slide deck explain the design, but their older package versions are not the installation instructions for this lab.

## 05. Build before running

You need Git, PowerShell seven, a browser, and the SDK selected by global dot json. This baseline asks for dot net eleven point zero point one hundred and permits compatible later feature bands. Run restore, build, and the test project command shown in the lab. Wait for them to succeed before continuing. You do not need Azure credentials, Docker, or Azure Functions for this lesson. If the SDK cannot be resolved, install the required SDK and reopen your shell; changing the version constraint would change the experiment.

## 06. Start Aspire locally

Start the AppHost with the command in the lab and keep that terminal open. For this local exercise we start it directly. The presenter's convenience script also prepares hosted demonstrations and may refresh a shared work order, which we do not need here. Aspire coordinates the processes and gives you their addresses. Open your own printed dashboard login link, then use the resource links for Demo Control and Command Center. Keep the login token out of screenshots you share. A dashboard saying started is not enough: wait for the city services to respond.

## 07. Two screens, two responsibilities

There are two browser surfaces. Demo Director is our lab bench: it creates the starting conditions and selects a stage. Command Center is the operator's application: that is where we perform the repair. Behind it, the Command Center API calls the Energy Hub, and only the Hub talks to SmartPole. SmartPole simulates the physical device. We will read the Hub directly to check what the operational system knows, rather than treating the browser's presentation as independent proof.

## 08. Prepare the starting fixture

In the Director, choose beat one, Deterministic. Read the preparation preview before confirming. We want Lights On Reported by a Client, which the code calls Forgotten Override. These names refer to the same starting fixture. The stage decides which capabilities are available; the scenario decides the condition of the city. Confirm preparation and check readiness. If you previously repaired the light, explicitly reapply this scenario. Merely selecting a stage that is already active does not prove that you recreated the anomaly.

## 09. Read the operator's facts

Switch to Command Center. Find L four seventeen on the North Promenade. Read the customer report and the operational fields before touching the action. The lamp is reported on, it is daylight, the schedule expects off, and the override is active. The controller is healthy. At this stage we have no agent investigation. The system can detect the discrepancy and execute a predefined correction. It cannot explain the maintenance decision merely because we gave the scenario a suggestive name.

## 10. Check the Energy Hub before acting

Now make the independent check. Use the read command in the lab with the HTTPS Energy Hub address from Aspire. Here we opened that same asset endpoint in the browser so you can inspect the actual response. Compare reported state, schedule, override, and revision. In your terminal, store this response as before. The revision is a version of authoritative state, not a value to copy from my recording. This run is at revision nineteen. The repair will have its own correlation identifier, which we will use to join its events.

## 11. Restore scheduled mode

Return to Command Center and click Restore Scheduled Mode once. These captures show the app before and after the actual click; the short pending transition is not recorded. The operator initiates a narrow deterministic operation. No model decides whether to press the button. The Hub computes the target, sends the device command, and waits for a result. A desired target can change while that work is pending. We must wait for confirmation before saying the reported lamp state changed. This is a local demo, not a production identity deployment.

## 12. Verify the receipt and reported state

Read the Hub again. These evidence cards contain the actual response after our UI click. Reported off, desired off, and manual override false. Last command says Succeeded, with a completion time and a correlation identifier. Revision advanced from nineteen to twenty. Now filter Hub activity by that command identifier. It records the pending target, then SmartPole confirmation. The lab includes checks that fail if the required conditions are missing. This is our evidence. A browser changing color, or an HTTP request being accepted, would not be enough. We proved the local simulated repair, not delivery to a real streetlight.

## 13. Why this code waits for confirmation

Open Energy Hub Service dot cs, in Restore Scheduled Mode Async. Look at the call to Set Lamp State Async and the success branch immediately after it. Reported Is On is assigned from the confirmed device result, and the override is cleared there. Earlier in the method, a pending command can record the desired target without inventing a reported success. This is why the Energy Hub remains the operational authority even after we add an agent. The agent may propose or request work; a confident explanation cannot replace this confirmation boundary.

## 14. A repair is not an investigation

Back in Command Center, the lamp is off. We have corrected the condition, but we still have not established the technician's reason. In the next lessons, the Operations Agent will read current state, follow the conversation, and retrieve a work order. That work order can be evidence. A similar remembered incident is only a lead to investigate. Keeping those categories separate prevents yesterday's explanation from becoming today's operational truth. The same L four seventeen case will let us see the difference.

## 15. Reset is part of the lab

Before repeating the exercise, reset deliberately. In your lab, return to the Director and reapply Lights On Reported by a Client, keeping Deterministic selected. The capture connection failed before that browser reset shot. For this rough cut, we executed the local scenario API instead. This card shows its actual verified Hub result: lamp on, override active, healthy controller, revision twenty two. Save a fresh before snapshot. Do not compare a new repair with an earlier scenario. The finished browser reset sequence still needs recording; the copyable lab explains your steps.

## 16. Pause and do

Pause the video now. Starting from the reset fixture, read and save the Hub state. Perform the restore from Command Center. Then verify the Hub's reported state, override, command receipt, and revision without following my clicks. Explain three things out loud: which service owns reported state; what distinguishes accepting a command from completing it; and what you still do not know about the cause. The companion lab contains copyable commands and checkpoints. Take the time you need, then return for the expected result and a recovery tip.

## 17. Expected result and recovery

Your answer should name the Energy Hub as owner of the operational twin. Success requires the confirmed off state and a succeeded correlated command, not just a changed desired target. We still need evidence for the cause. If your result differs, check the fixture first. Controller Fault is designed to fail the healthy-device expectation, while Normal Operation is already off. Reapply Lights On Reported by a Client, verify a healthy controller and Deterministic stage, reread the Hub, and retry. Do not keep clicking against an unexplained failed receipt. Inspect it and restore the starting conditions.

## 18. Ready for the first investigation

You can read this solution in Visual Studio 2026 or VS Code. We will attach a debugger and single step when the first agent's tool call gives us a useful decision to inspect; you do not need that setup to finish today's lab. Read chapters six and fourteen of the companion book for the rationale and lab pattern. Reset the anomaly for the next lesson, or stop the AppHost using the lab's cleanup instructions. You can now operate and verify the city. Next we will ask what the agent actually observed, and what it is justified in concluding.