# Deck vs. implemented API drift

Where the deck shows an API shape that the installed SDK no longer offers (or never shipped), this
log records what the demo actually uses, so the deck and the live code stay reconcilable. Rows are
anchored by the stable demo identifier (the `DemoSnippets` value stamped into the relevant slide's
speaker notes), never by physical slide number - slide numbers shift whenever the deck is edited.

| Demo anchor | Deck shape | Implemented shape | Why |
| --- | --- | --- | --- |
| `H08_AGENT_CREATION` | `AsAIAgent(model, instructions, tools)` string overload | `AsAIAgent(options: new ChatClientAgentOptions { Name, ChatOptions = { ModelId, Instructions, Tools }, AIContextProviders })` | The string overload cannot attach `AIContextProviders`, and the SDK's provider chat client (`AIContextProviderChatClient`) is internal, so context providers can only join through the options overload. Adopted when the Knowledge stage landed. |
| `H08_KNOWLEDGE_RETRIEVAL` | Generic "knowledge tool" | `TextSearchProvider` with `TextSearchBehavior.OnDemandFunctionCalling` and `FunctionToolName = "search_work_knowledge"` | The Agents.AI SDK ships retrieval as a context provider, not a hand-rolled tool; on-demand mode keeps the model in charge of when to search. |

Speaker-note follow-ups tracked here:

- The session demo's note "don't live-demo the Why? follow-up" (anchor `H08_AGENT_SESSION`) is
  obsolete — the Knowledge stage demonstrates it live with evidence citations (see
  `docs/prompts/03-knowledge.md`).
