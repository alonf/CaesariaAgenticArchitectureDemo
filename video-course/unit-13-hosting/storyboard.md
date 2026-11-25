# Lab 13 — choose a habitat, preserve authority

Outcome: compare the local composition with the existing Foundry-hosted Operations Agent, inspect real provenance and the C# hosting boundary. No deployment, permission change or publication in this lesson. Baseline59017d7; book chapter12. Consult current Microsoft Learn for platform contracts; historical book findings are not current limits.

1. Recall Agent Framework code versus Foundry runtime responsibility. Introduce three separate questions: execution, context and authority. Both Foundry hosting and Durable extension support state; durability semantics must be explicit.
2. Director Hosting prepares local fixture. Main Walkthrough; ask the work-records question locally and inspect simulated evidence label.
3. Flip existing habitat to Foundry hosted and ask the same question. Verify actual completion, response ID and source attribution. If consent or service unavailable, show the real blocker; no fabricated success, new permission grants or deployment.
4. Show the existing hosted agent in portal if accessible without changing configuration. Then Program.cs hosting registration/shared read tool, authorization handler and omitted controls.
5. Explain cloud Hub is separate from laptop, Work IQ uses user delegation while Energy Hub authorizes agent identity. Work IQ instruction-only boundary is weaker than missing write capability.
6. Exercise: identify real versus simulated evidence, trace reduced composition, compare session persistence with orchestration history. Reset to LOCAL; optional cloud exercise only with an existing authorized deployment.

Primary references: https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents and https://learn.microsoft.com/en-us/agent-framework/hosting/durable-extension. Current docs describe optional hosted resilient execution, so do not claim only Functions can recover work. A local Functions demo does not prove Azure deployment or scale.
