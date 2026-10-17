# Research Findings Summary (Phase 3, step 1)

Date: 2026-10-14 · Type: external · Actor-agnostic · Brief: [`../planning/research-brief.md`](../planning/research-brief.md)

## Research Question
Collect and cross-verify the content of the given sources (14 bradmurry.com URLs, Medium, Mastra, 2 AutoGen pages) and identify agent patterns/skills; ultimately to use them in a multi-agent debate about applications of AI in production. **This document is only a collection of facts, without recommendations** (the recommendation is a separate step, see `../debate/`).

## Sources Investigated

| Group | Sources | Full text? | Raw files | Findings (final) |
|---|---|---|---|---|
| Brad: agentic core (6 posts from the list) | binding-llms-to-FSM, walking-HIL-FSM, 13-patterns index, agents-in-production-intro, self-evolving-intro, reactive-reducer-intro | yes | 03–08 | `bm-agentic-core-raw-findings.md` (132 sections) + `bm-patterns-1-7-raw` + `bm-patterns-8-13-raw` |
| Brad: 13 per-pattern articles | patterns 1–13 | yes — **outside the list of 18 URLs** (scope extension) | 09–21 | `bm-patterns-1-7-raw` (51), `bm-patterns-8-13-raw` (77) |
| Brad: "Self-evolving" series 1–4 and "Agents in Production" 1–4 | 8 articles | yes — **outside the list of 18 URLs** | 22–29 | `bm-series-parts-raw` (55) |
| Brad: infra posts from the list | apis-at-massive-scale, dev-ops-contract, config-driven-transforms, sparkctl, dsl-ai-partition, ai-annotations, pyflink-intro | yes | 30–32, 42–45 | `bm-infra-raw` (73) |
| AutoGen (9 design-patterns pages) | intro, concurrent-agents, sequential-workflow, group-chat, handoffs, mixture-of-agents, multi-agent-debate, reflection, code-execution-groupchat | yes (`curl` + HTML→text conversion) | 33–41 | `autogen-raw-9pages` (14 sections, ~83 findings) |
| Mastra *Patterns of Building AI Agents* | 1 page | **no** — content behind an email gate; table of contents only | — | `mastra-book-findings.md` (gap) |
| Medium *12 design patterns* | 1 article | **no** — HTTP 403, the user gave up | — | `medium-12-patterns-findings.md` (gap) |
| Self research (additional) | papers, documentation (caching, OWASP, LoRA…) | abstracts/documentation | — | [`../../2026-10-14-llm-self-research/README.md`](../../2026-10-14-llm-self-research/README.md) |

The findings files from the first round (from WebFetch, fragmentary) — `bm-agents-fsm`, `bm-data-infra-ops`, `autogen-patterns` — are **superseded** by the `*-raw` versions wherever the full text exists; they are kept as an audit trail.

## Key Findings (facts, no recommendations)

### A. Thesis common to Brad's sources (reactive reducer)
- The context (a typed store) is the only source of truth; actors (agent, human, tool, system) only *contribute values*, never "request a transition"; the machine itself takes the first edge whose guards pass (micro-step loop, cap 128, parking, journal). [`03`, `08`]
- Legality is structural (an undeclared edge cannot fire); the reducer is pure (effects only in entry/exit/onTransition actions); guards are CEL. [`03`]
- The same FSM definitions are meant to run interactively or compiled to Mutiny/Flink. [`03`, `08`]
- Recurring idioms (13 patterns): *many contributors → one map field keyed by the runtime's author*; *accumulate what you keep, consume what re-arms*; *when nothing is destroyed, every test becomes a comparison* (`size(a) > size(b)`). [`05`, `15`–`17`]
- "A deterministic shell around a probabilistic core": distributed-systems patterns (idempotency key, journal, replay, checkpoint/savepoint, backpressure, DLQ) carried over to agents; "timeout is UNKNOWN, not FAILED". [`26`–`29`]
- The self-evolving agent loop: RUN → MEASURE → DISCOVER → BUILD; three signals (efficiency, correctness, quality); disciplines: one change at a time, check before you build, report a regression, report a ceiling. [`07`, `22`–`25`]

### B. Catalog of 13 patterns (Brad) — mechanism and stated limitation

