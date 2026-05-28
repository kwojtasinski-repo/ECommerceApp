# RAG parity fix — diagnosis report (2026-05-28)

> Companion to [`rag-parity-findings-2026-05-28.md`](./rag-parity-findings-2026-05-28.md).
> Validates whether the R1/R2/R3 fixes applied earlier today actually take effect on the
> production HTTP code path, and answers the user's three follow-up questions:
> (1) HTTP/batch-ingest design — does mounted config matter?
> (2) Can we close the remaining 10 mismatches by adding more precise queries instead of re-indexing?
> (3) If we ever do re-index with a different chunker — what's available given the ONNX embedder?

---

## 1. TL;DR

| Fix | Mechanism | Production effect | Re-index needed? |
|---|---|---|---|
| **R1** — `.github/context/*` weight bump on .NET | Query-time `TopicFilter.ApplyWeights()` multiplier | ✅ Active after container restart | No |
| **R2** — `amendments/**` down-weight on .NET | Same as R1 | ✅ Active after container restart | No |
| **R3** — glossary mount + canonical sync | Query-time `GlossaryExpansionPreprocessor.Expand()` on the QUESTION | ✅ Active after container restart | No |
| **R3-side note** — `tools/rag-dotnet/multilingual-glossary.yaml` (mirror file) | Fallback only — used when running `.NET` directly with `dotnet run` from `tools/rag-dotnet/` outside Docker | ⚠️ Not used by the HTTP container, but the mirror+warning header is still correct for local dev fallback |

All three fixes are correctly deployed. No further action required for the R-block.

The batch-ingest design (production path) does NOT undermine R1/R2/R3 because weights and glossary are query-time concerns — the server-mounted `rag-config.yaml` always governs them, regardless of which ZIP was used to ingest the documents.

---

## 2. .NET HTTP server — config loading model

### 2.1 What gets baked into the image vs mounted at runtime

The .NET HTTP runtime image (`Dockerfile-rag-dotnet*`) contains **only** the ONNX model directory + .NET binaries. It contains **no documents and no configs**.

Runtime mounts (from `docker-compose.yaml`):

| Host path | Container path | Purpose |
|---|---|---|
| `tools/rag-dotnet/rag-config.yaml` | `/rag-config.yaml` | Server config (weights, query settings, companion file names) |
| `tools/rag/metadata-rules.yaml` | `/metadata-rules.yaml` | Doc-kind detection at ingest time |
| `tools/rag/queries.yaml` | `/queries.yaml` | Named eval queries |
| `tools/rag/multilingual-glossary.yaml` | `/multilingual-glossary.yaml` | Query-time PL/DE expansion (canonical, shared with Python) |
| `.` (repo root) | `/workspace:ro` | Workspace discovery / source_roots |

The .NET service loads `rag-config.yaml` ONCE at startup via `RagConfig.Load()` in `Program.cs`. The path is resolved by `RagConfig.ResolveConfigPath()`:

1. `RAG_CONFIG` env var (HTTP service sets it to `/rag-config.yaml`)
2. `RAG_WORKSPACE/tools/rag/rag-config.yaml`
3. `/app/rag-config.yaml`

The glossary path resolves via `config_files.multilingual_glossary` declared inside `rag-config.yaml`, with `RAG_GLOSSARY` env var as override. The container resolves it to `/multilingual-glossary.yaml` → bound to the **canonical** `tools/rag/multilingual-glossary.yaml`.

### 2.2 Weights — query-time only

`tools/rag-dotnet/src/RagTools.Core/Query/RagQueryService.cs#L96` (and `RagReadDocsService.cs#L111`):

```csharp
var weighted = TopicFilter.ApplyWeights(allHits, cfg);
```

This runs AFTER Qdrant returns hits. The multiplier comes from `RankingWeightResolver` which glob-matches the chunk's relative path against `ranking.weights[]`. First match wins.

**Implication for R1/R2**: editing `tools/rag-dotnet/rag-config.yaml` weights changes scores on the very next query after container restart. No re-index, no chunk rewrite.

