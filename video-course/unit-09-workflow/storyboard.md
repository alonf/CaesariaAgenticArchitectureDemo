# Lab 09 — the corrective operation has a planned path

Baseline843e0d0. Book chapter10, actual C# graph and real local execution. Outcome: observe planned approval, stale-state rejection, then a separately reset successful correction. The current in-process workflow is not restart durable.

1. Recall MCP input and explain explicit executors/edges: validate → policy → approval when required → execute → verify → work item if unresolved → complete.
2. Director Workflow; reapply daylight fixture after the previous repair. Main Walkthrough, run Restore Lighting Operation, inspect graph and actual pending gate.
3. With the gate open, change the local fixture through Director (reapply daylight anomaly to advance revision), then approve the old review. Verify no stale restore and read current Hub revision. If UI invalidates pending review on scenario change, document actual withdrawal and use the existing automated stale-state test as additional evidence, not invented UI footage.
4. Prepare a fresh run and approve; wait terminal state, verify Hub Off and receipt. Show generated Mermaid plus displayed-only YAML.
5. Director attach Visual Studio 2026 for WORKFLOW; step graph construction and actual plan values promptly. Use local graph/debug controls, no cloud changes.
6. Exercise with deny and policy no-op; expected and reset. Questions: planned versus reactive control, revision binding, Resolved versus CommandExecuted, in-process wait versus checkpoint/durable wait.
