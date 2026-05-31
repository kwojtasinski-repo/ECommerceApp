# ADR-0027: RAG Pipeline for Copilot Documentation Search — Design Assumptions and Implementation Decisions

## Status
Accepted

## Date
2026-05-18

## Context

As the ECommerceApp codebase grew (26+ ADRs, 10+ bounded contexts, architecture docs, context
files), GitHub Copilot Chat's built-in workspace search became insufficient for semantic
reasoning over project documentation. Queries like "what ADR covers TypedId?" or "show me
the full content of ADR-0016" required manual file navigation that disrupted the
AI-developer workflow.

The goal was to expose the documentation corpus to Copilot Chat via the **Model Context
Protocol (MCP)** using semantic search, without requiring an external paid API or a remote
embedding service.

---

## Decision

### 1. Approach: local RAG over MCP (not GitHub Copilot Enterprise/embeddings API)

**Alternatives considered:**

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| GitHub Copilot Enterprise semantic search | Zero infra | Paid tier, no offline, no custom ranking | ❌ Rejected |
| OpenAI Embeddings API | Accurate, SaaS | API key, token cost, data leaves repo | ❌ Rejected |
| Local RAG via MCP (chosen) | Free, offline, customizable, no data egress | Infra complexity | ✅ Chosen |

**Rationale**: The project is developed offline and behind a corporate network. No data
should leave the local machine. The MCP SDK supports stdio transport which VS Code Copilot
Chat can spawn locally without any cloud dependency.

---

### 2. Embedding model: `paraphrase-multilingual-MiniLM-L12-v2` (384 dimensions)

**Alternatives considered:**

| Model | Dims | Languages | Size | Decision |
|-------|------|-----------|------|----------|
| `all-MiniLM-L6-v2` | 384 | English | ~80 MB | ❌ Initially used; replaced |
| `paraphrase-multilingual-MiniLM-L12-v2` | 384 | 50+ | ~470 MB | ✅ Chosen |
| `all-mpnet-base-v2` | 768 | English | ~420 MB | ❌ Too large, English only |
| `text-embedding-3-small` (OpenAI) | 1536 | All | API | ❌ Rejected (data egress) |

**Rationale**: The codebase contains Polish UI labels, Polish comments, and English
documentation mixed in the same files. A multilingual model handles this without the
caller needing to detect language. The 12-layer variant outperforms the 6-layer on
retrieval quality at the same dimensionality cost.

---

### 3. Vector store: Qdrant

**Alternatives considered:**

| Store | Embedding | Local | Filter support | Decision |
|-------|-----------|-------|----------------|----------|
| Qdrant | External | ✅ Docker + in-proc | ✅ Rich | ✅ Chosen |
| FAISS | In-process | ✅ | Limited | ❌ No Python SDK for MCP pattern |
| ChromaDB | In-process | ✅ | Moderate | ❌ heavier dep, less metadata support |
| Weaviate | External | ✅ Docker | ✅ Rich | ❌ More complex setup, fewer Python e2e examples |

**Rationale**: Qdrant has the best balance of local Docker support, metadata payload
filtering, and Python + .NET client parity. The Python client supports an embedded
(file-based) mode (`VECTOR_MODE=local`) used when no separate Qdrant server is running.

> **Note (2025):** The original in-memory Qdrant mode (`mode: memory`) was removed.
> Unit tests now stub all Qdrant and embedding calls. E2E tests require a local or
> Docker Qdrant instance. See `tools/rag/tests/test_ingest_api.py` and `tests/test_e2e.py`.

---

### 4. Two implementations: Python (reference) and .NET (production)

Both implementations expose the same 4 MCP tools over stdio:

| Tool | What it returns |
|------|-----------------|
| `query_docs` | Top-K chunks ranked by semantic similarity; optional bc (bounded context) post-filter |
| `read_docs` | Top unique files (chunk view by default; full-content mode auto-detected from intent phrases) |
| `list_adrs` | Disk-based ADR directory scan — title, amendment count, main file path |
| `get_adr_history` | All chunks for a specific ADR ordered by document position |

#### Python implementation (`tools/rag/`)

**Why Python first**: `sentence-transformers` has the richest ecosystem for offline model
loading. In-process embedding eliminates a subprocess round-trip. The in-memory Qdrant mode
enables self-contained e2e tests without Docker.