### 2.3 Glossary — query-time only (on the QUESTION, not the doc)

`tools/rag-dotnet/src/RagTools.Core/Preprocessors/GlossaryExpansionPreprocessor.cs`:

```csharp
if (ctx.Purpose == EmbedPurpose.Ingest) return Task.FromResult(text);
return Task.FromResult(_glossary.Expand(text));
```

Glossary expansion is **skipped during ingest**. It only fires when the embedder is called with `EmbedPurpose.Query` — i.e. when expanding the user's question. Document vectors stay pure.

**Implication for R3**: the previously-stale `tools/rag-dotnet/multilingual-glossary.yaml` mirror file was the wrong concern. The real problem was that the canonical file was never mounted into `rag-dotnet-http`. Today's compose change fixes the actual code path.

---

## 3. Batch-ingest design (the user's HTTP-upload concern)

Endpoint: `POST /ingest/{collection}/batch` — implemented in
`tools/rag-dotnet/src/RagTools.Mcp/Controllers/IngestController.cs`.

Pipeline: `ZipUploadFilter` → `IZipBatchParser.ParseAsync()` → `BatchValidator.Validate()` → `IBatchIngestService.Enqueue()` → background `IngestWorker`.

### 3.1 What the ZIP must contain

Per `BatchValidator.cs`:

- `rag-config.yaml` (required) — drives chunking + companion filenames for THIS batch.
- `metadata-rules.yaml` (required, can be renamed via `config_files`) — drives doc_kind detection.
- `queries.yaml` (required, can be renamed via `config_files`) — eval queries.
- `multilingual-glossary.yaml` (optional) — currently **ignored at ingest** (glossary is query-time only).
- N `.md` files — the documents to chunk + embed + store.

### 3.2 Which config wins — ZIP vs server mount

| Config concern | Source | Used when |
|---|---|---|
| Chunker settings (max/min/overlap tokens, heading splits) | **ZIP's `rag-config.yaml`** | Ingest time only — applied to that batch's chunks |
| Doc-kind / ADR ID detection rules | **ZIP's `metadata-rules.yaml`** | Ingest time only — metadata stored on chunks |
| Ranking weights | **Server's mounted `rag-config.yaml`** | Query time — uniform across all ingested collections |
| Glossary expansion | **Server's mounted `multilingual-glossary.yaml`** | Query time — on user question |
| Embedder model | Server's image (baked) | Both ingest and query — must match exactly |

This separation is intentional: different tenants/clients can upload ZIPs with different chunking strategies, but the operator controls how queries get ranked.

### 3.3 What this means for the R-block

R1/R2/R3 all affect **query-time** behavior. They do NOT require re-ingesting the existing collections. Even if the existing data was ingested with a slightly different `rag-config.yaml` (e.g. a snapshot in the ZIP), the weights and glossary now in effect come from the server's mount and apply to every query.

✅ No re-index triggered by R1/R2/R3.

---

## 4. Can the remaining 10 mismatches be closed by adding more precise queries?

User's hypothesis: "do nie ogarniemy tego poprzez większą ilość queries bardziej szczegółowych albo precyzyjnych?"

### 4.1 Categorise the 10 remaining mismatches

From `rag-parity-findings-2026-05-28.md` §8:

| Category | Count | Examples | Cause |
|---|---|---|---|
| **A — Generic/low-density queries** | 3 | G1 (DI question), G2 (validators), G3 | Both servers struggle to find a single canonical doc because the topic spans many files |
| **B — Python over-boost `agent-decisions.md`** | 4 | S1-rag, S1-cache, ML-pl-ref, ML-de-ada | Python's reranker promotes the decisions log even when canonical ADR exists; .NET picks the ADR (often more correct) |
| **C — .NET chunker boundary noise** | 3 | Queries that still surface `0028-002-batch-manifest-pipeline.md` despite the 1.20→1.10 down-weight | Chunker boundaries cause oversized chunks rich in common engineering terms |

