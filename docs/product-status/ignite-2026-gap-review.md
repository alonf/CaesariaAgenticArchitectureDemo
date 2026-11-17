# Ignite 2026: Foundry announcements and Caesarea gaps

Reviewed November 18, 2026, Asia/Jerusalem, against announcements published November 17.
Ignite runs November 17–20: this is a dated inventory of Foundry and closely related agent-platform
news available so far, not a claim to cover announcements that have not yet been published.
Sources below are Microsoft announcements, documentation and the official Book of News's original
items. Its generated summaries are not the authority for availability.

Compared with the September H08/W20 release PDFs, their previously hash-verified text extracts,
the current update branch, and the November 17 implementation records. Page references are PDF
animation pages, not PowerPoint slide numbers. The editable decks/manuscript have not been changed.
Some web-tool article opens failed; direct HTTPS retrieval provided the main Foundry improvement,
models and observability articles. This is announcement/documentation review, not tenant eligibility
testing or verification of new APIs.

## Assessment

The architecture still fits: Hubs own operational facts and commands; agents investigate through
focused boundaries; evaluation precedes promotion. The decks already explain runtime choice,
Agent 365 and the production-feedback loop. The gaps are concrete platform mappings, maturity
labels and evidence, rather than a missing architectural foundation.

The most immediate correction is **Insights service GA**, announced November 17. Our selected
Projects SDK remains beta and exposes experimental Insights APIs. Service GA does not remove
those SDK annotations or establish compatibility with a new SDK release.

Keep the agreed scope: run the Functions recovery demonstration locally; explain Azure hosting
in the presentations and book. AKS deployment remains a future, separately scoped exercise.
The running demo was not changed or restarted for this review.

## Announcement inventory and gaps

GA means the source explicitly says generally available. Preview, Frontier, limited access and
future rollout are kept distinct. Availability in our subscription/region is unverified.

### Runtime and execution

| Announcement | Status at this review | What Caesarea has / what to change |
| --- | --- | --- |
| Foundry deployment to customer-owned AKS | Announced preview; hosted-agent rollout wording differs across sources [1–3] | H08 already lists self-hosted Kubernetes. Add the distinct Foundry-integrated AKS path. No AKS resources or account configuration exist in this repo. |
| More hosted-agent concurrency; faster starts | Concurrency GA; faster starts preview [2] | Existing Responses-based hosted composition is relevant. Do not claim measured improvements without a load/cold-start comparison. |
| Microsoft Copilot harness in Foundry | Preview; introduced October 28, not first announced at Ignite [2, 15] | H08 p. 19 covers MAF/Copilot harness concepts. Distinguish our MAF implementation, the managed Foundry harness and Copilot SDK 2.0. |
| Microsoft Copilot SDK 2.0 | New SDK/runtime announcement; no blanket GA claim inferred from version number [12] | Add to the harness comparison. This is not a consequence of our MAF package upgrade and does not require rewriting our agent. |
| Voice agents | GA announced [2] | No voice interaction in our demo. Catalogue/book update only; a radio/dispatcher scenario would be separate scope. |
| Microsoft 365 accounts for agents | Preview [2] | Our delegated Work IQ access does not give the agent its own mailbox/account. Explain the identity distinction; do not add write capabilities to the demo. |
| Toolboxes and browser automation updates | Announced; individual new capabilities not established by the overview [2] | Existing MCP/Work IQ integration remains. Toolboxes and browser automation already existed; do not label the entire products new or infer new API support. |
| Foundry Local on Azure Local | Inference GA; Agent Service/guardrails preview [16] | Our local Functions process still calls a cloud model. It is not a demonstration of Foundry Local or disconnected operation. |
| Windows 365 for Agents integration with Foundry | Preview planned for early 2027 [17] | Future catalogue entry, not a capability available to demonstrate now. |

### Evaluation, improvement and operations

