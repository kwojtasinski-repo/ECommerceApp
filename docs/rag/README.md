# RAG — Retrieval-Augmented Generation for ECommerceApp

> **New here? Start with [SETUP-GUIDE.md](SETUP-GUIDE.md)** — step-by-step instructions
> to get from zero to a working Copilot Chat with project-aware answers in ~10 minutes.

---

## What is this?

Instead of Copilot guessing answers from its generic training data, RAG makes it retrieve
the specific chunk from *this repo's* documentation before answering.

**Example:** asking "What does ADR-0016 say about coupon limits?" returns the exact text
from `docs/adr/0016/...` — not a made-up answer.

---

## Quick overview

```
Your docs (docs/, .github/context/)
      -
      ¡  ingest.py (one-time + after changes)
      -  chunks text, generates 384-dim embeddings, stores in Qdrant
      -
      ¡
   Qdrant  ‹¦¦¦ mcp_server.py ‹¦¦¦ VS Code Copilot Chat
  (vector DB)   (4 MCP tools)       (query_docs / read_docs / list_adrs / get_adr_history)
```

**Qdrant** is a vector database — it stores the embeddings and searches them by
semantic similarity. It runs as a Docker container.

**MCP tools** exposed to Copilot Chat:

| Tool | What it does |
|------|-------------|
| `query_docs(question)` | Semantic search — returns the most relevant doc chunks |
| `read_docs(question)` | Full file content of the top-ranked matches (better for reasoning) |
| `list_adrs()` | Lists all indexed ADRs with titles and amendment counts |
| `get_adr_history(adr_id)` | Returns the full text of one ADR + all its amendments |

---

## Implementations

Two implementations exist — both expose the same MCP tools and use the same model:

| | **Python** | **.NET** |
|---|---|---|
| Status | ? Production-ready | ?? Experimental |
| Recommended for | **Everyone** | Advanced / .NET-only setups |
| Model accuracy | ? Full | ?? Slightly reduced (tokenizer workaround) |
| Polish query support | ? Yes | ?? Reduced |

**Use Python unless you have a specific reason not to.**

---

## Where to go next

| Goal | Doc |
|------|-----|
| **Set up for the first time** | [SETUP-GUIDE.md](SETUP-GUIDE.md) |
| **Understand the architecture** | [rag-architecture.md](rag-architecture.md) |
| **Check index stats** | [index-stats.md](index-stats.md) (Python) / [index-stats-dotnet.md](index-stats-dotnet.md) (.NET) |
| **Python source code** | [tools/rag/](../../tools/rag/) |
| **.NET source code** | [tools/rag-dotnet/](../../tools/rag-dotnet/) |

---

## How chunking works

Documents are split at heading boundaries (H1–H6 in auto mode; configurable via `split_on_headings` in `rag-config.yaml`) with an 800-token max per chunk
and 80-token overlap between consecutive chunks. Each chunk's embed text is prefixed
with its breadcrumb so similarity search captures section context:

```
ADR-0016 — Coupons > §3 Validation rules > Max coupons per order

The maximum number of coupons per order is controlled by CouponsOptions.MaxCouponsPerOrder
(default: 5, ceiling: 10). ...
```

---

## Ranking weights

After similarity search, each hit's score is multiplied by a path-based weight.
First-matching glob wins. Configured in `tools/rag/rag-config.yaml › ranking.weights`.

| Weight | Path pattern | Why |
|--------|-------------|-----|
| 1.25 | `known-issues.md` | Bug-fix gate — always top priority |
| 1.20 | `agent-decisions.md` | Correction history — high signal |
| 1.20 | `docs/adr/*/amendments/**` | Amendments override original ADR sections |
| 1.15 | `project-state.md` | BC block status — critical |
| 1.10 | `docs/adr/*/example-implementation/**` | Concrete code examples |
| 1.00 | `docs/adr/*/[0-9]*-*.md` | Main ADR file |
| 0.70 | `docs/roadmap/**` | Forward-looking, lower priority for "how" questions |
| 0.40 | `docs/adr/*/checklist.md` | Indexed but rarely the primary answer |

---

## Multilingual support

The embedder is `paraphrase-multilingual-MiniLM-L12-v2` (384-dim, 50+ languages).
Before embedding, the query is expanded using `tools/rag/multilingual-glossary.yaml` —
Polish/German domain terms are mapped to English equivalents and appended 3× to boost
weight in mean pooling. No re-indexing needed; this is query-time only.

**Benchmark (2026-05-19):** EN 5/5, PL 5/5, DE 3–4/5 correct top-1.

---

## CLI usage

```powershell
# From the repo root, with local Python venv active:
python tools/rag/query.py "how does the order placement saga handle compensation?"

# Top-10, JSON output for piping
python tools/rag/query.py "coupon validation" --top-k 10 --json

# Filter by bounded context
python tools/rag/query.py "stock adjustment" --bc Inventory
```