**Weaknesses**: Python startup time (3–8 s for model load); torch not yet available on Python
3.14 (recommended: Python 3.13); requires a `sentence-transformers` venv.

#### .NET implementation (`tools/rag-dotnet/`)

**Why .NET**: The project is primarily a .NET shop. An ONNX Runtime-based embedder runs
inside the same process without Python as a runtime dependency. The model is the same
(`paraphrase-multilingual-MiniLM-L12-v2`) downloaded once via PowerShell from the
HuggingFace pre-exported ONNX bundle.

**Key parity decisions made during implementation** (see `agent-decisions.md` 2026-05-18):
- Both implementations share `tools/rag/rag-config.yaml` — `.NET` does not have a separate config.
- Config resolution uses 4-way priority: explicit arg → `RAG_CONFIG` env → `RAG_WORKSPACE`-derived
  path → `AppContext.BaseDirectory` fallback (Docker bundle).
- `RagConfig.Workspace` derives from the config-path grandparent (Python parity with
  `config_path.parents[2]`), then `RAG_WORKSPACE` env, then `cwd`.
- The Docker image uses `curlimages/curl` to download the pre-exported ONNX bundle instead
  of a Python `optimum-cli` stage — smaller image, no Python builder dependency.
- ONNX model downloaded locally via `pwsh tools/rag-dotnet/download-model.ps1` (no Python).

**Weaknesses**: Requires external Qdrant (no embedded mode); Docker build downloads ~470 MB
ONNX model; gRPC port 6334 must be accessible.

---

### 5. When to use which implementation

| Scenario | Use |
|----------|-----|
| Local dev, quick iteration, no Docker | Python (`tools/rag/`) |
| Local dev, Python 3.13 venv available | Python (recommended default) |
| Docker-only environment, no Python runtime allowed | .NET (`tools/rag-dotnet/`) |
| Production deployment (no sentence-transformers overhead) | .NET |
| CI smoke tests without Qdrant | Python unit tests only — all Qdrant calls are stubbed in `tests/test_ingest_api.py` |
| Adding a new MCP tool | Implement in Python first (reference), then port to .NET |

---

### 6. Chunking strategy: heading-based, 800-token max, 80-token overlap

Chunks are split on Markdown headings (H1/H2/H3) with a hard token cap of 800 tokens
(≈ 600 words). Overlap of 80 tokens prevents a question that straddles two chunks from
returning a half-answer. These parameters were tuned on the ADR corpus specifically.

---

### 7. Metadata enrichment

Every vector point carries:
- `rel_path`: repo-relative file path
- `doc_title`: H1 of the document
- `doc_kind`: derived from `metadata-rules.yaml` glob patterns (`adr_main`, `adr_amendment`,
  `adr_router`, `architecture`, etc.)
- `adr_id`: extracted from path via regex patterns in `metadata-rules.yaml`
- `breadcrumb`: heading hierarchy at the chunk location
- `start_line`: line number in the source file (used for ordering in `get_adr_history`)
- `weight`: float ranking multiplier (ADR amendments weighted 1.5×, ADR main 1.2×, other 1.0×)

The `bc` (bounded context) filter on `query_docs` and `read_docs` is a **substring
post-filter** on `breadcrumb` + `doc_title`, not a Qdrant payload filter. This mirrors the
Python reference implementation and handles BCs that appear in multiple doc kinds.

### 8. Tool output contract: return only the data asked for

Each MCP tool returns the **minimum data that satisfies the query** — it is a semantic
retrieval API, not a file reader. The agent should never need to read a whole document to
answer a focused question.

| Tool | Default output | Full-content mode trigger |
|------|---------------|--------------------------|
| `query_docs` | Top-K scored chunks with breadcrumb, path, line range, text | N/A — always chunks |
| `read_docs` | Best chunks grouped by file (chunk view) | Question matches `FullIntentRe` regex (e.g. "show me all", "full content of", "entire file") |
| `get_adr_history` | All indexed chunks for the ADR ordered by `start_line` | N/A — always chunks |
| `list_adrs` | One line per ADR: id, title, amendment count | N/A — no file content |

