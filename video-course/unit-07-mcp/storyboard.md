# Lab 07 — same capability, a new ownership boundary

Baseline843e0d0. Book chapter9; real Windows local execution. Introduce MCP as discovery/invocation boundary, not intelligence or an authorization guarantee. No cloud changes.

1. Recall the function-tool loop; outcome: prove local and remote reads reach the same Hub owner.
2. Director prepares MCP Tools, Local function. Read main-app Walkthrough.
3. Ask L-417 and inspect local trace; flip MCP remote, ask again, inspect badge and fresh Hub state.
4. Show StreetlightToolsCapability MCP_CLIENT: transport, discovery, explicit named adoption, correlation. Server advertises more than this stage exposes to the model.
5. Director arm Energy Hub MCP_SERVER, attach VS Code; actual breakpoint at EnergyMcpTools and inspect assetId/state, then continue and detach promptly.
6. Pause exercise: compare traces and Hub state. Explain failure when discovery is unavailable; do not claim a staged outage unless actually recorded.
7. Recap ownership; questions: annotations versus enforcement, discovery versus adoption, correlation and disposal. Next: remote tool requesting operator input.