| Announcement | Status at this review | What Caesarea has / what to change |
| --- | --- | --- |
| Insights | Service GA [1, 2] | Read-only reader exists; no configured monitor or genuine finding was available in the last check. Correct service status; retain SDK beta labels. |
| Multi-turn evaluation; Agent optimizer | GA [1] | ASSERT already exercises behavioral and multi-turn cases. Foundry's evaluator/optimizer is a separate service path, not something those tests prove integrated. |
| Trace annotations, trace-derived/golden datasets, safety rubrics | Preview features [2] | Frozen ASSERT fixtures are useful inputs, but are not a managed golden-dataset integration. Add a human review step to the improvement diagram. |
| Insight-to-optimizer handoff, skill optimization, cost optimization | Preview [2] | No implementation. Keep generated changes as reviewed candidates; do not let optimization change Hub policy or publish automatically. |
| External-agent observability and evaluation | GA [5] | Best optional extension to the local demo: instrument compatible traces into Application Insights and register the agent in Foundry while retaining its endpoint. Registration alone supplies no telemetry. |
| Cross-agent debugging; Trace Analytics/critical-path analysis | Public preview [5] | We have local MAF spans and HTTP correlation. A full Operations → Security/Workforce trace in the cloud is not yet verified. This would make the A2A teaching point tangible. |
| Fleet view; per-agent token/cost tracking | Fleet public preview; cost private preview [6] | Aspire is local service visibility, not this fleet experience. Do not transfer Foundry Cost's maturity label to the Monitor cost feature. |
| Cost in Foundry | Preview [2] | Add outcome quality, latency and cost to the comparison narrative. No cost dashboard capture exists for this demo. |
| Protected GenAI-content tables and PII redaction | Public preview [5] | Sensitive-content export stays off. Adding cloud telemetry requires explicit routing/access review; redaction does not prove every sensitive value was removed. |
| Azure Monitor Health Models | GA [5] | Useful future mapping from service dependencies to workload impact. It does not replace the Energy Hub's authoritative operational twin. |
| Observability Skills Plugin and Grafana MCP | Announced capabilities; verify each enabled surface [5] | Potential developer investigation tools. They do not become operational-agent tools automatically. Retain separate control over remedial actions. |

### Knowledge, identity and governance

| Announcement | Status at this review | What Caesarea has / what to change |
| --- | --- | --- |
| Expanded Microsoft IQ platform; hierarchical business plugins | IQ platform GA; business plugins Frontier [8] | Explain the distinction between our repository Skills/local knowledge and centrally governed, inherited organizational guidance. IQ itself predates Ignite. |
| Foundry IQ knowledge graphs and Microsoft Retriever 1 | Public preview [9] | We use local knowledge and CaseMemory, not Foundry IQ. Future asset → work order → procedure retrieval can demonstrate citations while preserving domain access boundaries. |
| Foundry IQ permissions, chunking/image enrichment, SQL/file sources | GA additions [9] | Useful knowledge-layer comparison; no retrieval migration needed for the current L-417 story. |
| Foundry IQ Fabric sources and Serverless | Fabric semantic-model/OneLake additions preview; Serverless GA scheduled December [9] | Label future availability precisely. Do not describe Serverless as GA today. |
| Work IQ retrieval/business grounding and task capabilities | GA additions; memory, authoring, Listen and agent-approval policy preview [10] | Existing read-oriented Work IQ story can be updated. Preview approval policy is not our deterministic repair approval; Listen is not a verified Hub event trigger. |
| Web IQ broader availability and sources | Broad availability; Commerce private preview [11] | Weather could enrich a future city investigation. External weather remains supporting evidence, never lamp state or authorization. |
| Agent 365/Entra governance expansion | Feature-specific preview/GA/Frontier rollout [13, 14] | Extend existing estate-governance slides with dynamic risk, connected identities, federation and gateway/local-agent coverage. Our custom local MAF agent is not automatically enrolled. |
| Agent Grid, AKS Agent Sandbox and flex nodes | Preview [3, 4] | Add infrastructure controls to the security/runtime map. Network policy and isolated code execution do not persist approvals or establish exactly-once device effects. |

Agent 365 detail matters: plugins/skills governance is GA; Azure AI Gateway/LiteLLM governance and
dynamic risk are preview; Business Analytics is Frontier preview; the governance action center
is expected in private preview in December [1, 13]. Keep these distinct from existing Agent 365 GA.

### Models and training