**Rationale**: Returning full files by default would exhaust the model's context window for
large ADRs and dilute the signal-to-noise ratio. The `FullIntentRe` regex in
`RagTools.Mcp.Tools.RagTools` (and its Python equivalent in `mcp_server.py`) is the only
gate for full-file mode.

**Constraint**: Any change to a tool that makes it return *more* data by default requires:
1. A conscious decision recorded here.
2. An update to `FullIntentRe` if the change is intent-driven.
3. Verification that the model context window budget is not exceeded for typical ADR sizes.

---

### 9. Self-containment requirement for both implementations

Both the Python (`tools/rag/`) and .NET (`tools/rag-dotnet/`) implementations must be
**self-contained**: their core logic, tests, and ingest pipeline must not depend on specific
EcommerceApp ADR numbers, domain entity names, bounded-context identifiers, or any
project-specific folder structure at test time.

**Design requirements**:
- Unit tests use synthetic/in-memory data (no real repo docs).
- E2e tests create a **synthetic workspace** with generic domain-neutral documents
  (e.g. "Alpha pattern", "Beta pattern") — no references to `CustomerId`, `OrderId`, or
  any EcommerceApp concept.
- Collection names used in tests include a UUID suffix to avoid cross-run conflicts.
- The apps discover the workspace at runtime via `RAG_WORKSPACE` / config-path derivation —
  never hardcode an absolute machine path (for example `C:\...`, `/Users/...`, `/home/...`).

**Rationale**: Self-containment enables the RAG tooling to be extracted and reused in other
projects without modification, and allows CI to run e2e tests in isolation without a full
repo checkout.

---

### 10. Multilingual query expansion — glossary-based approach

#### Problem

The embedding model (`paraphrase-multilingual-MiniLM-L12-v2`) supports 50+ languages and
produces cross-lingual embeddings that cluster semantically similar content across languages.
However, it does this by placing language-specific tokens in a shared latent space trained
for **semantic similarity**, not **translation parity**. In practice:

- Common foreign words like German `Fehler` (error/bug) or Polish `błędy` (bugs) share
  vector space with _any_ concept the model associates with "error" — including JS error
  pipelines, exception handlers, retry envelopes, and log entries.
- A query like `"znane błędy FluentAssertions aktualizacja dotnet 8"` should retrieve
  `known-issues.md` KI-008 (FluentAssertions → AwesomeAssertions) — but without expansion,
  the non-English words pull the vector toward generic "error" documents, and KI-008 placed
  at #3 or lower.

#### Solution: pre-embedding append expansion

Before embedding a query, each non-English word is matched against a YAML glossary
(`multilingual-glossary.yaml`). Any matching entry's `english` string is appended to the
query before it is passed to the model. The expanded string is then embedded — the English
expansion acts as a semantic anchor.

**File locations:**

| File | Used by |
|------|---------|
| `tools/rag/multilingual-glossary.yaml` | Python server (primary) |
| `tools/rag-dotnet/multilingual-glossary.yaml` | .NET server (own copy — can diverge) |

Both files ship with the same content but are independent copies. The .NET server loads its
copy from the workspace path via `RagConfig.GlossaryPath` with `RAG_GLOSSARY` env override
for Docker mounts.

#### The repeat=3 technique

Appending the expansion once gives the English terms only ~33% weight in mean pooling
for a 7-word non-English query:

```
original:  7 tokens
expansion: 10 tokens, appended once
English weight = 10 / (7 + 10) = 59%  ← but original non-English tokens pull in wrong direction
```

In practice, generic foreign words like `Fehler` or `błędy` are so broadly associated with
"error" concepts that a single append is insufficient. The solution is to repeat the expansion
3 times, pushing English weight to 60–87% for typical short queries:

```python
# Python — query.py
return query + (" " + expansion) * 3
```

```csharp
// .NET — MultilingualGlossary.cs
public const int ExpansionRepeat = 3;
return query + string.Concat(Enumerable.Repeat(" " + expansion, ExpansionRepeat));
```

**English-only queries are always passed through unchanged** — the glossary contains only
non-ASCII patterns so there is no risk of accidental expansion for pure English queries.

---

## Amendment 1 — Swappable Embedder Pipeline (2026-06-01)

### Context

