# Research 2026-10-14: agent patterns (Brad Murry + AutoGen + others) and multi-agent debate

Workflow: `research-gather` (Phase 1 brief → Phase 2 plan + gatherers → Phase 3 merge/verification), then a debate (Debate/Consensus pattern). Type: external, actor-agnostic.

| Folder / file | Contents |
|---|---|
| `planning/` | `research-brief.md`, `research-plan.md`, `sources.md` |
| `analysis/findings/` | findings with quotes; `*-raw-*` = from the full text (authoritative), `bm-agents-fsm`, `bm-data-infra-ops`, `autogen-patterns` = first round via WebFetch (superseded, kept as an audit trail), `medium-12-patterns` and `mastra-book` = gaps |
| `analysis/00-summary.md` | summary and catalog of 13 patterns + AutoGen |
| `analysis/01-cross-source-verification.md` | contradictions, correlations, claim verdicts, quote verification (94.8% verbatim matches) |
| `analysis/02-rejected-information.md` | rejected information |
| `debate/positions/`, `debate/critiques/` | round 1: 3 positions and 3 critiques (pro / con / pragmatic) |
| `debate/judge-round1.md`, `debate/decision.md` | judge's verdict (`consensus`) and the agreed decision with the dissenting opinion preserved |
| `analysis/sources-index.md` | provenance index: each raw file ↔ URL/description ↔ whether it was in the original list ↔ how it was obtained (generated from the headers) |
| [`../../reports/2026-10-14-agent-patterns-workflow-improvements.md`](../../reports/2026-10-14-agent-patterns-workflow-improvements.md) | **final report**: where the patterns will improve our workflow (with evidence from this session and references) |
| [`../../reports/2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md`](../../reports/2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md) | 13 patterns vs our setup (what we have, where they are worth it, where not), trade-offs and shared guardrails G1–G11; user ideas and remarks |
| [`../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md`](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) | report review (assumption-check + 2 simulations) and direction-check points H1–H9 with a GO/ADJUST/PIVOT rule |
| `application-ideas.md` | application ideas: the ECommerceApp application (A1–A6) and the Claude Code/Copilot setup (B1–B8), with metrics and "stop" criteria; hypotheses to test |
| `../2026-10-14-llm-self-research/README.md` | additional self research (LLM-as-judge, reflection, prompt optimization, SLM, caching, memory, agent loop, chatbot, model learning) |

**Limitations:** Medium (HTTP 403) and Mastra (content behind an email gate) = gaps; 8 articles of the series and the 13 patterns are an extension of scope beyond the original 18 URLs; the debate consensus is not independent confirmation (one evidence base, dominated by one author).

**Update 2026-10-15:** pattern selection as a rule derived from features, simulation on git history, and hypothesis checks: [`../2026-10-15-pattern-selection/README.md`](../2026-10-15-pattern-selection/README.md).

---

## For the agent verifying this research against the code

The purpose of this folder: **a solid starting point**, not a finished decision. The agent that receives it is to check against the code what of this is accurate for ECommerceApp.

- **Full bibliography and provenance:** [`SOURCES.md`](SOURCES.md) (with URLs, access dates and scope of reading).
- **Read first:** [`../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md`](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) (hypotheses H1–H9, GO/ADJUST/PIVOT state) and [`debate/decision.md`](debate/decision.md) together with the erratum.
- **What has already been verified against the code (2026-10-14/15):** the numbers in `.github/setup-state.md`; no LLM integration in the shop's code; `Product` has no SKU or URL (it has `Name`, `Cost`); `IOrderAccessService` works by token without a `ClaimsPrincipal`; 9 of 10 BLOCKS MERGE rules have no violations in a regex scan.
- **To check against the code (open):**
  1. Whether the 8 service-registration lines in `ECommerceApp.Web/Startup.cs` and `ECommerceApp.API/Startup.cs` are a tolerated exception or a violation of the rule in `dotnet.instructions.md` (lines 167–169).
  2. Whether the direct `fetch(...)` in `cartNotification.js` and `checkout-placeorder.js` is an exception to the "use `ajaxRequest`" rule.
  3. Whether a read-only chatbot can call `IOrderAccessService` + the existing order queries from a backend proxy (end-to-end).
  4. Which rules from `anti-patterns-critical.context.md` are already enforced (analyzers, tests, `.editorconfig`); only `BoundedContextDependencyTests` was found.
- **What not to take on faith:** claims about the "Aegis" runtime and "baseline vs optimum" metrics (one author, no trials or repetitions); the numbers of thresholds and budgets (these are proposals); the debate consensus (one model family, one evidence base).
- **Known corrections:** errata E1 and E2 in `analysis/01-cross-source-verification.md` §6; the erratum in `debate/decision.md` (SKU/URL).