### 4.2 Can more queries help each category?

| Category | Can more queries help? | Why |
|---|---|---|
| **A** | ❌ Limited | "Generic" means the question itself is the problem. Adding more queries doesn't change ranking math. Could be slightly improved by writing more specific eval queries (Q1b/Q2b…), but production users will still type generic questions. |
| **B** | ✅ **YES — partial** | Adding queries like "Which ADR specifically covers caching strategy?" → forces evaluation to reward canonical ADR over decisions log. If new queries fail on Python and pass on .NET, that's evidence to LOWER Python's `agent-decisions.md` weight (counterpart to R1 — a "R4" on Python side). |
| **C** | ❌ Won't help | Chunker boundaries are upstream of query ranking. The chunk content is fixed at ingest time; ranking weights can only scale them, not re-shape them. A truly precise query would still match the same chunk text. Fix requires either re-chunking or chunk exclusion rules (see §5). |

### 4.3 Recommended next step — Category B queries

Add 6-8 new queries to `tools/rag/queries.yaml` that:

1. Reference a specific ADR by number AND ask about its decision (e.g. "What does ADR-0027 decide about chunking?").
2. Reference a specific BC AND a specific cross-cutting concern (e.g. "How does Sales BC handle currency conversion via ADR-0019?").
3. Use mixed PL/DE keywords for the same topic (validates R3 glossary keeps holding).

After adding queries, re-run `python tools/rag/compare_queries.py`. If Python's top-1 drifts toward `agent-decisions.md` while .NET stays on the canonical ADR — confirms Category B diagnosis and unlocks a Python-side weight adjustment (R4).

**Verdict**: ✅ Worth doing. Lower risk than re-indexing. ~30 min of work. Defer R4 (Python weight change) until queries prove it's warranted.

---

## 5. Chunker — ONNX compatibility and what's available

### 5.1 Current chunker

`tools/rag-dotnet/src/RagTools.Core/MarkdownChunker.cs` — heading-aware:

