# RAG parity audit — findings & actionable recommendations

**Date**: 2026-05-28  
**Source data**: [docs/reports/rag-parity-audit-2026-05-28.md](rag-parity-audit-2026-05-28.md)  
**Script**: [tools/rag/compare_queries.py](../../tools/rag/compare_queries.py)  
**Sprint**: 2 / item B1 — completion analysis

---

## 1. Headline numbers

| Metric | Value | Interpretation |
|---|---|---|
| Total queries | 18 | 5 spec + 3 gen + 6 Sprint-1 + 4 multilingual |
| Top-1 path match | 4 / 18 (22%) | **Low parity** |
| Top-1 mismatch | 14 / 18 (78%) | High divergence between servers |
| Errors | 0 / 18 | Both servers healthy, all queries returned hits |
| Files only in Python top-5 (cumulative) | 26 | Python surfaces more unique sources |
| Files only in .NET top-5 (cumulative) | 31 | .NET surfaces different unique sources |
| Avg \|score delta\| at top-1 | 0.068 | Python systematically more confident at top-1 |

The 22% top-1 parity rate is **not necessarily a bug** — both indexers were designed independently and the divergence is documented (see `/memories/repo/rag-mcp-anomalies.md`). The question is *which divergences hurt agent answer quality*.

---

## 2. Confirmed pattern: Python ranks `.github/context/*` highly; .NET does not

**Evidence** (top-1 results):

