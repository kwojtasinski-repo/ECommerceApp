# Application ideas: the ECommerceApp application and the Claude Code / Copilot setup

- **Date:** 2026-10-14
- **Status:** **hypotheses to be tested**, not implementation recommendations. The outcome of the conversation after the research and the debate ([`debate/decision.md`](debate/decision.md)).
- **What I reviewed:** the repo structure, [`docs/architecture/bounded-context-map.md`](../../architecture/bounded-context-map.md), the `.claude/` and `.github/` folders, `.github/AGENT-PIPELINE.md`, `.github/context-cost-analysis.md`, `tools/hooks/README.md`, `.github/instructions/{rag,agent-memory}.instructions.md`, `.claude/settings.json`.
- **What I did NOT review:** the domain code deeper than file names, the volume of orders/returns/tickets (no data), `mcp-routing-eval.prompt.md`, `tools/rag/queries.yaml`, the contents of the six agent folders in the repo root (`classifier`, `requirements-critic`, `linguistic-boundary-verifier`, `test-strategy-reviewer`, `transcriptor-criticor`, `maet-program-classifierator`).
- **Guiding principle (from the debate):** an LLM where the result can be measured on your own data and where a human or a deterministic check stands behind it. The transactional path (reservations, payments, sagas, coupon calculation) stays without an LLM.

---

## A. The ECommerceApp application (the core of the shop)

