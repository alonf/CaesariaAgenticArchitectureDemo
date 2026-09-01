# Stage 7 — InteractiveInput

Deck anchor: `MULTI_ROUND_TRIP_REQUEST` (MCP interactive input / MRTR demo).

## Goal

Add `DemoStage.InteractiveInput`: the demo's **first write-capable tool** —
`restore_scheduled_mode` over MCP — guarded by **Multi Round-Trip Requests (MRTR)**. The tool
pauses input-required for explicit operator confirmation and produces **no side effect before
the input arrives**. Six read-only stages were a discipline; write arrives with its safety
mechanism built into the protocol. MRTR supplies *interactive input* — a human answer in the
loop — not authentication or policy authorization; those control points arrive in later stages
(ToolApproval, Governance).

## MRTR in one paragraph

MRTR (SEP-2322, MCP protocol revision 2026-07-28) replaced the legacy elicitation/sampling/roots
server-to-client calls with stateless re-invocation: a tool handler that needs input **throws
`InputRequiredException`** with named `InputRequest`s, which travels back as the `tools/call`
result; the client resolves the requests through its registered handlers and **re-invokes the
same tool** with `InputResponses` filled in (echoing the server's opaque `RequestState`). The
server checks `IsMrtrSupported` and degrades gracefully for older clients.

## Scope

- **`MULTI_ROUND_TRIP_REQUEST`** (`EnergyRestoreMcpTool` in EnergyHub.Api) — the slide-verbatim
  shape: check `IsMrtrSupported` first; a continuation carrying an answer in
  `context.Params.InputResponses` must also echo the server's one-time, expiring `RequestState`
  (issued per pause, bound to the asset — a confirmation cannot be fabricated or replayed);
  a valid approve executes via the existing deterministic restore workflow (desired state →
  SmartPole confirmation → reported state), a decline returns a cancellation note; otherwise
  throw input-required with a form-mode elicitation (boolean `approved` schema) plus freshly
  issued request state. The no-side-effect-before-input guard is pinned by an architecture
  test.
- **Agent side**: the MCP client registers an `ElicitationHandler` that bridges to the operator —
  the question parks in a `PendingApprovalStore`, Command Center polls
  `/api/operations-agent/approvals` and posts the decision, and the paused tool call resumes.
  If the run is cancelled or times out, the pending entry is cancelled and the tool never
  executes.
- **Stage-gated exposure**: the restore tool joins the agent's tool list only at
  InteractiveInput+ and only in MCP mode — every earlier stage keeps its read-only truth
  (architecture-test pinned). The write path requires `Tools: MCP`.
- Command Center gains **"Restore L-417 to scheduled mode (agent)"** and an
  **Operator input required** prompt panel (Approve / Deny) that appears while the tool is
  paused.

## Lecture beat

1. At InteractiveInput with `Tools: MCP`, click **Restore L-417 to scheduled mode (agent)**.
2. The **Operator input required** panel appears — the panel itself is the proof that the tool
   is paused input-required across a real service boundary. (The `restore_scheduled_mode` trace
   entry lands with the final answer, after the run completes.)
3. **Deny first**: the answer reports the cancellation and the map still shows the override -
   state provably unchanged.
4. Click again, **approve**: the restore executes through the Stage-0 deterministic workflow,
   SmartPole confirms, and the map updates.
5. Contrast forward: MRTR is the *tool* asking mid-execution; the ToolApproval stage will show
   the *client* gating invocation before the tool runs (`ApprovalRequiredAIFunction`) - two
   different control points, both with a human at the boundary.

## Verification

- Deterministic tests cover the pending-approval store (decision delivery, denial, unknown ids,
  cancellation on abandoned runs and pre-cancelled tokens, cancel-all on stage downgrade), the
  MRTR request-state store (one-time consume, expiry, asset binding), the stage catalog and
  snippet registration, and architecture pins: the confirmation check precedes the restore call
  in the tool, and the agent exposes the write tool only behind the InteractiveInput stage gate.
- The pause/approve/deny round trips are verified live against the real model and MCP boundary.