| # | Pattern (category) | Key mechanism | Limitation stated by the author |
|---|---|---|---|
| 1 | Sequential Pipeline (Flow) | a field per stage; the back-edge consumes the trigger, keeps the notes | `false` is "present" — a re-armable field cannot be a bool |
| 2 | Parallel Fan-Out/Fan-In (Flow) | requirements = fan-out; guard `size(findings) >= N` = join; `required:false` when the join ≠ all-of | dynamic variant via `fork()` with bounded re-dispatch |
| 3 | Supervisor/Router (Authority) | enum `route` + hub; `finding` consumed, `notes` accumulated | routing twice to the same specialist "does nothing" if the field is not consumed |
| 4 | Hierarchical (Authority) | `spawn … alias … into`, `fsmCompleted[alias]`; a separate store per level | no declarative seeding of the child's context (the brief goes via an addressed write) |
| 5 | Peer Handoff/Swarm (Delegation) | enum `handoff`, `trail` (list) via an exit hook, `stuck` state | the no-progress guard does not cover turns; the mesh is verbose |
| 6 | Debate/Consensus (Reasoning) | 3 debaters + judge; `positions` kept, `critiques` cleared; enum `consensus/continue`; `maxIterations: 8` | shared blind spots require model diversity, not a different loop; cost = debaters × rounds × (positions+critiques) + judge |
| 7 | Blackboard (Coordination) | `facts` (list, stays) vs `hypotheses` (map, cleared) | collections 1 level deep; fixed schema; no opportunistic scheduler |
| 8 | Planner/Executor (Planning) | `directive`/`plan` separately; replan consumes 3 triggers; `history` | the plan is text, not a task graph |
| 9 | Generator/Critic/Refiner (Reasoning) | no refine state; `size(drafts) < 4` as a budget; `stalled` state | no "apply diff" — the refiner re-emits the whole artifact |
| 10 | Auction/Contract-Net (Delegation) | `bids`/`confidence` maps; `winner` enum; delegate from the template `{{ctx.winner}}` | single-round auction; a bid is not a record |
| 11 | Event-Driven/Pub-Sub (Coordination) | the reducer as an internal bus; reactors = requirements | **"half real"**: `emit` only logs, no ingress/egress |
| 12 | Quorum/Voting (Reasoning) | `votes` map keyed by author; guard `size(filter(...)) >= 2` | one-shot; error correlation cannot be fixed by the mechanism |
| 13 | Dynamic Team Formation (Delegation) | `roster` = list of enums (registry as a type); a single `phase` state; order = `roster[size(outputs)]` | swapping a member mid-run does not work "for free" |

### C. AutoGen — 8 patterns (core-user-guide)
Concurrent agents, sequential workflow, group chat (the manager picks the speaker), handoffs (the delegate tool returns a topic), mixture-of-agents (layers of workers + aggregator, direct messaging), **multi-agent debate**, reflection (coder + reviewer, JSON `approved`), code-execution-groupchat. Details and quotes: `findings/autogen-raw-9pages-findings.md`.
- **Debate (AutoGen):** 4 identical solvers + an aggregator; sparse ring topology; "fixed number of rounds" (code `max_round=3`); aggregation = **majority vote** `max(set(answers), key=answers.count)`; **no judge/critic role**; the page says nothing about tie-breaking or evaluation; the only example ends with full agreement.
- **Key difference for the debate:** Brad = judge + enum + critique + budget; AutoGen = peer solvers + voting + a fixed number of rounds.

### D. Infra/AI posts (from the list)
- *apis-at-massive-scale*: hash-before-write → clean CDC → recompute once on change → pre-computed paths `/{category}/{type}/{key}` → dual-write Redis + S3 as a fallback. Outcome: a self-description of "billions of requests".
- *dev-ops-contract*: contract = a command interface (`make deps/test/build/deploy`) + a phase lifecycle with short-circuit.
- *config-driven-transforms*: source/sink DSL; validation at 4 points; versioning; JSON-in/JSON-out tests.
- *sparkctl*: configuration-driven model serving (`cluster.yaml` + `recipes/*.yaml`, `apply`, sha256 drift detection, a uniform OpenAI-compatible endpoint on LiteLLM). No description of an agent pattern.
- *dsl-ai-partition*: the DSL as a permission boundary between AI-written orchestration and human-written critical components; two approval paths (fast/careful). The "90%/80%" numbers are unsupported.
- *ai-annotations*: the `@!` annotation standard; no description of how the agent runtime loads them.
- *pyflink-intro*: a short post — micro-batching ≠ streaming.

### E. Mastra (table of contents only — chapter content unavailable)
1. From Wishlist to Working Agent (incl. Human-in-the-Loop) 2. Intro to Context Engineering (compress context, share context between subagents…) 3. From MVP to Production (failure modes, evals, SME labels, production datasets) 4. Autonomy Is a Two-Edged Sword (lethal trifecta, sandbox, access control, guardrails) 5. What's Next(ish).

## Gaps and Uncertainties
- **Medium** — 0 facts. **Mastra** — chapter titles only. Patterns from these sources **cannot** enter the catalog as established.
- All claims about the "Aegis" runtime come from a single author; **I did not find a publicly verified implementation** in the collected material (the sparkctl/rejected post: the mention of the aegis repo was not verified).
- All "baseline vs optimum" metrics (e.g. tokens 612,400 → 26,400) are **self-descriptions without a trial, without a number of repetitions and without information on whether these are real runs** (gatherer `bm-series-parts-raw`).
- Parts 5–7 of "Agents in Production" and acts II–III are only mentioned, not available.
- Quotes were verified by a script — see `01-cross-source-verification.md` §5.