| Announcement | Status at this review | What Caesarea has / what to change |
| --- | --- | --- |
| Microsoft-Decision-1.1 | GA, including use as an evaluation judge [2, 7] | Candidate for bounded routing/classification or a judge experiment. Never replace deterministic authorization or remediation policy with a probabilistic decision. |
| Model router expansion | Larger pool, default selection in supported projects, AI Gateway integration [7] | We use explicit model deployments. Evaluate tool calling, structured outputs, reasoning replay, latency and cost before introducing routing. No default-model change now. |
| MAI-Code-2 / Code-2-Flash, MAI-Image-2.7 family, MAI-Cyber-2-Flash | Coding available in GitHub; Code-2-Flash limited Foundry preview; images public preview; cyber through MDASH [7] | Catalogue/developer-tool updates. Do not imply all these are generally available Foundry inference endpoints. |
| fal video models, Baseten and upcoming Meta models | Video/Baseten preview; Meta models coming soon [1, 7] | Outside this demo's core needs. Baseten provisioned throughput is planned for December; no reason to add inference infrastructure now. |
| Managed Compute | GA with Data Zone availability [7] | Dedicated model inference is another hosting choice; distinguish model hosting from agent hosting. |
| Managed Training and Programmed Training | New Managed Training capabilities preview; Programmed Training public preview [1, 7] | No training pipeline or suitable training dataset here. Book/catalogue coverage only; evaluate simpler instruction/tool improvements first. |
| GPT-6.2 in Foundry | Keynote announcement; deployment availability not established here [18] | Candidate inventory, not proof it is deployable in our project. Keep the tested model until comparative evaluations justify a switch. |

The model announcements span different products and availability levels. Do not turn “announced
at Ignite” into a blanket model upgrade or claim a performance/cost improvement from another
customer's benchmark.

## AKS: what is actually different

Our hosting slides already permit a container on Kubernetes. The new option connects Foundry's
agent/model deployment experience to infrastructure owned by the customer. It is not merely the
observation that a .NET container can run on AKS, and it is not the Functions Durable Extension.

There is a material rollout discrepancy: the Book of News describes the infrastructure capabilities
as preview, while the Foundry launch article says the hosted-agent AKS preview is coming soon
[1–3]. Present it as **announced preview; rollout/eligibility must be verified**, not a live option
already exercised by this solution.

The current [Cognitive Services account schema][19] also documents a significant provisioning
constraint: `agentHostingConfigurations` is creation-only, has exactly one entry and is immutable.
It requires a customer cluster, management and federated workload identities, and storage in the
cluster's subscription/region with suitable network access. Existing accounts without this property
cannot simply add it. Our `infra/modules/foundry.bicep` uses API `2025-06-01`, with no such property
or AKS resources. A future spike must verify rollout, regions, cluster requirements, identity,
networking, storage, ingress, telemetry and the exact .NET image/protocol combination on a separately
planned account. No verified end-to-end .NET quickstart or regional matrix was established here.

## Concrete slide and demo updates

The verified release text gives these anchors:

| Existing material | Gap | Recommended change |
| --- | --- | --- |
| H08 p. 19, MAF Agent Harness | New SDK/runtime vocabulary | Compare MAF, Copilot SDK 2.0 and Foundry's managed Copilot harness. No claim that the existing MAF demo uses all three. |
| H08 pp. 45–46, MAF Hosting; W20 p. 41 | Kubernetes appears only as generic self-hosting | Add rows for application-hosted containers, local Functions + emulator, Azure Functions + DTS, Foundry-managed hosted agents, and Foundry-integrated customer AKS (announced preview). Separate compute ownership, state durability, identity and rollout. |
| H08 pp. 50–51; W20 pp. 35 and 45 | Existing governance stack lacks new platform mappings | Place Agent Grid/sandbox at infrastructure/tool boundaries and Agent 365/Entra at estate/identity boundaries. Preserve deterministic Hub authorization. |
| H08 pp. 52–55; W20 p. 38 | Improvement loop is already present but mainly conceptual | Show trace → Insight → expert annotation → regression/evaluation → candidate optimization → human promotion. Label each service/preview separately and retain ASSERT. |
| H08 p. 57, wider catalogue | New knowledge/model capabilities and changed maturity | Update IQ graphs/Retriever, routing, training, voice and external observability; date the table. Keep deferred capabilities out of the live-demo claims. |

Recommended work order:

1. Correct current service-status wording and link this dated inventory. Keep the published
   September PDFs intact and retain selected SDK beta/preview labels.
2. Complete and record the v2 local restart rehearsal with pending/terminal checkpoints.
3. Finish genuine Insights evidence and the existing Routines rehearsal. Routines was already GA
   in September; this review found no reason to relabel it an Ignite launch.
4. Consider a bounded external-agent telemetry exercise: instrument one local Operations Agent,
   verify compatible spans and identity/version attribution, register it in Foundry, then show one
   real trace. This is proposed cloud integration, not yet done. Extend to peer-agent spans only
   after verifying each provider's propagation. It can coexist with a local Functions worker.
5. Prepare the hosting, context and improvement comparisons; fold governance into existing slides
   rather than lengthening the talk with every announcement. Use captured/live labels honestly.
