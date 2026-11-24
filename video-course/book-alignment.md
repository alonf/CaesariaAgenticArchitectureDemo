# Book, code and course alignment

Source: Alon Fliess, *The Agentic Revolution in .NET*, companion-book-v1.0,
[205-page release PDF](https://github.com/alonf/CaesariaAgenticArchitectureDemo/releases/download/companion-book-v1.0/The-Agentic-Revolution-in-dotNET-Companion-Book.pdf).
SHA-256: `f00b0eeb9dfc4d77aa69dcf289827e65d82083493d3fb5720f3bbf903a0af94e`.
Page references below are PDF pages. Reviewed all chapter takeaways and the relevant architecture,
MAF, hosting and setup passages; this is a course alignment review, not a line-by-line technical
audit of every assertion in the book.

The book's Appendix A (p198) fixes its examples at commit
`59cfd13ec32af7c6adc000e00cc56945d65e87c2`, .NET 10. Its September package and platform-status
tables are historical. Do not copy them as November setup instructions or current availability
claims. Use the course baseline, global.json, current source and observed execution instead.

| Learning problem | Book rationale | Course application |
|---|---|---|
| Who owns the city? | Ch4 pp39–52; Ch6 pp66–77 | Unit00: independent Hub read, desired versus reported state, simulator-confirmed receipt |
| When is an agent useful? | Ch6 pp69–76; Ch7 pp78–89 | Fixed repair path first; agent added when intermediate evidence changes the next investigation step |
| Can a conversation prove current facts? | Ch7 pp86–89 | Session continuity is context; every current operational assertion needs a fresh authoritative read |
| Why is L-417 on? | Ch8 pp90–102 | Work order is evidence with provenance; memory is a hypothesis; skill is procedure |
| Can the capability cross a boundary? | Ch9 pp103–115 | MCP discovery is not permission; correlate the request with the Hub |
| Who permits the write? | Ch10 pp116–131 | Contrast tool-requested input, planned workflow gate, and runtime interception; demonstrate denial and stale state |
| Why another agent? | Ch11 pp132–145 | Separate reasoning/authority responsibility, not one agent per microservice |
| Where does it run? | Ch12 pp146–162 | Separate identity, context and execution isolation; hosted sessions alone do not prove business-process durability |
| How do we know it worked? | Ch13 pp163–183 | Deterministic invariants and observed tools/outcomes alongside judged answer quality |
| Can the learner reproduce it? | Ch14 pp184–197 | Pinned clone, exact setup, checkpoints, resets and failure recovery in every lab |

Unit00 uses the book's deterministic foundation and lab structure. It does not narrate the entire
book or all later framework features. Book p189's successful restore becomes a learner-verifiable
Hub state/receipt check. Book p188's Director preparation becomes an explicit reset between attempts.
The first intro draft spent too long on IDE setup; attachment and stepping now belong in Unit02.

The November durable recovery beat is an extension beyond the frozen book baseline. Explain its
persisted orchestration separately from the in-memory workflow/approval examples. Routines and
Insights remain optional future units; require actual evidence before recording claims of execution.
