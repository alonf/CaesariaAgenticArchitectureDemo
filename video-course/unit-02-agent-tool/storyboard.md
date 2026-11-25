# Lab 02 — The first agent and its function tool

Outcome: ask whether L-417 is on, observe get_streetlight_state, inspect its deterministic Hub read and verify no repair occurs.

1. Recap Lab 01 Hub ownership. New requirement: natural-language query, not a write.
2. Slide 14 context loop, then simple code slides 16/18. Description supplies metadata; AIFunctionFactory registration exposes the function.
3. Windows Director Investigation Agent, Lights On fixture. Main app Walkthrough, ask Is streetlight L-417 on? Capture actual answer and timeline.
4. Director attach VS Code, arm FUNCTION_TOOL, repeat. Inspect assetId and return data, resume, distinguish model tool selection and result-to-answer exchange.
5. Source: FoundryOperationsAgent, StreetlightToolsCapability, EnergyTools. Read the admitted capabilities and ordinary C# HTTP boundary.
6. Independent before/after Hub read. Reported On stays On and no command executes. Ask for missing cause evidence; do not invent a work order.
7. Exercise: reproduce call, locate registration, distinguish observed fields from inference. Continue/detach before reset. Book ch7 pp78–89; stage01 prompt. Actual execution determines final narration/timing.
