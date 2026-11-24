# Book, code and course alignment

Source: Alon Fliess, *The Agentic Revolution in .NET*, companion-book-v1.0,
[205-page release PDF](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/download/companion-book-v1.0/The-Agentic-Revolution-in-dotNET-Companion-Book.pdf).
SHA-256: `f00b0eeb9dfc4d77aa69dcf289827e65d82083493d3fb5720f3bbf903a0af94e`.
Page references below are PDF pages. Reviewed all chapter takeaways and the relevant architecture,
MAF, hosting and setup passages; this is a course alignment review, not a line-by-line technical
audit of every assertion in the book.

Production provenance: the downloaded book's Appendix A (p198) identifies commit
`59cfd13ec32af7c6adc000e00cc56945d65e87c2`. Use the pinned course baseline, global.json,
compiled source and observed execution for commands and signatures. Do not narrate comparisons
between editions or dates; teach the architecture in present tense.

| Learning problem | Book rationale | Course application |
|---|---|---|
| Who owns the city? | Ch4 pp39–52; Ch6 pp66–77 | Unit00: system of systems, domain Hubs, twin semantics, independent read; Lab01: confirmed repair |
| When is an agent useful? | Ch6 pp69–76; Ch7 pp78–89 | Unit00 decision map before the app; Lab01 fixed repair; investigation grows when evidence changes the next step |
| Can a conversation prove current facts? | Ch7 pp86–89 | Session continuity is context; every current operational assertion needs a fresh authoritative read |
| Why is L-417 on? | Ch8 pp90–102 | Work order is evidence with provenance; memory is a hypothesis; skill is procedure |
| Can the capability cross a boundary? | Ch9 pp103–115 | MCP discovery is not permission; correlate the request with the Hub |
| Who permits the write? | Ch10 pp116–131 | Contrast tool-requested input, planned workflow gate, and runtime interception; demonstrate denial and stale state |
| Why another agent? | Ch11 pp132–145 | Separate reasoning/authority responsibility, not one agent per microservice |
| Where does it run? | Ch12 pp146–162 | Separate identity, context and execution isolation; hosted sessions alone do not prove business-process durability |
| How do we know it worked? | Ch13 pp163–183 | Deterministic invariants and observed tools/outcomes alongside judged answer quality |
| Can the learner reproduce it? | Ch14 pp184–197 | Pinned clone, exact setup, checkpoints, resets and failure recovery in every lab |

Unit00 teaches purpose, MAF, the deterministic foundation, Hubs/twins and the agentic layer before
showing Aspire and the two UIs. It includes attach/step/inspect/continue/detach in VS Code followed
by Visual Studio 2026 before any individual lab. Book p189's restore belongs in Lab01 with a
learner-verifiable Hub receipt/state check. Book p188's preparation becomes an explicit reset.

The durable recovery beat needs its own executable evidence. Explain its
persisted orchestration separately from the in-memory workflow/approval examples. Routines and
Insights remain optional future units; require actual evidence before recording claims of execution.
