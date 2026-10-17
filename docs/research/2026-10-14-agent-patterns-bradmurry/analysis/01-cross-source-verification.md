# Cross-Source Verification (Phase 3, step 2)

Date: 2026-10-14. Confidence convention: methodology §8 (High = ≥2 independent sources/explicit evidence; Medium = one source; Low = speculation/contradictory). **Note:** in the `*-raw-findings.md` files "High" means "the claim is explicitly in the text", **not** "independently verified". In this document I use the strict scale.

## 1. Contradictions and inconsistencies (I do not decide in favor of one side)

| # | Source A says | Source B says | Nature |
|---|---|---|---|
| C1 | `08` (series intro): each loop is bounded "by a verdict enum, policy.maxIterations, and the reducer's no-progress guard" | `13`, `14`, `16` (the author corrects himself): the no-progress guard "does not span turns", the safeguards are the enum + `maxIterations`, "two rather than three" | **Author's self-correction** — earlier claims weakened. The series intro was not corrected. |
| C2 | `09` (pipeline): `implemented` is "NOT a bool" (a bool reset to `false` stays "present") | `09`, final table: "implemented = true" | Internal inconsistency, not explained by the author |
| C3 | `26` (AiP Part 1): the incident-triage YAML has `derive-key`, `notifyKey`, `attempts`, re-entrant `notify` | `27`: the same FSM name, `notify` requires `notifyRef` from `opsTool`, no `derive-key` | Two different versions of "the same" workflow |
| C4 | `23` (data quality): the signals table has 3 facts + 1 opinion | text: "Two facts, one opinion" | Editorial error (marked `[sic]`) |
| C5 | `23`: tokens after Round 2 = 38,000 | optimum = 41,000 (higher than after Round 2, although called the "optimum") | The metric grows at the "optimum" — unexplained |
| C6 | `25` (entity resolution): the final FSM has no `policy` block | the series text everywhere stresses `maxIterations`/policy as a safeguard | Example–principle inconsistency; the split of tuning into rounds in the summary also does not match the description of the rounds |
| C7 | AutoGen debate (`39`): prose "agent A is connected to agents B and C" | the figure and `TypeSubscription`: A neighbors B and D | Prose vs code (both quoted) |
| C8 | AutoGen debate: prose "SolverRequest" starts the debate | the code publishes `Question` | Prose vs code |
| C9 | AutoGen reflection (`40`): prose lists "max iterations" as a stop condition | the code has no iteration limit | Prose vs code — a possible infinite loop |
| C10 | AutoGen group chat (`36`): prose "APPORVED" from the editor | the code only checks the user's "approve" message | Prose vs code; the page itself: "not meant to be used in real applications" |
| C11 | AutoGen mixture-of-agents (`38`): 3 layers of workers, the orchestrator synthesizes, the result is "published" | `range(num_layers-1)` → 2 layers; the synthesis is done by workers; the result is returned directly | Prose vs code |
| C12 | AutoGen handoffs (`37`): "Refund Agent" | code: `IssuesAndRepairsAgent` | Name in prose ≠ code |
| C13 | `32` (config-driven-transforms): the heading "schleppy transforms" | import `plot.transform.processor` | Inconsistent tool name |
| C14 | user: "14 posts" | blog index: 13 posts + the index page = 14 URLs | Not a contradiction of the sources — a clarification of the count |

## 2. Correlation between sources (what is confirmed independently and what is not)

