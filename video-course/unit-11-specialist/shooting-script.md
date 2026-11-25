# Lab 11 — consult across a reasoning boundary

Real local Windows execution; OFF baseline `843e0d0`; ON/code baseline `59017d7`. Synthetic review narration. Holds are labelled.

## 01 — When does a second agent earn its place?
**Picture:** Context engineering, then the intentional-lighting fixture.

**Narration:** A second service does not automatically need a second agent. Start with a tool or a deterministic projection when that is enough. A specialist becomes useful when it owns a separate reasoning responsibility, context and domain boundary. Here the Energy Hub knows that lighting is required, while Security owns the reasons. We will compare the same question with consultation off and on, inspect exactly what crosses the boundary, and verify that no repair was attempted. This is delegation: Operations retains ownership of the answer.

## 02 — A correct answer can be deliberately incomplete
**Picture:** Main application Walkthrough and earlier consult-OFF answer.

**Narration:** Prepare Multi-Agent in Director. This resets the city to Security Operation and starts with consultation off. Read the main application Walkthrough. The lamp is on in daylight, yet its effective target is also on. There is no anomaly. In the earlier recorded off run, Operations correctly reports an external directive without claiming to know the private reason. That limited answer is useful. Do not confuse a missing explanation with a broken deterministic system, and do not restore the schedule just because daylight normally means off.

## 03 — Consult the specialist, inspect the returned verdict
**Picture:** Enable consultation, new request, completed answer and specialist block.

**Narration:** Turn Security Consult on and ask the same daylight question in a new session. This successful take follows a transport-timeout correction documented in the lab. The trace shows assess lighting requirement ran. The consulted specialist is named, the verdict is lighting required, the classification is an active operation requiring lighting, and the recommendation is to leave it lit until the window ends. Operational details are withheld. The outer trace counts four tools and four model round trips; it does not count the specialist internal model calls as outer calls.

## 04 — Two contexts, one narrow result
**Picture:** SecurityConsultCapability and specialist construction.

**Narration:** In SecurityConsultCapability, the Operations Agent discovers one named MCP capability and propagates correlation. The model may select that tool. On the other side, SecurityAssessmentAgent reads a single authoritative snapshot, creates its own Agent Framework agent with its own instructions and snapshot-bound tool, and runs its own context loop. Its model answer does not cross the boundary directly. The service sanitizes it first. A remote tool can therefore contain a second agent, but MCP alone does not make every remote tool an agent.

## 05 — The safety rule belongs to code
**Picture:** Sanitizer implementation and independently fetched Hub JSON.

**Narration:** Look at SecurityAssessmentSanitizer. Requires lighting and the deadline come from records, not model prose. Only recognized reason and recommendation codes are considered, and an incompatible recommendation cannot lower the lighting requirement. Public wording is rendered by deterministic code. Now inspect the Hub response: reported on, effective target on, requires lighting true, and anomalous false. The revision and scenario command receipt match the before snapshot. Consultation explained the situation without changing it. The local caller header demonstrates a policy seam; it is not production authentication.

## 06 — Pause and test the boundary
**Picture:** Exercise card.

**Narration:** Pause and compare consultation off and on. Save the Hub before and after, and identify which fact was already available to Operations and which judgment came from Security. Investigate where the area is bound, where arbitrary model text is discarded, and whether a deterministic projection could satisfy a simpler version of this requirement. If consultation fails, inspect correlated logs and the specialist budget rather than inventing a missing result. Do not repair the lamp. Reapply the scenario to reset the fixture. We added a separate reasoning context while keeping operational authority in the Hub.
