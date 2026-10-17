# Sources and basis of the research (bibliography)

- **Research date:** 2026-10-14 to 2026-10-15
- **Rule:** all sources are given as links to the originals, with the access date and the scope of reading.
- **Related:** [`analysis/sources-index.md`](analysis/sources-index.md) (number ↔ URL), [self research](../2026-10-14-llm-self-research/README.md) §13, [pattern selection](../2026-10-15-pattern-selection/README.md) §2.
- **Publication dates:** given only where they appear on the page or in the arXiv metadata. The rest: "no data".
- **Confidence of use:** **full text** = read in full; **abstract/summary** = only the abstract or the fetching tool's summary; **snippet** = a search engine result; **gap** = could not be read.

---

## A. The user's original list (18 URLs)

### A1. bradmurry.com (14 URLs; blog index + 13 posts)

Access: 2026-10-14. The index https://bradmurry.com/software/ confirmed 13 posts.

| Post | URL | Usage | Scope of reading |
|---|---|---|---|
| Agents in Production (intro) | https://bradmurry.com/software/agents-in-production-intro/ | the "deterministic shell" thesis, the series' three claims | full text |
| Self-evolving agents (intro) | https://bradmurry.com/software/self-evolving-agents-intro/ | RUN/MEASURE/DISCOVER/BUILD loop, three signals, disciplines | full text |
| Reactive Reducer Patterns (index of 13 patterns) | https://bradmurry.com/software/reactive-reducer-patterns/ | catalog of 13 patterns and their limitations | full text |
| Reactive Reducer Patterns (intro serii) | https://bradmurry.com/software/reactive-reducer-patterns-intro/ | three reducer rules, two execution targets | full text |
| Walking a Reentrant Human-in-the-Loop FSM, Step by Step | https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/ | accumulation instead of clearing, `size()` comparisons, parking | full text |
| Binding LLMs to Finite State Machines Using the Reactive Reducer Pattern | https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/ | micro-steps, DSL, durability | full text |
| sparkctl | https://bradmurry.com/software/sparkctl/ | serving models from configuration | full text |
| DSL / AI partition | https://bradmurry.com/software/dsl-ai-partition/ | DSL ↔ AI partition as a permission boundary | full text |
| AI annotations | https://bradmurry.com/software/ai-annotations/ | `@!` annotations | full text |
| PyFlink (intro) | https://bradmurry.com/software/pyflink-intro/ (2023-07-15 per the page) | streaming vs micro-batching | full text |
| API Architecture for Massive Scale | https://bradmurry.com/software/apis-at-massive-scale/ | CDC, pre-computed paths, Redis + S3 | full text |
| Devops Contract | https://bradmurry.com/software/dev-ops-contract/ | command + phase contract | full text |
| Config Driven Data Transforms (pt1) | https://bradmurry.com/software/config-driven-transforms/ | source/sink DSL, validation | full text |
| (blog index) | https://bradmurry.com/software/ | only confirmation of the list of posts | abstract/list |

### A2. The rest of the list

| Source | URL | Status |
|---|---|---|
| Medium: Every AI agent needs these 12 design patterns… | https://medium.com/@mohsho10/every-ai-agent-needs-these-12-design-patterns-or-it-will-eventually-fail-4970ebd4cb89 | **gap** (HTTP 403, no account; the user gave up) |
| Mastra: Patterns of Building AI Agents | https://mastra.ai/books/patterns-of-building-ai-agents | **gap** (chapter content behind an email gate; only the table of contents was read) |
| AutoGen: design patterns, intro | https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html | full text |
| AutoGen: multi-agent debate | https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html | full text |

---

## B. Scope extension: Brad's articles outside the original list

Found from links on the index pages; **URLs and titles verified 2026-10-15 (HTTP 200)**. Read on 2026-10-14.

### B1. 13 orchestration patterns (as "Implemented via the Reactive Reducer")
All under `https://bradmurry.com/software/<slug>`:

| # | Pattern | slug |
|---|---|---|
| 1 | Sequential Pipeline | `sequential-pipeline-pattern-implemented-via-reactive-reducer` |
| 2 | Parallel Fan-Out / Fan-In | `parallel-fan-out-fan-in-pattern-implemented-via-reactive-reducer` |
| 3 | Supervisor / Router | `supervisor-router-pattern-implemented-via-reactive-reducer` |
| 4 | Hierarchical Manager / Worker | `hierarchical-manager-worker-pattern-implemented-via-reactive-reducer` (2026-09-04 per the page) |
| 5 | Peer Handoff / Swarm | `peer-handoff-swarm-pattern-implemented-via-reactive-reducer` |
| 6 | Debate / Consensus | `debate-consensus-pattern-implemented-via-reactive-reducer` |
| 7 | Blackboard / Shared-State | `blackboard-shared-state-pattern-implemented-via-reactive-reducer` (2026-09-07) |
| 8 | Planner / Executor | `planner-executor-pattern-implemented-via-reactive-reducer` |
| 9 | Generator / Critic / Refiner | `generator-critic-refiner-pattern-implemented-via-reactive-reducer` |
| 10 | Auction / Contract-Net | `auction-contract-net-pattern-implemented-via-reactive-reducer` (2026-09-10) |
| 11 | Event-Driven / Publish-Subscribe | `event-driven-publish-subscribe-pattern-implemented-via-reactive-reducer` (2026-09-11) |
| 12 | Quorum / Voting | `quorum-voting-pattern-implemented-via-reactive-reducer` (2026-09-12) |
| 13 | Dynamic Team Formation | `dynamic-team-formation-pattern-implemented-via-reactive-reducer` |