The original design hard-wired `OnnxEmbedder` throughout the codebase (Program.cs, RagTools.cs,
IngestWorker, E2E fixtures). Adding a second provider (Ollama) required touching every call
site. Additionally, two cross-cutting concerns — glossary expansion and length truncation —
were applied inconsistently (expansion only in QueryDocs; truncation silently inside
OnnxEmbedder). There was no extension point for post-retrieval hit processing.

### Decision

Introduce a three-layer extension architecture in `RagTools.Core`:

**D1 — `IEmbedder` abstraction** (`IEmbedder.cs`)  
A single interface replacing all direct `OnnxEmbedder` references:
```csharp
public interface IEmbedder : IDisposable {
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
```
`OnnxEmbedder` and the new `OllamaEmbedder` both implement this interface. `OnnxEmbedder`
keeps existing synchronous `Embed`/`EmbedBatch` methods for backward compatibility.

**D2 — `EmbedderPipelineBuilder` + pre/post-processor chain** (`EmbedderPipelineBuilder.cs`)  
A fluent builder that wraps an inner `IEmbedder` with ordered `IEmbedderPreprocessor`
(text → text) and `IEmbedderPostprocessor` (vector → vector) stages. An `EmbedContext`
carrying `EmbedPurpose` (Query | Ingest) flows through every stage — preprocessors can
branch on purpose (e.g., glossary expansion is skipped on ingest path).

Built-in preprocessors registered by default:
- `GlossaryExpansionPreprocessor` — multilingual query expansion; no-op on ingest.
- `LengthTruncationPreprocessor` — word-count hard cap; applies on both purposes.

DI entry points:
```csharp
services.AddOnnxEmbedderPipeline(modelDir).Register();
services.AddOllamaEmbedderPipeline(cfg.Embedder.Ollama).Register();
```

**D3 — `IResultPostprocessor` extension point** (`IResultPostprocessor.cs`)  
A post-retrieval pipeline applied after hit weighting and BC filtering in `QueryDocs`.
No built-in implementation is shipped — it is a pure open extension point:
```csharp
public interface IResultPostprocessor {
    Task<IReadOnlyList<DocumentSearchResult>> ProcessAsync(
        IReadOnlyList<DocumentSearchResult> hits, QueryContext ctx, CancellationToken ct = default);
}
```

**Embedder provider selection** (`rag-config.yaml` → `embedder.provider`):
- `onnx` (default) — existing ONNX/MiniLM path, unchanged behavior.
- `ollama` — HTTP calls to local Ollama instance; `api_url`, `model`, `timeout_seconds`
  configurable under `embedder.ollama`.

### Consequences

- All callsites (`RagTools.cs`, `IngestWorker`, Program.cs, E2E fixtures) use `IEmbedder`.
- Switching providers requires only a config change (`embedder.provider`); no code change.
- Pre/post processor lists are ordered and DI-registered; teams can add processors by
  implementing `IEmbedderPreprocessor`/`IEmbedderPostprocessor` and calling `.WithPreprocessor<T>()`.
- `IResultPostprocessor` allows injecting reranking, deduplication, or citation enrichment
  without modifying `RagTools.cs`.
- `OnnxEmbedder` remains unchanged for sync callers (ingest script); async wrappers added.
- When provider is `ollama`, the SentencePiece model dir is not required (guard skipped).


#### Matching rules

- Case-insensitive, word-boundary-aware regex: `(?<![a-z])<pattern>(?![a-z])`
- Each glossary entry contributes its `english` string at most once per query (even if
  multiple patterns from the same entry match)
- Multiple entries can match the same query (each adds its own English group)
- The lookbehind/lookahead only tests ASCII letters `[a-z]`, so Unicode word boundaries
  (e.g. `ę`, `ś`, `ą`) are handled correctly

#### Benchmark results (2026-05-19)

15 queries across English / Polish / German against both servers after expansion was added:

| Language | Python (top-1 correct) | .NET (top-1 correct) |
|----------|------------------------|----------------------|
| English  | 5/5 ✅                | 5/5 ✅              |
| Polish   | 5/5 ✅                | 5/5 ✅              |
| German   | 3/5 ✅                | 4/5 ✅              |
| **Total**| **13/15**             | **14/15**           |

The two remaining German misses on Python are vocabulary gaps (the German word for
"entity identifier", `Bezeichner`, is not in the glossary) or expansion-group competition
(a query containing both `Bestellung` and `Rückabwicklung` triggers the orders expansion
AND the saga expansion simultaneously).

