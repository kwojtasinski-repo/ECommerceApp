# Agent pattern selection: can it be computed, and checking the unsupported / unchecked hypotheses

- **Date:** 2026-10-15 (sources fetched on that day; publication dates next to each source)
- **Mode:** the assistant's own research (WebSearch + WebFetch + local scripts). It did not undergo independent cross-verification.
- **Related:** [report on the review and hypotheses H1–H9](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md) · [pattern fit and guardrails](../../reports/2026-10-14-agent-patterns-fit-tradeoffs-guardrails.md) · [main research](../2026-10-14-agent-patterns-bradmurry/README.md) · [self research](../2026-10-14-llm-self-research/README.md)
- **Files in this folder:** `scripts/select_pattern.py` (simulation), `results.md` (its output), `pattern-decision-card.draft.md` (draft of the pattern card, a research artifact only).

**User's question:** is it possible to dynamically choose the right pattern (debate, planner/executor, etc.) for a problem, and can it be "computed"? In addition: check the hypotheses that came out as unsupported or unchecked in the previous review.

---

## 1. Short answer

**Yes, partially.** It can be **computed with an explicit rule** from a few task features; the rule can be tested for consistency and robustness. **Its accuracy cannot be demonstrated today**, because we have no labels saying "which pattern was the right one". The literature knows two levels: (a) **qualitative** decision models (descriptions of "when what"), (b) **learned predictors** on benchmark data. Our rule is an intermediate level: a transparent rule over features, to be calibrated in shadow mode. Two things cannot be computed from git: **whether the subtasks are truly independent** and **whether the conclusion is contested**. An agent or a human has to estimate that.

---

## 2. What the literature says (as of 2026-10-15)

Confidence level: **High** = primary source fetched, **Medium** = abstract or a single source, **Low** = search-engine snippet.