| Claim | Sources | Independent? | Assessment |
|---|---|---|---|
| "An LLM judge/critic is a weak signal; an external, deterministic check is needed" | Brad (`07`, `22`–`25`: "the weakest signal… drifts toward 'looks thorough'") ⟷ Zheng et al. 2023 (position/verbosity/self-enhancement biases) ⟷ Huang et al. 2023 ("struggle to self-correct… without external feedback") | three independent groups, but **different claims** (see erratum E1 at the end of the file) | **Medium** (corrected after the debate; originally "High directionally") |
| "A loop must have an explicit limit/stop outside the model" | Brad (maxIterations, cap 128) ⟷ Anthropic ("Stopping conditions, such as a maximum iteration count") ⟷ blogs on ReAct | partially | **High** (directionally) |
| "Start with the simplest; autonomy = cost and compounding errors" | Anthropic ⟷ Brad ("Not every workflow needs a full FSM… a for-loop could do for free", `29`) | yes | **High** |
| "Model diversity, not a different loop, helps with shared blind spots (debate/quorum)" | Brad (`14`, `20`) ⟷ literature on judge biases (self-enhancement) | partially; the literature does not test Brad's debate | **Medium** |
| "The reactive reducer pattern gives determinism, replay, bounded cost" | Brad only (several posts) | **no** — one author, several posts are not independent confirmation | **Medium** (the author's claim) |
| Baseline→optimum metrics (4 examples) | Brad only | no; no trials/repetitions | **Low–Medium** |
| "Multi-agent debate improves the result" | AutoGen: only a description + one example with full agreement; Brad: no numbers | no data in the sources | **Low** (no evidence of effectiveness in the collected material) |
| External evidence of multi-agent debate effectiveness (e.g. the paper on sparse topology mentioned on the AutoGen page) | the AutoGen page links to arXiv 2406.11776 — not fetched | — | **gap** |

## 3. Gaps (Missing Information)
- Medium (403), Mastra (chapter content).
- No independent benchmark whatsoever for the "Aegis" runtime; no public repo in the material.
- No data on the effectiveness of debate/quorum in the sources (Brad: descriptions of the mechanics, the author himself: "can't tell you the consensus is right").
- AiP parts 5–7 and acts II–III; the 13 pattern subpages were available only thanks to pasting by the user.
- Stopped at the "state as of 2026-10-14"; the publication dates of most of Brad's posts are not known (some pages have "Jul 2026", "Sep 2026" in the excerpt).

## 4. Declarative Conclusions (consolidated)
Not applicable — external sources (publications), not transcripts with speakers. Treated as the *author's claims*: marked in the findings as "single-source / self-reported". The most risky for later conclusions (analytical influence filter, methodology §9):

| # | Claim | Author | Justification in the source | Corroboration | Verdict |
|---|---|---|---|---|---|
| D1 | "the glue code is gone" | Brad (`03`) | declaration only | none | **Unsupported** |
| D2 | "This series is all true. Not a thought experiment" (`07`) | Brad | archive of 5 versions of disease-etl | archive files unavailable | **Partially supported** |
| D3 | The 2020 result "billions of requests… didn't even break a sweat" (`30`) | Brad | no numbers besides this | none | **Unsupported** (self-description) |
| D4 | "90% fast track / 80% of use case" (`43`) | Brad | none | none | **Unsupported** |
| D5 | "An LLM is a fantastic contributor. It is a terrible state machine." (`03`) | Brad | narrative justification | consistent with Huang et al. at the level of direction (not literally) | **Partially supported** |
| D6 | SLM "sufficiently powerful, inherently more suitable, necessarily more economical" (arXiv 2506.02153) | Belcak et al. | position paper | no independent ones in the collected materials | **Partially supported** (the authors themselves call it a "value statement") |

## 5. Quote verification (automatic, 2026-10-14)
A script compared the quotes in quotation marks (≥25 characters) from the `*-raw-*findings.md` files with the source text (addresses in [`../SOURCES.md`](../SOURCES.md)) (after normalizing whitespace, apostrophes, backticks). Result:

| Findings file | Quotes | Matching | Non-matching |
|---|---|---|---|
| autogen-raw-9pages | 173 | 160 | 13 |
| bm-agentic-core-raw | 177 | 173 | 4 |
| bm-infra-raw | 294 | 285 | 9 |
| bm-patterns-1-7-raw | 232 | 212 | 20 |
| bm-patterns-8-13-raw | 279 | 273 | 6 |
| bm-series-parts-raw | 293 | 270 | 23 |
| **Total** | **1,448** | **1,373 (94.8%)** | **75 (5.2%)** |

Manual review of a sample of the non-matching ones: most are **detector artifacts** (code fragments in backticks, pairs of quotation marks enclosing sentences instead of quotes, inner double quotation marks changed to single ones when quoting, e.g. `'how many have answered'`) — not fabricated quotes. **I did not review all 75 individually**; treat 94.8% as a lower bound of verbatim agreement, and the rest as "to be checked manually before use in a publication".

## 6. Erratum (after the debate, 2026-10-14)
- **E1 (the row "An LLM judge/critic is a weak signal…" in §2):** originally I grouped three sources as confirming a single claim and gave "High". The debate judge (`../debate/judge-round1.md`) deemed this an overstatement. Correctly: Zheng et al. 2023 show **judge biases** (position, verbosity, self-enhancement) and >80% agreement with humans, i.e. the judge needs calibration, rather than "is weak"; Huang et al. 2023 concern **self-correction without external feedback**; Brad claims that the LLM critic is the "weakest signal". These are three similar but different claims, so the agreement is only directional (**Medium**). The error was in my synthesis, not in the sources.
- **E2:** "Low" in §2 concerns solely the effectiveness of **debate**. It does not cover patterns 10 (auction) and 13 (dynamic team), for which there is no assessment of effectiveness in the material (no data, not evidence against).