- Splits on H1–H6 boundaries (configurable via `split_levels`).
- Respects code fences (won't split inside ```).
- Token budget enforced via `SentencePieceTokenCounter.FromModelDir(modelDir)` — uses the ONNX model's own tokenizer vocab, so chunk boundaries match what the embedder will see.
- Settings (`max_tokens`, `min_tokens`, `overlap_tokens`, `split_on_headings: auto|always|never`) come from the **ZIP's** `rag-config.yaml` at ingest time.

### 5.2 Pluggability

❌ **NOT pluggable**. There is no `IChunker` interface. `MarkdownChunker` is concrete and registered directly in `Program.cs` DI:

```csharp
services.AddSingleton(_ => new MarkdownChunker(cfg.Chunker, tokenCounter));
```

Swapping the chunker means writing a new chunker class, extracting an interface, plumbing it through DI, and re-ingesting all collections.

### 5.3 ONNX constraint

The ONNX embedder (`paraphrase-multilingual-MiniLM-L12-v2`, 384-dim, max 128 tokens per chunk in this codebase) is purely text-in / vector-out. It does NOT constrain the chunker — any chunker that yields plain text within the token budget is compatible.

The real constraint is the **tokenizer**: chunks must be measured in the same SentencePiece tokens the model uses, otherwise chunks may overflow `max_seq_length` and get silently truncated.

### 5.4 Available chunker alternatives

In our codebase: **none**. There is no `SemanticChunker`, `FixedSizeChunker`, sliding-window chunker, or similar.

In the broader .NET ecosystem (would require integration work):

| Approach | Risk | Effort | When it helps |
|---|---|---|---|
| Smaller `max_tokens` + larger `overlap` | Low | Minimal (config-only change in ZIP) | Reduces chunk noise from headings packed with common terms |
| `min_tokens` higher → merge fewer small chunks | Low | Same | Prevents tiny H3 sections from being concatenated with unrelated H3s |
| Sliding-window chunker (no heading awareness) | Medium | New class | Topics that span heading boundaries (rare here) |
| Semantic chunker (embed sentence pairs, split on cosine drop) | High | New class + tuning | Long flowing prose. **Our docs are heading-rich** → low expected gain. |
| LLM-based chunker (call OpenAI/Gemini to suggest splits) | Very high | New class + cost + non-determinism | Not appropriate for an offline self-hosted RAG. |

### 5.5 Recommendation

Before any re-index with a different chunker, try **config-only tuning** of the existing `MarkdownChunker` via the ZIP's `rag-config.yaml`:

- Lower `chunker.max_tokens` from current value to ~256 (need to verify current setting).
- Raise `chunker.min_tokens` to suppress merge-noise.
- Increase `chunker.overlap_tokens` slightly so cross-section context is preserved.

If config-only tuning doesn't help, the chunker change itself is a larger project — out of scope for current Sprint 2. Document as a follow-up.

---

## 6. Open items after this diagnosis

| ID | Item | Priority | Sprint |
|---|---|---|---|
| Q-PRECISE | Add 6-8 precise queries to `queries.yaml` (Category B mismatches) | Medium | Sprint 2 (cheap) |
| R4 | Conditional Python-side weight lowering for `agent-decisions.md` based on Q-PRECISE outcome | Low | Sprint 2 if evidence supports |
| CHUNK-RETUNE | Investigate current `chunker.max_tokens`/`min_tokens` in production ZIP `rag-config.yaml`; experiment with smaller chunks via re-ingest | Low | Sprint 3+ (requires staged re-index) |
| IDX-MAIN-FILE | ADR-0028 `main_file` returns wrong file on .NET (anomalies memo #7) | Medium | Sprint 2 (next) |
| IDX-AMENDMENT-COUNT | Inflated amendment counts on .NET (anomalies memo #7) | Medium | Sprint 2 (next) |
| B2 | `query_docs_cached` MaxTopK 20 vs 45 parity decision | Medium | Sprint 2 |

---

## 6a. MCP_TRANSPORT switch — does HTTP mode have a different code path? (verified)

**Hypothesis** (raised after the initial diagnosis): maybe per-collection config
persistence is wired only in `MCP_TRANSPORT=http` mode, and the diagnosis above only
inspected the stdio path.

**Verified — NO.** Both transports register the same services:

- [`tools/rag-dotnet/src/RagTools.Mcp/Program.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Program.cs) L39:
  `var transport = (Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio").ToLowerInvariant();`
- HTTP branch (L102–L155) and stdio branch (below) both register `IDocumentStore`,
  `BatchIngestService`, `IngestWorker`, `RagQueryService`, `RagReadDocsService`,
  `GlossaryExpansionPreprocessor`, etc. The HTTP branch additionally adds
  `AddControllers()`, `IngestController`, `ApiKeyMiddleware`, `MapMcp("/")` and Kestrel
  binding — those are transport concerns only.
- `IngestController` (HTTP-only) calls `IBatchIngestService.Enqueue()` which calls
  `IngestChannel.WriteAsync()` which is consumed by `IngestWorker`. Neither writes config
  via `StoreConfigAsync`. Confirmed by grep across `tools/rag-dotnet/src/RagTools.Core/Ingest/**`:
  zero matches for `StoreConfig` or `RagConfigPayload`.
- `RagQueryService` / `RagReadDocsService` / `GlossaryExpansionPreprocessor` are
  identical singletons in both transports — each reads from the mounted singleton
  `RagConfig`, never from `IDocumentStore.FetchConfigAsync`.

**Verdict**: `MCP_TRANSPORT` toggles the protocol layer (stdio framing vs Kestrel HTTP
endpoints), not the storage or ranking layer. There is no hidden HTTP code path that
implements per-collection config persistence. The gap documented in Amendment 004
applies to BOTH transports identically.

---

## 7. References

- [`docs/reports/rag-parity-findings-2026-05-28.md`](./rag-parity-findings-2026-05-28.md) — original parity audit + post-fix metrics
- [`docs/reports/rag-parity-audit-2026-05-28.md`](./rag-parity-audit-2026-05-28.md) — auto-generated parity table (overwritten by `compare_queries.py`)
- `/memories/repo/rag-mcp-anomalies.md` entry #8 — long-form anomalies log
- [`tools/rag-dotnet/src/RagTools.Core/RagConfig.cs`](../../tools/rag-dotnet/src/RagTools.Core/RagConfig.cs)
- [`tools/rag-dotnet/src/RagTools.Core/Query/RagQueryService.cs`](../../tools/rag-dotnet/src/RagTools.Core/Query/RagQueryService.cs)
- [`tools/rag-dotnet/src/RagTools.Core/Preprocessors/GlossaryExpansionPreprocessor.cs`](../../tools/rag-dotnet/src/RagTools.Core/Preprocessors/GlossaryExpansionPreprocessor.cs)
- [`tools/rag-dotnet/src/RagTools.Core/Ingest/BatchValidator.cs`](../../tools/rag-dotnet/src/RagTools.Core/Ingest/BatchValidator.cs)
- [`tools/rag-dotnet/src/RagTools.Core/MarkdownChunker.cs`](../../tools/rag-dotnet/src/RagTools.Core/MarkdownChunker.cs)

---

## 10. R4 experiment — Python amendments down-weight (REJECTED)

**Date**: 2026-05-28 18:05 UTC  
**Trigger**: Q-PRECISE queries (Section 9 of [findings report](./rag-parity-findings-2026-05-28.md)) confirmed Python systematically over-ranks amendments and `agent-decisions.md` when the query explicitly names an ADR.

**Hypothesis**: Lowering Python `docs/adr/*/amendments/**` weight 1.20 → 1.10 in `tools/rag/rag-config.yaml` would let canonical ADRs win the top-1 slot.

**Method**:

1. Edit `tools/rag/rag-config.yaml` — `amendments/**` weight 1.20 → 1.10.
2. `docker compose --profile rag-python-http up -d --force-recreate rag-python-http`.
3. Wait ~10s for warm-up, re-run `python tools\rag\compare_queries.py` (all 26 queries).
4. Compare new top-1 path matches vs the baseline.

**Result**: top-1 matches stayed at **10/26** (identical before and after). All previously-mismatched QP queries still picked Python amendments. Edit reverted.

**Why it didn't work**: Python raw cosine scores on amendment chunks are consistently **0.05–0.15 higher** than on canonical ADR text for these queries. The multiplier change (1.20 → 1.10, a factor of 0.917) shrinks the boost by ~8%, but the raw-score gap is ~10–25%, so the amendment still wins. A weight low enough to flip the top-1 (≤ 0.85 on amendments) would over-correct on the queries where amendments ARE the correct answer (`a1-oversize-guard`, `a4-operator-notifications` for their own topics).

**Conclusion**: This is a **retrieval-quality** problem in Python, not a **weight-policy** problem. Quick-win weight tuning is exhausted. Real fixes belong in code:

| Option | Cost | Risk |
|---|---|---|
| Per-query `adr_id` detection + boost canonical file when query names an ADR | medium (regex + path-rule in `query.py`) | low — only affects queries that explicitly name an ADR |
| Hybrid BM25 + cosine ranking | high | medium — risks regression on other queries |
| Re-chunk ADRs so amendments live in the same logical chunk as the canonical text | high (re-index + chunker rules change) | high — touches every ADR; needs `metadata-rules.yaml` work |
| Accept current behaviour and prefer `.NET` for "which ADR" queries | zero | low — but asymmetric server behaviour is surprising |

Recommended next step: track as a future workstream alongside the per-collection config gap (ADR-0028 Amendment 004). Do NOT pursue in this sprint — out of "quick wins" scope.

**Related**: Section 9 of `rag-parity-findings-2026-05-28.md`.