6. Update the book afterward: Chapter 7 harnesses; Chapters 8–11 context/authority and recovery;
   Chapter 12 runtime/IQ options; Chapter 13 evidence and optimization; Chapter 14 labs and dated
   availability; Appendix A inventory. Preserve the original edition and source links.

Do not introduce AKS, Agent Grid, voice, new model defaults, training or automatic optimization
into the current live system just to match the announcement catalogue. Each needs a teaching
requirement, an integration plan and its own evidence.

## Discrepancies and follow-up

- AKS rollout: preview announcement versus “coming soon” in the Foundry article, as above.
- Router model count: Book of News says 50; the detailed model article says 44. Say expanded pool,
  and verify the actual supported models before showing a number.
- Foundry IQ Serverless: December GA is a future milestone at this review date.
- Service GA does not establish .NET SDK GA, region eligibility, tenant access or tested behavior.
- Recheck after the November 20 closing announcements before freezing the new slides. No recurring
  monitoring task or deployment has been scheduled by this review.

## Sources

Unless noted otherwise, these are November 17, 2026 announcements. The schema supplies current
implementation constraints, not evidence of when the product was announced.

1. [Microsoft Ignite 2026 Book of News](https://news.microsoft.com/events/ignite/2026/book-of-news/).
2. [Continuous improvement comes to Microsoft Foundry](https://azure.microsoft.com/en-us/blog/continuous-improvement-comes-to-microsoft-foundry/).
3. [Azure Catalyst and new AI infrastructure](https://azure.microsoft.com/en-us/blog/introducing-azure-catalyst-agentic-modernization-and-new-ai-infrastructure-innovations/).
4. [Introducing Agent Grid](https://techcommunity.microsoft.com/blog/azurenetworkingblog/introducing-agent-grid/4573407).
5. [Agent observability in Azure Monitor at Ignite 2026](https://techcommunity.microsoft.com/blog/azureobservabilityblog/agent-observability-in-azure-monitor-at-ignite-2026/4573966).
6. [What's new in Azure Monitor at Ignite 2026](https://techcommunity.microsoft.com/blog/azureobservabilityblog/whats-new-in-azure-monitor-at-ignite-2026/4573974).
7. [Foundry models, routing, training and Managed Compute](https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/from-model-choice-to-your-durable-advantage-build-ai-that-keeps-improving/4573609).
8. [Introducing the new Microsoft IQ](https://azure.microsoft.com/en-us/blog/introducing-the-new-microsoft-iq-putting-your-organizations-intelligence-to-work-in-every-agent/).
9. [Foundry IQ knowledge graphs](https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/foundry-iq-introducing-knowledge-graphs-engineered-for-agents/4572683).
10. [Work IQ at Ignite 2026](https://techcommunity.microsoft.com/blog/microsoft_365blog/whats-new-in-work-iq-at-ignite-2026-agents-that-get-work-done/4572219).
11. [Web IQ broad availability](https://blogs.bing.com/webiq/2026/11/web-iq-is-now-broadly-available-build-better-agents-that-keep-up-with-the-world/).
12. [Microsoft Copilot SDK 2.0](https://commandline.microsoft.com/microsoft-copilot-sdk-2-0-github/).
13. [Agent 365 at Ignite 2026](https://techcommunity.microsoft.com/blog/agent-365-blog/whats-new-in-microsoft-agent-365-ignite-2026/4571753).
14. [Microsoft Entra announcements](https://techcommunity.microsoft.com/blog/microsoft-entra-blog/ai-is-changing-access-see-how-entra-helps-you-stay-ahead-/4556456).
15. [October Foundry roundup](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-october-2026/) — November 5, pre-Ignite chronology.
16. [Sovereign Private Cloud and Foundry Local](https://techcommunity.microsoft.com/blog/azurearcblog/whats-new-with-sovereign-private-cloud-at-microsoft-ignite-2026/4573886).
17. [Windows 365 for Agents](https://techcommunity.microsoft.com/blog/windows-itpro-blog/windows-365-at-ignite-2026-introducing-new-cloud-pc-capabilities-for-people-agen/4570006).
18. [Ignite keynote](https://blogs.microsoft.com/blog/2026/11/17/ignite-2026-building-the-frontier-ecosystem/).
19. [Cognitive Services account schema, 2026-09-15-preview][19].

[19]: https://learn.microsoft.com/en-us/azure/templates/microsoft.cognitiveservices/2026-09-15-preview/accounts