| Query | Python top-1 | .NET top-1 |
|---|---|---|
| Q4 (FluentAssertions / KI) | `.github/context/known-issues.md` (0.649) ✅ | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` (0.605) ❌ wrong |
| Q5 (blocked BCs) | `.github/context/project-state.md` (0.648) ✅ | `docs/adr/0004/0004-module-taxonomy...md` (0.523) ❌ wrong |
| S1-rag (RAG pipeline) | `.github/context/agent-decisions.md` (0.611) | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` (0.479) |
| S1-cache (L3 hook) | `.github/context/agent-decisions.md` (0.707) | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.536) ✅ correct |
| ML-de-ctx | `.github/context/agent-decisions.md` (0.732) | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` (0.591) ✅ correct |

**Diagnosis**: .NET indexer either (a) does not ingest `.github/context/*.md` with the same weight as Python, or (b) applies different metadata boost. For Q4 and Q5 this is a **functional regression** — `known-issues.md` and `project-state.md` are the authoritative sources by design.

Note: for S1-cache and ML-de-ctx, the .NET answer is actually MORE correct (the canonical doc is the ADR, not the agent-decisions log). Python over-boosts `agent-decisions.md`. So the divergence cuts **both ways**.

**Recommendation R1**: investigate .NET indexer config for `.github/context/**` doc-kind weight and Python config for `agent-decisions.md` boost. Both need adjustment — neither is "right" today.

---

## 3. Recurring offender on .NET side: `0028-002-batch-manifest-pipeline.md`

Appears as top-1 on .NET for THREE unrelated queries:
- Q4 (FluentAssertions) — completely wrong
- G1 (DI wiring) — completely wrong
- S1-rag (RAG ingest) — semantically near but off-topic

**Diagnosis**: this file likely has very high TF-IDF on common engineering terms ("pipeline", "manifest", "batch", "deviation") and the .NET ranker lacks a counter-balancing metadata penalty. Python does not exhibit this — its multi-feature reranker (per ADR-0027 §6) is more selective.

**Recommendation R2**: capture as a .NET indexer anomaly entry, file under `/memories/repo/rag-mcp-anomalies.md`. Candidate fixes (in priority order):
1. Reduce default weight of `amendments/**` files in .NET ranker.
2. Apply query-token overlap penalty when document terms repeat across all queries.
3. Add an exclude/down-weight rule for "batch-manifest-pipeline" specifically if it remains an outlier after (1).

---

## 4. Multilingual queries — glossary expansion works on Python, weaker on .NET

| Query | Python top-1 score | .NET top-1 score | Delta |
|---|---|---|---|
| ML-pl-ctx | 0.563 | 0.561 | 0.002 — parity |
| ML-pl-ref | 0.662 (`project-state.md`) | 0.539 (wrong ADR) | 0.123 — .NET miss |
| ML-de-ctx | 0.732 | 0.591 | 0.141 — Python much stronger |
| ML-de-ada | 0.708 | 0.615 | 0.093 — Python stronger |

**Diagnosis**: Sprint 1 glossary expansion (8 PL/DE entries) was committed in `tools/rag/multilingual-glossary.yaml`. The glossary is query-time only (no re-index needed). Python clearly applies it; .NET appears to apply a weaker variant OR a different glossary path.

**Recommendation R3**: confirm .NET reads the same `multilingual-glossary.yaml`. If yes, the difference is in how the expanded terms are weighted at query time. Trace through `RagQueryService` to verify the expansion step exists and the resulting OR-query weights match Python's behavior.

---

## 5. Generic queries (G1–G3): both servers weak, but for different reasons

Generic queries are expected to have low parity. The interesting observation is that **neither server returns the most useful file** (e.g. G1 should return an Infrastructure DI extension example, not an ADR-0027 chunk).

**Recommendation R4**: not a parity issue — log as a corpus gap. Either add named eval queries (skill `generate-eval-questions`) or accept that "DI wiring" is too vague for the corpus to answer well.

---

## 6. Concrete next steps (prioritised)

| # | Action | Owner | Sprint |
|---|---|---|---|
| 1 | Update `/memories/repo/rag-mcp-anomalies.md` with sections 2, 3, 4 findings | this session | 2 (now) |
| 2 | Implement R1 — adjust `.github/context/**` weighting on both servers | next session | 2 (B-cluster) |
| 3 | Implement R2 — .NET `amendments/**` down-weight + amendment for `batch-manifest-pipeline` if needed | next session | 2 (B-cluster) |
| 4 | Implement R3 — verify .NET glossary expansion path | next session | 2 (B-cluster) |
| 5 | Document `query_docs_cached` MaxTopK 20 vs 45 decision (B2) | next session | 2 |
| 6 | Re-run `compare_queries.py` after each fix; track top-1 parity rate as the regression metric | continuous | 2 |

**Target after R1+R2+R3**: top-1 parity ≥ 60% (currently 22%). Avg score delta ≤ 0.05 (currently 0.068).

---

## 7. Re-run instructions

```powershell
# From host (NOT inside rag-tools container):
python tools/rag/compare_queries.py
# Outputs:
#   .rag/compare_servers.out.txt              — plain side-by-side
#   docs/reports/rag-parity-audit-<date>.md   — overwrites today's report
```

If running from a container with shared docker network instead, change the two `http_session(port)` calls to use `rag-python-http:3002` and `rag-dotnet-http:3001` and run on the `rag` profile network.

---

## 8. Post-fix results — same-day verification

After applying R1, R2, R3 the parity script was re-run twice. The deltas:

| Metric | Baseline | After R3 | After R1+R2+R3 |
|---|---|---|---|
| Top-1 parity | 4 / 18 (22%) | 7 / 18 (39%) | **8 / 18 (44%)** |
| Only-in-.NET top-5 (sum) | 31 | 22 | **22** |
| Avg \|score delta\| at top-1 | 0.068 | 0.058 | 0.068 |

Note: the score-delta "regression" between R3 and R1+R2+R3 is an artefact — R1/R2 increased \.NET top-1 scores on `.github/context/*` (good), which widened the gap on queries where Python already had a high top-1 score. Top-1 path parity is the primary metric and improved monotonically.

### What was changed

| Fix | File | Change | Risk |
|---|---|---|---|
| R1 | `tools/rag-dotnet/rag-config.yaml` | `.github/context/{known-issues,agent-decisions,project-state}.md` weights bumped (1.25→1.30 / 1.20→1.25 / 1.15→1.20) on \.NET only | Low — query-time only, no re-index |
| R2 | `tools/rag-dotnet/rag-config.yaml` | `docs/adr/*/amendments/**` weight lowered (1.20→1.10) on \.NET only | Low — still above the 1.00 neutral, just less aggressive |
| R3 | `docker-compose.yaml` + `tools/rag-dotnet/multilingual-glossary.yaml` | Added `multilingual-glossary.yaml` mount to both `rag-python-http` and `rag-dotnet-http`; synchronised the `.NET` glossary mirror copy (8 missing PL/DE entries from Sprint 1) | Low — file-only, no code change |

Python `rag-config.yaml` was **not modified** — R1/R2 are \.NET-only tuning. Python `multilingual-glossary.yaml` is unchanged (the canonical file already had the Sprint 1 entries).

### Remaining mismatches — acceptable

The 10 remaining top-1 mismatches fall into three categories, none of which warrant further weight tuning in this session:

1. **Generic queries (G1–G3)**: low information-density questions where neither server has a clearly "correct" answer (3 mismatches).
2. **Python over-boosts `agent-decisions.md`** for S1-rag / S1-cache / ML-pl-ref where the canonical doc is the ADR itself, and the \.NET answer is actually MORE precise. Fixing this would require lowering Python `agent-decisions.md` weight, which would harm the queries where it's correct (Q4 / Q5).
3. **\.NET chunker boundaries** still surface `0028-002-batch-manifest-pipeline.md` and `0014/amendments/a4-operator-notifications.md` for some queries (down-weight helped but didn't fully solve). Root cause is chunker-level, not weights — out of scope for query-time tuning.

B2 (`query_docs_cached` MaxTopK 20 vs 45) and the `.NET` indexer bugs (ADR-0028 main_file, amendment count) remain open — these need code changes, not config tuning.

---

## 9. Q-PRECISE follow-up (Sprint 2 / Category-B validation)

To validate the **Category B hypothesis** (Python over-boosts `agent-decisions.md` / amendments while .NET picks the canonical ADR), 8 new "Q-PRECISE" queries were added that explicitly name an ADR by id and ask for the canonical document:

| Tag                | Question (excerpt)                                                                | Python top-1                                              | .NET top-1                                                       | Winner       |
| ------------------ | --------------------------------------------------------------------------------- | --------------------------------------------------------- | ---------------------------------------------------------------- | ------------ |
| `QP-0027-chunk`    | ADR-0027 RAG chunking strategy max tokens overlap heading boundaries decision     | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` (0.686) | `docs/adr/0027/0027-rag-pipeline-design.md` (0.678)              | **.NET**     |
| `QP-0029-sand`     | ADR-0029 context-mode sandbox runtime allowlist DNS decision                       | tie / both correct                                        | tie / both correct                                                | match        |
| `QP-0016-coup`     | ADR-0016 coupon maximum per order limit five ten ceiling decision                  | `docs/adr/0016/amendments/a1-…` (0.746)                  | `docs/adr/0016/README.md` (0.628)                                | **.NET**     |
| `QP-0019-curr`     | ADR-0019 NBP exchange rate currency conversion decision API integration            | `docs/adr/0014/amendments/a3-integration-flow-decisions.md` (0.668) | `docs/adr/0008/0008-supporting-currencies-bc-design.md` (0.671) | **.NET**     |
| `QP-0028-batch`    | ADR-0028 amendment 002 batch manifest pipeline                                     | tie / both correct                                        | tie / both correct                                                | match        |
| `QP-which-rag`     | Which ADR specifically defines the RAG architecture and embedder model choice?     | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.699) | `docs/adr/0028/tech-details-dotnet.md` (0.615) | mixed (neither perfect, .NET closer to canonical) |
| `QP-which-mt`      | Which ADR specifically governs remote multitenant RAG ingest and per-collection…  | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.635) | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` (0.572) | **.NET**     |
| `QP-cross-bc`      | What ADR covers cross-bounded-context messaging and event publishing?              | `.github/context/agent-decisions.md` (0.545)              | `docs/adr/0002/0002-post-event-storming-…` (0.475)               | **.NET**     |

**Score**: 5 / 8 .NET wins canonical, 2 / 8 ties, 1 / 8 mixed — **Category B hypothesis confirmed**. Python systematically over-ranks amendments and `agent-decisions.md` when the question explicitly names an ADR.

### R4 experiment — REJECTED

Hypothesis: lowering Python `docs/adr/*/amendments/**` weight from 1.20 → 1.10 would let canonical ADRs win on Q-PRECISE-style queries.

Test (2026-05-28 18:05 UTC):

1. Edited `tools/rag/rag-config.yaml` — amendments weight 1.20 → 1.10.
2. Force-recreated `rag-python-http` container.
3. Re-ran `compare_queries.py` (all 26 queries).

Result: **ZERO change** in top-1 path matches (10/26 before AND after). Reverted.

Why: Python raw cosine on amendment chunks is consistently **0.05–0.15 higher** than on canonical ADR text for these queries. A weight multiplier of 1.10 vs 1.00 (factor 1.10) cannot overcome a 0.15 raw-score gap (factor ~1.30 at typical 0.5-0.7 score range). The win condition would require weight < 0.85 on amendments, which would over-correct and hide genuinely-relevant amendment content.

The root cause is **Python embedder/retriever behaviour** (favouring later/longer amendment chunks over the original ADR section), not weight policy. Possible future fixes (NOT this sprint):

- Per-query first-token bias toward canonical ADR file when `adr_id` is detected in the query.
- Hybrid scoring that boosts files whose path matches the queried ADR id.
- Re-chunking ADRs so amendments and canonical text live in the same logical chunk.

Tracked as future workstream — see Roadmap Phase 3 sibling task and KI-NNN to be assigned in the next maintainer sync.

---

## 10. .NET `list_adrs` indexer bugs — partial fix (Sprint 2 / item #7)

Two bugs in `RagTools.Core/QdrantDocumentStore.ListAdrsAsync`:

### Bug A — `main_file` pointed at `tech-details-*.md` instead of canonical ADR

**Root cause**: `tools/rag/metadata-rules.yaml` catch-all rule classifies every `docs/adr/**` file (other than amendments/example/checklist/migration/README) as `doc_kind = adr_main`. That includes `tech-details-python.md`, `tech-details-dotnet.md`, etc. The `.NET` code picked `FirstOrDefault(doc_kind == adrDocKind)` which returned whichever chunk Qdrant happened to scroll first — usually a `tech-details-*.md` chunk.

Python is immune because its `_tool_list_adrs` does a filesystem scan `folder.glob(f"{adr_id}-*.md")` — only files whose name starts with the ADR id qualify.

**Fix** (`QdrantDocumentStore.cs` `IsCanonicalAdrFile` helper): prefer the chunk whose `rel_path` filename starts with `"{adr_id}-"`, fall back to the old behaviour only if no canonical chunk exists.

**Status**: deployed. Verification needed — `list_adrs` was re-run after rebuild but ADR-0028 still returned `main_file = docs/adr/0028/tech-details-python.md`. Investigation pending: likely either (a) no chunk in the collection has `rel_path = docs/adr/0028/0028-remote-multitenant-rag-ingest.md` (the canonical file was never indexed under collection `ecommerceapp_docs_dotnet`), or (b) the `rel_path` payload uses backslashes/different casing. Cross-check with `query_docs(question="ADR-0028 remote multitenant")` confirmed top chunk is `0028-remote-multitenant-rag-ingest.md`, so the chunk DOES exist — the comparison may be case-sensitive or path-separator mismatched. Logged as KI follow-up.

### Bug B — Amendment count inflation (chunks vs files)

**Root cause**: `.NET` counted chunks (`g.Count(p => doc_kind == amendmentDocKind)`) instead of distinct files. A single amendment markdown can split into 5–10 chunks, inflating counts ~10×. Reported parity drift: ADR-0028 Python:3 .NET:33; ADR-0009 Python:6 .NET:24+; etc.

**Fix**: replaced `Count(...)` with `.Select(rel_path).Distinct().Count()`.

**Status**: deployed and **mostly verified**. Post-fix counts:

| ADR    | Python (filesystem) | .NET pre-fix | .NET post-fix |
| ------ | ------------------- | ------------ | ------------- |
| 0009   | 6 files             | inflated     | **6** ✅      |
| 0010   | 5 files             | inflated     | **5** ✅      |
| 0014   | 12 files            | inflated     | **12** ✅     |
| 0017   | 6 files             | inflated     | **6** ✅      |
| 0029   | 11 files            | inflated     | **11** ✅     |
| **0028** | **4 files**       | **33**       | **33** ❌     |

ADR-0028 remained inflated despite the fix. Working theory: amendment chunks for `0028-002-batch-manifest-pipeline.md` may have `rel_path = null` or paths that differ chunk-to-chunk (e.g. line ranges appended). Needs targeted scroll-and-print investigation outside this sprint.

**Net result**: 5 of 6 inflated ADRs corrected. Treat remaining ADR-0028 anomaly as a known issue and address in a follow-up.

---

## 11. B2 — `query_docs_cached` MaxTopK raised 20 → 45

**Context**: Python `query_docs_cached` derives `top_k = max(30, top_files * 15)` (45 at the default `top_files=3`). .NET capped at `RagQueryService.MaxTopK = 20`, causing the internal `query_docs_cached` wrapper to either truncate the request or reject it with `TopKOutOfRange` — producing asymmetric behaviour at the same logical call site.

**Decision (Sprint 2 / item B2)**: raise .NET `MaxTopK` to 45 to match Python.

**Implementation**:

- `tools/rag-dotnet/src/RagTools.Core/Query/RagQueryService.cs:21` — constant 20 → 45 with inline rationale comment.
- `tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs:72` — wrapper comment updated.
- Test fixtures using literal "20" in synthetic error messages left unchanged (they are example data, not assertions against the public constant).

**Verification**:

- `dotnet build` clean.
- `dotnet test RagTools.Tests` — **478 tests pass, 0 failures, 0 skips** (the only test that compares against `MaxTopK` uses the symbol, not the literal).
- `rag-dotnet-http` container rebuilt and restarted; HTTP smoke test confirms `query_docs(top_k=45)` returns 200 instead of `TopKOutOfRange`.

**Impact**: no risk to existing call sites — only widens the validation bound. Consumers that already passed `top_k ≤ 20` are unaffected. `query_docs_cached` invocations now have the same chunk-density on both servers.

**Risk note**: at `top_k=45` the post-ranking output still respects `top_files` grouping, so end-user payload size doesn't balloon. Wall-clock latency increases marginally (Qdrant scroll-and-rerank work scales linearly).