| Source (date) | What it says about selection | Confidence | Caveats |
|---|---|---|---|
| **Anthropic, *How we built our multi-agent research system*** ([link](https://www.anthropic.com/engineering/multi-agent-research-system); publication date not given in the excerpt) | **The only explicit effort-scaling rule found:** simple fact-finding: 1 agent, 3–10 tool calls; comparisons: 2–4 subagents with 10–15 calls each; complex research: > 10 subagents with clearly divided responsibilities. Cost: agents ≈ 4× the tokens of a chat, multi-agent ≈ 15×. **Poor fit:** tasks requiring shared context across all agents, with many dependencies, and coding ("fewer truly parallelizable tasks than research"). Observed errors: 50 subagents for simple queries, duplicated work given a vague assignment | Medium–high | vendor source; the rule is **in the prompts**, not computed |
| **Kim et al., *Towards a Science of Scaling Agent Systems*** ([arXiv 2512.08296](https://arxiv.org/abs/2512.08296), v1 2025-12-09, v3 2026-04-08; [Google Research post](https://research.google/blog/towards-a-science-of-scaling-agent-systems-when-and-why-agent-systems-work/)) | A predictive model picks the best architecture for **87%** of held-out configurations. Coordination helps with divisible tasks (up to about +80%), **hurts sequential ones** (up to −70%, per the post: "39–70%"), tasks with many tools carry overhead. Architectures without central verification **amplify errors** (independent: 17.2×; centralized: 4.4×). Predictor inputs: number of tools, task divisibility, sequential dependencies | Medium | **Version discrepancy:** abstract v3: 260 configurations, 6 benchmarks, R² (CV) 0.373; post: 180 configurations, 4 benchmarks, R² 0.513. I did not determine which version is correct. The fit is modest, the benchmarks are not our domain |
| **Zhang et al., *Stop Overvaluing Multi-Agent Debate…*** ([arXiv 2502.08788](https://arxiv.org/abs/2502.08788), v1 2025-02-12, v3 2025-06-21; earlier title: *If Multi-Agent Debate is the Answer, What is the Question?*) | 5 debate methods, 9 benchmarks, 4 models: debate **often does not outperform** simple baselines (Chain-of-Thought, Self-Consistency), despite higher compute cost. **Model heterogeneity** as a "universal antidote" | Medium (abstract) | reasoning and QA benchmarks, not evaluation of source synthesis |
| **Huang et al.** ([arXiv 2310.01798](https://arxiv.org/abs/2310.01798), 2023-10-03) | per search-engine snippets: on GSM8K debate ≈ self-consistency with 3 answers (83.2 vs 82.5), and with 9 answers it does worse (83.0 vs 88.2) | **Low** | numbers from search-engine snippets, not from the paper itself |
| **Yue et al., *MasRouter*** ([ACL 2025](https://aclanthology.org/2025.acl-long.757), [arXiv 2502.11133](https://www.arxiv.org/pdf/2502.11133)) | a learned controller sequentially chooses the collaboration mode, roles and model; authors' claim: up to 52.07% lower overhead on HumanEval | Low–medium | authors' claims, not independently verified; requires training data |
| **Liu et al., pattern catalogue** ([arXiv 2405.10467](https://arxiv.org/abs/2405.10467), v4 2024-11-06, §5.1) | **The decision model is qualitative:** e.g. incremental querying when we want greater reasoning certainty; one-shot when the call budget is limited; single-path vs multi-path plan; self/cross/human reflection; multi-agent reflection via voting, roles or debate. **Does not discuss token cost or convergence** for debate and voting | Medium | from the fetched HTML of v4 |

**Conclusion from the literature:** for debate, the evidence is **not on its side as a default choice** (Zhang), and the only really tested **predictor** of selection (Kim) uses features that can be partially assessed: divisibility, sequential dependencies, number of tools. Our own result from 2026-10-14 (5 of 7 described synthesis claims were wrongly attributed or exaggerated) concerns **critique of a synthesis with source checking**, not the tasks tested by these papers; I do not transfer it automatically.

---

## 3. Three levels of "computing"

| Level | Description | Requires | Today |
|---|---|---|---|
| (a) Qualitative | a decision tree of "when what" questions (Liu §5.1, Anthropic) | expert knowledge | exists in the literature and in our reports |
| (b) Rule over features | an explicit score/priority from a few estimated features, with thresholds | feature estimates | **done** in `scripts/select_pattern.py`, tested on git |
| (c) Learned predictor | a model trained on labels (Kim, MasRouter) | data: task → best pattern and outcome | **no data** |

---

## 4. Proposed selection rule (level b)

Task features (to be estimated by an agent or a human; in the simulation approximated from git): **size** (S/M/L), **subtask independence**, **path uncertainty** (steps not known in advance), **availability of a deterministic check**, **risk / reversibility**, **whether the conclusion is contested**, **density of tools and dependencies**.

Decision order (the first matching branch wins, patterns overlap):

| # | Condition | Pattern | Autonomy level |
|---|---|---|---|
| 1 | small size and low risk | **no pattern** | – |
| 2 | everything else | **sequence with gates** (default) | A |
| 3 | steps not known in advance, re-planning may be needed | **Planner → Executor**; ask after 2 re-plans | B |
| 4 | large task, ≥ 3 independent areas, little shared state | **read-only fan-out + decomposition check** (not for edits on shared files: Kim, Anthropic) | A |
| 5 | high risk | deterministic checks first, then **independent validation** (another session/model); if there is no check, add it first | B |
| 6 | conclusion contested and affects a costly decision | **one critic with source checking** | A |
| 7 | ≥ 2 real alternatives, no deterministic check, costly error | **debate** (3 perspectives + judge, enum verdict, ≤ 2 rounds, **different models if available**) | C: ask first |
| 8 | binary high-stakes decision | **quorum of 3 independent votes** | C |

**Sub-rule "debate or not":** by default one critic (row 6). Debate only when **all** conditions from row 7 are met; treat sequential and tool-dense tasks as unfavorable for coordination (Kim).
**Sub-rule "planner/executor or not":** when the specification is clear and the steps known, we skip plan-execute and do a sequence; when the steps depend on discoveries along the way (Anthropic: orchestrator-workers for unpredictable subtasks), we choose row 3.

**What this rule does not compute:** subtask independence (needs an agent's or a human's assessment) and contested conclusions. That is exactly why I treat it as **advisory**, not decisive.

---

## 5. Simulation on git history (200 commits, 2026-05-27 – 2026-10-14)

Full results: [`results.md`](results.md). Warning version: a commit is not a task, the features are git proxies, so **I test consistency and robustness, not accuracy**.

| Result | Value |
|---|---|
| No pattern (small change, low risk) | 34% of commits |
| Edge test: small tasks (S) with a recommended pattern | **0 of 21** |
| Mean agreement of the recommendation under random threshold changes (300 combinations) | **90%**; 70% of commits stable (≥ 90%), 6% unstable (< 60%) |
| Autonomy level change under threshold changes | on average 5% of combinations |

**Calibration showed something important.** In the base variant independent validation occurs for **42%** of commits, because "≥ 3 projects" counted `docs/` and test projects as separate projects (81 commits). After **one change** (count only source projects, excluding docs/tests/.github) it occurs for **20%**:

| Autonomy level | Base | Variant 2 |
|---|---|---|
| no pattern | 34% | 34% |
| A | 23% | 44% |
| B (announce and act) | 38% | 20% |
| C (ask) | 5% | 4% |

**What follows from this (hypotheses):**
1. The rule is **stable**, but **stable ≠ accurate**: the excessive triggering of validation came from one imprecise feature, not from the thresholds. The quality of selection depends mainly on the **definition of the features**, not on the numbers in the thresholds.
2. With variant 2, questions to the user (level C) would be about **4%** of tasks; announcements (B) about **20%**. This can be checked on real traffic in shadow mode.
3. Real accuracy can be assessed only on labels: the **sheet of 20 commits** to label is in `results.md` §3 (the "Agree?" column is empty).

---

## 6. Checking the unsupported / unchecked hypotheses (H1–H9)

Original definitions and PASS conditions: [review report §6](../../reports/2026-10-14-agent-patterns-review-and-direction-checks.md). Below is the **status updated after today's checks**.

| ID | Was | What I checked | Now |
|---|---|---|---|
| **H1** selection from context makes sense, because tasks are M/L | partially | simulation: 66% of commits get at least a sequence with gates, and 34% no pattern; M/L ≈ 90% (S1) | **partially supported**; human labels are missing (sheet ready) |
| **H2** deterministic rule checks will catch something real | unsupported | extended scan (11 checks); violations in 2 groups: **8 service-registration lines in `Startup.cs`** (the rule in `dotnet.instructions.md:169` has no exception for Web/API, and Web has `WebExtensions.cs`) and **direct `fetch(...)` in 2 JS files** (`cartNotification.js`, `checkout-placeorder.js`; rule: use the `ajaxRequest` helper). `User.Claims` only in `BaseController` (allowed) | **partially supported, small scale** (about 10 lines/2 files); unknown whether these are tolerated exceptions |
| **H3** evidence gates in research | supported | unchanged | **supported** |
| **H4** debate over a single critic | unchecked | literature: Zhang et al. (debate often does not outperform simple baselines; model heterogeneity helps) | **unsupported as a default choice**; stays at level C, heterogeneity requirement; our result concerns critique of a synthesis, not benchmarks |
| **H5** accurate selection by an agent | unchecked | the rule computed and tested for robustness; I did not test an LLM's decisions | **still unchecked**; shadow-mode sheet prepared (rule, not agent) |
| **H6** the pattern card does not eat tokens | unchecked | **I measured the card draft**: 1,821 characters, 321 words, **≈ 455 tokens** (range 387–524 per the heuristic from `context-cost-analysis.md`, ±15%) = about **6.4%** of the constant block of 7,110 tokens, if it were always loaded | **measured on the draft**; justifies on-demand loading (zero for S tasks), not constant loading |
| **H7** a read-only chatbot is feasible | partially | in the Application layer there is **token-based access without a ClaimsPrincipal**: `IOrderAccessService.GetScopeAsync(token)` and `HasAccessAsync(orderId, token)` | **better supported** (the backend can check access without a cookie); end-to-end unchecked |
| **H8** Copilot and Claude Code consistency | unchecked | cannot be done without running both tools | **unchecked** |
| **H9** no quality and cost regression | unchecked | requires a set of reference tasks | **unchecked** |

**Still unchecked (requires your involvement or a real run):** H5 in the LLM version, H8, H9, labeling of the H1/H5 sheet.

---

## 7. What changes in the reports after this check
1. **Debate:** stays at level C (on request), with a requirement of different models; there is no evidence that it is better than one critic with source checking.
2. **Fan-out:** read-only and only for truly independent questions; avoid for edits on shared files and for sequential tasks (evidence: Kim, Anthropic).
3. **Effort-scaling rule** (Anthropic: 1 agent / 2–4 subagents / > 10) is the **only explicit numeric rule found**; we treat it as a reference point, not as ours.
4. **Cost anchor:** multi-agent ≈ 15× the tokens of a chat (Anthropic); our debate ≈ 0.52 million subagent tokens.
5. **H2 partially regains meaning:** 2 groups of real deviations from the rules were detected; small scale.
6. **H6 measured on the draft:** ≈ 455 tokens.

## 8. Limitations
- All scientific sources read only through the abstract or a tool's summary; numbers from the search engine marked as **Low**.
- Kim et al.: divergent versions (number of configurations, R², gain/loss values).
- The git simulation tests the rule's consistency, not accuracy; a commit ≠ a task.
- The rule-scan regexes are approximations; I did not separate legacy code from new.
- The card measurement is a characters/4 heuristic on the draft, not a real tokenizer nor real loading in a tool.
- I did not do an LLM test of selection (H5), an A/B test of debate (H4) or a test in Copilot (H8).