Example of a full address: https://bradmurry.com/software/debate-consensus-pattern-implemented-via-reactive-reducer

### B2. Series
| Series / part | Title | slug |
|---|---|---|
| Self-evolving 1 | Log and Telemetry Triage at Scale | `self-evolving-agents-pt-1` |
| Self-evolving 2 | Data Quality Audits over Messy Data | `self-evolving-agents-pt-2` |
| Self-evolving 3 | Support Ticket Mining at Scale | `self-evolving-agents-pt-3` |
| Self-evolving 4 | Entity Resolution and Data Fusion | `self-evolving-agents-pt-4` |
| Agents in Production 1 | Agents Are Distributed Systems Now | `agents-in-production-pt-1` |
| Agents in Production 2 | What Kafka and Flink Taught Me About Building Reliable AI Agents | `agents-in-production-pt-2` |
| Agents in Production 3 | Deterministic Shell, Probabilistic Core | `agents-in-production-pt-3` |
| Agents in Production 4 | Stop Treating Agent Workflows Like Task Lists | `agents-in-production-pt-4` |

Address: `https://bradmurry.com/software/<slug>`. Parts 5–7 of the "Agents in Production" series are only mentioned in the text and were not read.

### B3. Second batch of AutoGen pages (added by the user later)
All under `https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/`: `concurrent-agents.html`, `sequential-workflow.html`, `group-chat.html`, `handoffs.html`, `mixture-of-agents.html`, `reflection.html`, `code-execution-groupchat.html`. Full text, read on 2026-10-14.

---

## C. External sources (self research and pattern selection)

Access: 2026-10-14 (part 1) and 2026-10-15 (part 2). Details and quotes: [self research §13](../2026-10-14-llm-self-research/README.md), [pattern selection §2](../2026-10-15-pattern-selection/README.md).