#### How to add a new language or concept (newbie guide)

1. Open `tools/rag/multilingual-glossary.yaml` (and mirror the change to `tools/rag-dotnet/multilingual-glossary.yaml`).
2. Find the relevant concept group, or add a new one at the bottom.
3. Add the foreign-language words as pattern entries.
4. **No re-indexing required** — expansion runs at query time, not at ingest time.
5. Test by querying the MCP server: `query_docs("your foreign query", top_k=3)`.

Rules for patterns:
- Use individual words/compounds in lowercase
- Avoid extremely common words (`the`, `und`, `i`) — they appear everywhere and add noise
- Prefer domain-specific terms and inflection forms (genitive, accusative, plural)
- English words must never appear in `patterns` — the glossary is for non-English only

---


|-----------|--------|-------|
| Python ingest + MCP | ✅ Complete | 117 unit tests pass |
| Python e2e tests | ✅ Complete | 74 e2e tests (require Python 3.13 + torch) |
| .NET ingest + MCP | ✅ Complete | 100 unit tests pass |
| .NET e2e tests | ✅ Complete | 12 `[SkippableFact]` tests; skip when model or Docker absent |
| Docker image (Python) | ✅ Complete | Per-file mounts, RAG_CONFIG/RAG_WORKSPACE |
| Docker image (.NET) | ✅ Complete | curlimages/curl ONNX download stage |
| Multilingual glossary (Python) | ✅ Complete | `tools/rag/multilingual-glossary.yaml`; 11 concept groups; repeat=3 |
| Multilingual glossary (.NET) | ✅ Complete | `tools/rag-dotnet/multilingual-glossary.yaml`; mirror of Python copy |
| Usage decision guide | ✅ Complete | `docs/rag/SETUP-GUIDE.md` — beginner setup + model switching |

---

## Consequences

- **Good**: Zero external API cost, fully offline, fast iteration, MCP tools available in
  Copilot Chat for all team members.
- **Good**: Both implementations share config, schema, and collection name so the team can
  switch transports without re-indexing.
- **Known limitation (.NET tokenizer)**: `paraphrase-multilingual-MiniLM-L12-v2` uses
  SentencePiece (XLM-RoBERTa) — the HuggingFace repo has no `vocab.txt`. The .NET
  `BertTokenCounter` requires WordPiece `vocab.txt`, so the download script provides the
  BERT base uncased vocabulary as a functional stand-in. Embeddings are valid 384-d vectors
  and differ between texts, but semantic ranking quality is lower than the Python path (which
  uses the correct SentencePiece tokenizer). Alternative: switch to `all-MiniLM-L6-v2` or
  `all-MiniLM-L12-v2` — BERT-based, native `vocab.txt`, 384-d, English-only, drop-in
  compatible. See `tools/rag-dotnet/README.md` for switching instructions.
- **Trade-off**: Two codebases to maintain. Mitigated by: shared config, shared schema, shared
  test fixtures for the fake workspace, and the Python implementation as the canonical reference.
- **Trade-off**: Python requires Python 3.13 (not 3.14) for the e2e suite due to torch wheel
  availability. Documented in README and e2e test skip message.
- **Constraint**: Any new MCP tool must be added to **both** implementations simultaneously
  (or explicitly tracked in this ADR's Implementation Status table until ported).

---

## Conformance checklist

- [ ] New MCP tool added to Python → also added to .NET (or backlog item created here)
- [ ] `metadata-rules.yaml` change → run `ingest --force-full` to rebuild the index
- [ ] Model change → update `embedder.model` in config + force full reindex (`--force-full`) + update ADR
- [ ] Config schema change → update both `RagConfig.py` (Python) and `RagConfig.cs` (.NET)
- [ ] New source root added to config → verify both ingest paths handle the root correctly
- [ ] New language added to glossary → edit `tools/rag/multilingual-glossary.yaml` AND `tools/rag-dotnet/multilingual-glossary.yaml` — **no re-index needed**
- [ ] New concept added to glossary → same as above; verify with a live query before committing
- [ ] `repeat` value changed in `_expand_query` / `MultilingualGlossary.Expand` → re-run the 15-query benchmark (see §10) to confirm no regression
