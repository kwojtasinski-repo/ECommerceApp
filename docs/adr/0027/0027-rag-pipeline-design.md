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
filtering, and Python + .NET client parity. The Python client supports an in-memory mode
(`mode: memory`) which is used by e2e tests without needing a running Qdrant instance.

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
- Both implementations share `tools/rag/config.yaml` — `.NET` does not have a separate config.
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
| CI smoke tests without Qdrant | Python (in-memory Qdrant mode via `mode: memory`) |
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

---

## Implementation Status

| Component | Status | Notes |
|-----------|--------|-------|
| Python ingest + MCP | ✅ Complete | 117 unit tests pass |
| Python e2e tests | ✅ Complete | 74 e2e tests (require Python 3.13 + torch) |
| .NET ingest + MCP | ✅ Complete | 100 unit tests pass |
| .NET e2e tests | 🔲 Planned | Guard by `[SkippableFact]` when `QDRANT_URL` not set |
| Docker image (Python) | ✅ Complete | Per-file mounts, RAG_CONFIG/RAG_WORKSPACE |
| Docker image (.NET) | ✅ Complete | curlimages/curl ONNX download stage |
| Usage decision guide | 🔲 Planned | `docs/rag/WHICH-RAG.md` |

---

## Consequences

- **Good**: Zero external API cost, fully offline, fast iteration, MCP tools available in
  Copilot Chat for all team members.
- **Good**: Both implementations produce identical search results from the same corpus and
  config — the team can switch without re-indexing (same collection name, same schema).
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
- [ ] Model change → update `embedder.dimensions` in config + force full reindex + update ADR
- [ ] Config schema change → update both `RagConfig.py` (Python) and `RagConfig.cs` (.NET)
- [ ] New source root added to config → verify both ingest paths handle the root correctly