| Source | URL | Publication date | What it was used for | Scope of reading |
|---|---|---|---|---|
| Anthropic, Building Effective Agents | https://www.anthropic.com/engineering/building-effective-agents | no data | workflow vs agent, 5 patterns, "simplest solution first" | full text (tool summary) |
| Anthropic, multi-agent research system | https://www.anthropic.com/engineering/multi-agent-research-system | no data | effort-scaling rule, 4×/15× cost, poor fits | tool summary |
| Anthropic, prompt caching (documentation) | https://platform.claude.com/docs/en/build-with-claude/prompt-caching | as of 2026-10-14 | prices, TTL, invalidation | documentation (first 100k characters) |
| Anthropic, streaming (documentation) | https://platform.claude.com/docs/en/build-with-claude/streaming | as of 2026-10-14 | SSE in the chatbot | documentation (beginning) |
| OpenAI, prompt caching (documentation) | https://developers.openai.com/api/docs/guides/prompt-caching | as of 2026-10-14 | minimum, TTL, prices | documentation |
| OWASP Top 10 for LLM Applications 2025 | https://genai.owasp.org/llm-top-10/ | 2025 edition | chatbot risks | list |
| Zheng et al., LLM-as-a-Judge | https://arxiv.org/abs/2306.05685 | 2023-06-09 (v4 2023-12-24) | judge biases, >80% agreement with humans | abstract |
| Gu et al., Survey on LLM-as-a-Judge | https://arxiv.org/abs/2411.15594 | no data | taxonomy of biases | snippet |
| LLMs-as-Judges survey | https://arxiv.org/abs/2412.05579 | no data | additional biases | snippet |
| Shinn et al., Reflexion | https://arxiv.org/abs/2303.11366 | 2023-03-20 (v4 2023-10-10) | episodic memory, HumanEval | abstract |
| Huang et al., LLMs Cannot Self-Correct Reasoning Yet | https://arxiv.org/abs/2310.01798 | 2023-10-03 (v2 2024-03-14) | self-correction requires an external signal; debate vs self-consistency | abstract; debate numbers from snippets (low confidence) |
| Madaan et al., Self-Refine | https://arxiv.org/abs/2303.17651 | no data | reflection with structure | snippet |
| Agrawal et al., GEPA | https://arxiv.org/abs/2507.19457 | 2025-07-25 (v2 2026-02-14) | prompt optimization | abstract |
| Belcak et al., SLM are the Future of Agentic AI | https://arxiv.org/abs/2506.02153 | 2025-06-02 (v3 2026-09-22) | position paper on SLMs | abstract |
| Packer et al., MemGPT | https://arxiv.org/abs/2310.08560 | 2023-10-12 (v2 2024-02-12) | context management | abstract |
| Chhikara et al., Mem0 | https://arxiv.org/abs/2504.19413 | 2025-04-28 | long-term memory (authors' claims) | abstract |
| Yao et al., ReAct | https://arxiv.org/abs/2210.03629 | 2022-10-06 (v3 2023-03-10) | agent loop | abstract |
| Liu et al., Agent Design Pattern Catalogue | https://arxiv.org/abs/2405.10467 | 2024-05-16 (v4 2024-11-06) | catalog of 18 patterns, decision model (§5.1) | HTML v4 (sections 4–5.1) |
| Kim et al., Towards a Science of Scaling Agent Systems | https://arxiv.org/abs/2512.08296 | 2025-12-09 (v3 2026-04-08) | architecture predictor (87%), sequential vs decomposable | abstract; **versions diverge** |
| Google Research, post on scaling agent systems | https://research.google/blog/towards-a-science-of-scaling-agent-systems-when-and-why-agent-systems-work/ | no data | selection rules and error amplification | tool summary |
| Zhang et al., Stop Overvaluing Multi-Agent Debate… | https://arxiv.org/abs/2502.08788 | 2025-02-12 (v3 2025-06-21) | debate vs simple baselines, heterogeneity | abstract |
| Yue et al., MasRouter (ACL 2025) | https://aclanthology.org/2025.acl-long.757 | 2025 | learned collaboration router | search engine summary |
| Hugging Face PEFT, LoRA | https://huggingface.co/docs/peft/main/en/conceptual_guides/lora | no data | LoRA | documentation |
| karpathy/nanoGPT | https://github.com/karpathy/nanoGPT | README from 2025-11 (deprecated) | training from scratch | README |
| karpathy/nanochat | https://github.com/karpathy/nanochat | no data | training from scratch | README |

Blogs and tutorials (model hosting comparisons, chatbot security, LoRA guides, caching): found via search on 2026-10-14, marked in the self research as **low confidence**; they are not the basis of any conclusion without confirmation in our own sources.

---

## D. Internal repository files on which the setup assessment was based

Only files tracked in git: `.github/AGENT-PIPELINE.md`, `.github/agents/*.md`, `.github/skills/research-gatherer/SKILL.md`, `.github/instructions/{agent-workflow,session-continuity,mcp-routing,docs-index,dotnet}.instructions.md`, `.github/context/{agent-decisions,anti-patterns-critical,repo-index}.context.md`, `.github/context-cost-analysis.md`, `.github/setup-state.md`, `.claude/settings.json`, `.claude/agents/`, `tools/hooks/README.md`, `docs/architecture/bounded-context-map.md`, code: `ECommerceApp.Domain/Catalog/Products/Product.cs`, `ECommerceApp.Application/Presale/Checkout/Services/IOrderAccessService.cs`, `ECommerceApp.UnitTests/Architecture/`, `ECommerceApp.Web/Startup.cs`, `ECommerceApp.Web/wwwroot/js/`.
Git history: the last 200 commits (2026-05-27 to 2026-10-14) for simulating task sizes.

---

## E. What the most important conclusions rest on

| Conclusion | Basis | Strength |
|---|---|---|
| Evidence gates in research add value | measurement in this session: 5 of 6 WebFetch responses fragmentary; 1,373 of 1,448 quotes matching | measured in this research |
| An LLM critic/judge requires an external signal and calibration | Zheng 2023, Huang 2023, Brad (agreement only directional) | medium |
| Debate is not better than a simple baseline by default | Zhang 2025 (abstract), Huang (snippet) | medium–low |
| Multi-agent coordination harms sequential tasks | Kim 2025, Anthropic | medium (divergent versions of Kim) |
| A custom decision layer has no evidential advantage over importing a framework (nor the reverse) | Anthropic (frameworks can hide prompts), prose/code discrepancies in AutoGen | low–medium |
| Chatbot: key on the server side, limits, no write tools | OWASP LLM Top 10 2025 | high (risk list) |
| The product has no SKU or URL | `Product.cs` in the repo | high (code) |
| All claims about the "Aegis" runtime | Brad only | low (one author, no public verification) |

---

## F. Quotes
- The findings contain only short quotes (≤ 40 words) with the source given; the full texts of the articles are available at the links above.
