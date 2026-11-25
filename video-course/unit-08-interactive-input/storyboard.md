# Lab 08 — a remote tool requests operator input

Baseline 843e0d0; companion book chapter 9 (pages 111–115); real Windows execution. Outcome: deny without a command, then explicitly approve and verify the Hub receipt.

1. Recall MCP discovery/invocation, introduce elicitation as a tool asking its client for missing input. Confirmation is one use, not authentication.
2. Director prepares Interactive Input with MCP remote; main-app Walkthrough explains the beat. The fixture must be On, daylight, override true.
3. Ask the built-in repair question. Observe the actual pending operator panel before clicking Deny. Save Hub state before/after and trace.
4. Repeat the request, approve, wait for terminal response, verify Off/override false and succeeded receipt at the Hub. Never reuse a repaired fixture silently.
5. Show EnergyRestoreMcpTool input-required branch, one-use request-state consumption, Boolean decision, then deterministic restore. Show the client handler and in-memory store limits.
6. Exercise: reset in Director, deny then approve. Expected state and recovery: if the request expires, start a fresh request; an in-memory wait does not survive worker restart.
7. Recap and code investigation questions: who issued/consumed state, what authenticates the operator, what happens if the city changes during the wait? Next lesson supplies state-bound workflow control.