Today there is no LLM integration in the repo (checked by the debate's pragmatist and confirmed by the user).

| # | Idea | Where in the repo | What it gives | How to measure | "Stop" criterion (proposal) | Risk |
|---|---|---|---|---|---|---|
| A1 | Catalog curation by the admin: a draft of the description/category/tags from a short input, detection of duplicate products | `Catalog/Products`, `Backoffice` | saves the admin's manual work; the admin approves | share of drafts accepted unchanged, minutes per product | accepted unchanged < about half on a sample of 50–100 products | low (human in the loop) |
| A2 | Diagnosis "why is this order stuck": the LLM reads the saga state, the outbox and the payment history (read-only) and explains it in plain language | `Sagas`, `Messaging`, `Payments` | the most painful spot of an asynchronous system; a tool for dev/ops | agreement of the diagnosis with the known cause on artificial cases from the E2E tests | diagnosis correct in fewer than the majority of known cases | low (read-only, internal) |
| A3 | Classification of return/cancellation reasons into a fixed taxonomy + a dashboard | `Fulfillment/Refund` (has `Reason`), `OrderCancelledPayload` | shows why customers return | agreement with about 100 manually labeled cases | agreement low or volume too small for the dashboard to show anything | low; value depends on volume |
| A4 | Semantic search over the catalog | Catalog + search | fewer queries without a result | share of searches without a result and clicks | no improvement relative to the current search | medium (embeddings infrastructure) |
| A5 | A coupon rule draft from a verbal description, validated by the existing parameter schema | `Sales/Coupons` | faster rule creation | share of correct rules on a set of phrases | the admin is faster anyway in the existing UI | low, the gain is rather small |
| A6 | Customer chatbot (FAQ, return policy, order status read-only) | new module | offloads support, provided there is traffic | share of conversations resolved without a human | no traffic/tickets to offload | highest (public traffic) |

**Outside the LLM:** stock reservations, payments, sagas, coupon calculation. Reason: your rules (saga without locks, immediate order status) and the debate (SKIP/DEFER for actions with an effect).

**Method before anything is built:** 1–2 days on an offline test: export about 100 records, run them through the model, count the agreement with manual labels. Weak → stop, before any code exists.

---

## B. The Claude Code / GitHub Copilot setup (how you work on the repo)

### What you already have and how it relates to the research
Data from the repo, not from my memory:

| Pattern from the research | What you have in the repo |
|---|---|
| Deterministic shell, structural legality | GO gate: the `UserPromptSubmit` and `PreToolUse` hooks block `Edit/Write/NotebookEdit` until `IMPLEMENT` (`.claude/settings.json`, `tools/hooks/`, ADR-0033) |
| Human in the loop, explicit loop limits | `AGENT-PIPELINE.md`: planner max-iter 3, implementer max 5 passes, 3 verification attempts (build + unit + integration), `bc-switch` max-iter 10, two HITL points |
| External signal instead of self-critique (reflection) | embedded `@verifier`: build + tests as a deterministic gate |
| Journal / resuming after a restart | `session-continuity.instructions.md`: numbered plan/validation phase files |
| RAG over documentation + knowledge graph | `tools/rag` (Qdrant, MCP), `tools/kg` (Neo4j, ADR-0031) |
| Model routing | `context-cost-analysis.md` §7: cheaper (included) models for exploration, a more expensive one for implementation; `research-gather` gatherers on a cheaper model |
| Memory between sessions | `.github/context/agent-decisions.md` + Claude Code memory |
| Validator from a different model family | your notes show the practice: implementation with one model, independent validation with another |

Conclusion: **you are ahead of most of what the sources describe.** Below are only gaps, not "let's add everything".

### Ideas (order = my assessment of value to cost)

| # | Idea | Why | How to measure | "Stop" criterion (proposal) |
|---|---|---|---|---|
| B1 | **Measure, don't estimate**: `context-cost-analysis.md` rests on assumptions (4 characters = 1 token, an assumed cache hit rate, state from 2026-06-03). Do a real measurement in Claude Code (`/context`, `/cost`) and in Copilot (if the panel shows it) | without a measurement it is not known whether 7,110 tokens of the fixed block and the "warm turn" are true | tokens of the fixed block and the cost of a typical session, before and after | measurement consistent with the estimate ±15% → change nothing |
| B2 | **Slimming down the fixed instruction block using Brad's method** (one change at a time, measure, report the ceiling): the biggest is `mcp-routing.instructions.md` at 2,058 tokens, then `copilot-instructions.md` at 2,184; goal: the same routing quality with fewer tokens. Requires a small set of "golden tasks" with deterministic checks (did the agent use RAG instead of reading the whole file? did it read `agent-decisions.md`?) | this is exactly "prompt optimization" at a scale where you have the data | tokens/turn and the share of golden tasks passed | quality drop on the golden tasks → roll back the change |
| B3 | **Stable prefix = cheaper session**: per the API documentation, changing the tool definitions or the system prefix invalidates the cache of the later levels (tools → system → messages). In practice: don't edit the always-loaded files and don't add/detach MCP servers in the middle of an expensive session. In this session `ecommerceapp-kg` did not connect, so the toolset was different than usual. **Note:** this follows from the Anthropic API documentation; I did not check exactly how Claude Code caches | cheaper long sessions | comparison of session cost with/without tool changes mid-session | no difference in the measurement → drop it |
| B4 | **Event log from hooks** (append-only JSONL): add a `PostToolUse` hook that records the tool, time and result. Today you have hooks only on `UserPromptSubmit` and `PreToolUse`. It lets you reconstruct what the agent actually did when the agent's account diverges from reality (your notes contain a case: a login regression described as "pre-existing") | "The run is the debug artifact" (Brad); post-mortem on facts | whether the log settles the "what did the agent do" dispute in the next incident | no use after a month → turn it off |
| B5 | **Count how much independent validation gives, on your own data**: from the phase notes (plan/validation) make a table "what the validator found that the implementer missed" (e.g. missing tests, the guest payment POST bug, the login regression). This is your own "calibrated judge" and stronger evidence than a 2023 paper | Zheng et al. measured chat, not your workflow | share of phases in which the validator found a real bug | if the validator rarely finds anything → consider cheaper validation |
| B6 | **Multi-agent debate only for contentious ADRs**: e.g. the still-deferred decision on the scope of removing the ADR-0026 dead code. Rules from our debate: different perspectives, a shared evidence base, critiques with source verification, a judge with an enum and a round limit | the cost of a debate is known; the value only where there are real alternatives | whether the debate surfaced an issue that an ordinary plan did not detect | if nothing new → do not repeat |
| B7 | **Claude Code parity with Copilot**: `.github/agents/` has 19 agents (`planner`, `implementer`, `research-planner`…), and `.claude/agents/` only 2 (`assumption-check`, `what-if`). The `research-gather` skill used in this session comes from the Gradebook repo (an additional working directory). Option: lightweight equivalents in `.claude/agents/` or a conscious decision that Claude Code uses the `.github/` instructions via `CLAUDE.md` | less dependence on another repo | whether research/plan can be run without Gradebook with the same quality | if rarely used → don't do it |
| B8 | **Memory consolidation**: `agent-decisions.md` is growing. Periodically merge episodic entries into rules and remove outdated ones (memory about files/functions should be verified before use). You already have `ac-knowledge-review.prompt.md` — it probably covers this; I did not check | long-term memory without upkeep drifts | number of entries and the share still true | if the prompt already does this → nothing |

### What I would NOT add to the setup
- A custom reducer runtime (Aegis) or a multi-agent framework: you already have HITL, limits and a gate; the sources give no independent evidence of an advantage.
- Automatic prompt optimizers (GEPA/DSPy) before the set of golden tasks exists (B2).
- Debate as a permanent step of every change: the cost grows with the number of debaters and rounds, and the sources have no data on effectiveness.

## C. What next (proposed order)
1. A2 and B1 (both cheap and measurable), then A1/A3 as an offline test.
2. B2 after B1 (a baseline measurement is needed).
3. B5 as an analysis of existing data, without building anything.
4. The chatbot (A6) only with real traffic.
