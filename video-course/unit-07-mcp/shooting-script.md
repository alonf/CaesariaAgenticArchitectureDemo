# Lab 07 — same capability, a new boundary

Real local Windows execution; baseline `843e0d0`. Synthetic review narration. Holds are labelled.

## 01 — Who owns a tool?
**Picture:** Context loop, then local read trace.

**Narration:** So far our agent has called a C sharp function compiled into the Operations Agent service. What changes when the capability belongs to another service? Model Context Protocol supplies discovery and invocation across that boundary. The model still selects a tool, the framework invokes it, and the result returns to the context. Our outcome is to compare the two paths, inspect the remote boundary, and prove that the Energy Hub remains the owner of state. MCP is a protocol, not a second intelligence and not an authorization guarantee.

## 02 — Compare local and remote
**Picture:** Prepare MCP Tools, local answer, flip remote, remote answer.

**Narration:** Prepare MCP Tools in Director, starting with Local function. Open the main application Walkthrough and ask whether L-417 is on. The local trace shows one read and two model round trips. Now switch Tool Source to MCP remote and ask the same question again. Look at the badge: MCP remote. The name and business capability are the same, but the tool was discovered at runtime from the Energy Hub. Both answers say the lamp is on. Neither read repairs it; the override and the operational anomaly remain.

## 03 — Discovery is not adoption
**Picture:** StreetlightToolsCapability transport and allowlist.

**Narration:** Open StreetlightToolsCapability in the Operations Agent. The client creates an HTTP transport and propagates the correlation identifier. It creates the MCP client, lists tools, and explicitly adopts the named streetlight-state tool. That last step matters. A server may advertise capabilities that this agent should not receive. The direct restore tool is added only inside the Interactive Input stage window. From Workflow onward that direct write is withdrawn. Discovery tells us what a server offers; our composition code decides which capabilities enter this agent context.

## 04 — Step into the owning service
**Picture:** Director arms MCP_SERVER and attaches VS Code; actual pause, F10/F11, twin expansion.

**Narration:** In Director, arm MCP server on the Energy Hub row and attach VS Code there. The process to debug is the owner of this remote implementation. Ask again. Now the breakpoint has stopped in EnergyMcpTools with asset identifier L-417. The class and method attributes expose the C sharp method as an MCP tool. Its read-only annotations describe the capability; the deterministic implementation enforces what it actually does. Step into GetState, then expand the actual twin. Reported on is true, expected schedule is false, and manual override is true. These are service values, not model guesses.

## 05 — A breakpoint can cause a timeout
**Picture:** Actual failed UI, detach and separate successful retry.

**Narration:** There is a real failure in this take. While the Hub was paused, the client exceeded its HTTP resilience timeout. Continuing the debugger does not turn that already-failed request into success. We detach through Director, then make a separate read request. It succeeds and again shows the MCP remote badge. This is a useful diagnostic distinction: a debugging pause can resemble a network failure. Check correlated logs before blaming deployment or model configuration. For this read-only operation, a new request is safe; consequential commands require outcome reconciliation before any retry.

## 06 — Pause and compare the boundary
**Picture:** Exercise card.

**Narration:** Pause here. Prepare MCP Tools and compare one local read with one remote read. Save the Hub JSON before and after, and compare the last command and state revision. They should be unchanged. If you debug the remote tool, continue promptly; if the request times out, detach and retry the read as a new request. Investigate three questions in the code: where is the discovered-tool allowlist enforced, how is correlation propagated, and why are tool annotations insufficient as authorization? We have moved a capability across a service boundary while preserving ownership. Next, the remote tool will ask for operator input.
