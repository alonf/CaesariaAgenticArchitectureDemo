# Slide vs. implemented API drift

Where the H08 deck shows an API shape that the installed SDK no longer offers (or never shipped),
this log records what the demo actually uses, so the deck and the live code stay reconcilable.

| Slide | Deck shape | Implemented shape | Why |
| --- | --- | --- | --- |
| 13 (agent creation) | `AsAIAgent(model, instructions, tools)` string overload | `AsAIAgent(options: new ChatClientAgentOptions { Name, ChatOptions = { ModelId, Instructions, Tools }, AIContextProviders })` — the form slide 24 shows | The string overload cannot attach `AIContextProviders`, and the SDK's provider chat client (`AIContextProviderChatClient`) is internal, so context providers can only join through the options overload. Adopted when the Knowledge stage landed. |
| 20 (knowledge retrieval) | Generic "knowledge tool" | `TextSearchProvider` with `TextSearchBehavior.OnDemandFunctionCalling` and `FunctionToolName = "search_work_knowledge"` | The Agents.AI SDK ships retrieval as a context provider, not a hand-rolled tool; on-demand mode keeps the model in charge of when to search. |

Speaker-note follow-ups tracked here:

- H08 slide 18 note "don't live-demo the Why? follow-up" is obsolete — the Knowledge stage
  demonstrates it live with evidence citations (see `docs/prompts/03-knowledge.md`).
