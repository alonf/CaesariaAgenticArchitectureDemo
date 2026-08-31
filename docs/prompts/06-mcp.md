# Stage 6 — McpTools

Deck anchors: `MCP_SERVER` (Energy Hub serves its tool), `MCP_CLIENT` (the agent discovers it).

## Goal

Add `DemoStage.McpTools`: the streetlight tool can be served over the Model Context Protocol from
the Energy Hub's own boundary. The agent discovers it at runtime instead of compiling it in.
**Same capability, same behavior, new boundary** - the whole argument for protocol-standardized
tools in one A/B.

## Scope

- **`MCP_SERVER`** (`EnergyMcpTools` in EnergyHub.Api): the official C# MCP SDK
  (`ModelContextProtocol.AspNetCore` 2.2.0), attribute-declared read-only tool
  (`get_streetlight_state`), served at `/mcp` over the streamable HTTP transport. The tool is
  owned by the Energy Hub boundary: any MCP-capable client can discover and invoke it, and the
  ownership story is the point - the domain team ships the tool, versioned and deployed with
  their service.
- **`MCP_CLIENT`** (in the agent's composition): one branch selects the tool source.
  `McpClient.CreateAsync` + `ListToolsAsync` discover the remote tool; `McpClientTool` *is* an
  `AIFunction`, so both branches produce the same type and the pipeline, recorder, and capability
  trace cannot tell them apart. The MCP transport rides a normal named HttpClient (service
  discovery, standard resilience, correlation header), and the client is disposed with the
  request.
- **Presenter toggle** `Tools: LOCAL / MCP` - a DemoControl panel over
  `GET/POST /api/operations-agent/tool-source`, stage-gated (409 below McpTools). Defaults to
  LOCAL on entering the stage: the flip is performed live as the beat.
- The response carries `ToolSource`; Command Center shows an **MCP remote** badge on the
  capability trace with a one-line boundary note.
- Snippet regions now span services: `MCP_SERVER` registers its breakpoint in EnergyHub.Api (the
  registration-coverage test validates the union of every service's `MapDemoBreakpoints` call).
  DemoControl's arming/attach panel still targets the Operations Agent process only; arming the
  Energy Hub snippet is done with the debugger attached to EnergyHub (compound launch) - see the
  backlog note.

## Lecture beat

1. At McpTools with Tools=LOCAL, ask **"Is L-417 on?"** - the familiar trace.
2. Flip **Tools: LOCAL → MCP** in the switchboard; ask again. Identical answer, identical tool
   name in the trace - now with the **MCP remote** badge: the tool was discovered over the
   protocol and invoked across a real service boundary.
3. Optional honesty beat: stop the Energy Hub service and re-ask - the tool is unreachable across
   a real network boundary and the agent reports it honestly (502, tool unavailable).
4. This is the setup for the next stage: `restore_scheduled_mode` arrives through this same MCP
   server with multi-round-trip interactive input.

## Verification

- Deterministic tests cover the stage catalog, snippet registration union across services, and
  the existing read-only boundary rules (the agent still has exactly one
  `AIFunctionFactory.Create`; MCP tools arrive via discovery - which is itself the point).
- The discovery handshake, identical-behavior A/B, and REMOTE trace are verified live.
