# RAG Tools — Setup & Usage Guide

Semantic search over the project's documentation, exposed as MCP tools to GitHub Copilot.

Three tools are available in Copilot Chat:
- `query_docs` — free-form semantic search
- `list_adrs` — list all indexed ADRs
- `get_adr_history` — fetch all chunks for a specific ADR

---

## Quick start (Python, recommended)

### 1. Install Python dependencies

```bash
cd tools/rag
python -m venv .venv
.venv\Scripts\activate          # Windows
# source .venv/bin/activate     # Linux / macOS
pip install -r requirements.txt
```

> **What this does:** creates an isolated Python environment and installs
> `sentence-transformers`, `qdrant-client`, `pyyaml`, `tqdm`, and `mcp`.
> The first `ingest` run will also download the embedding model (~500 MB) from HuggingFace.

---

### 2. Build the Docker image

```bash
# From the repo root
docker compose build rag-tools
```

> **What this does:** packages the Python code and its dependencies into a Docker image
> called `rag-tools`. The image includes an embedded Qdrant database so no separate
> Qdrant server is needed for the Python path.

---

### 3. Run the ingest pipeline

```bash
# From the repo root
docker compose --profile rag run --rm rag-tools python ingest.py
```

> **What this does:** scans all Markdown files under `docs/` and `.github/context/`,
> splits them into chunks (heading-based, 800-token max), generates embeddings using
> `paraphrase-multilingual-MiniLM-L12-v2`, and stores everything in Qdrant.
> On first run this downloads the embedding model — subsequent runs are incremental
> (only changed files are re-embedded).

**Useful flags:**

| Flag | Effect |
|------|--------|
| `--dry-run` | Scan and chunk only — prints stats, no embeddings written |
| `--force-full` | Re-index everything even if files are unchanged |

---

### 4. Verify the index

```bash
# Run a test query
docker compose --profile rag run --rm rag-tools python query.py "coupons ADR decision"
```

> **What this does:** embeds the question, searches Qdrant, and prints the top results
> with scores, breadcrumbs, and text. A non-empty result list means the index is working.

---

### 5. Register the MCP server in VS Code

The server is pre-registered in `.github/copilot/mcp.json`. VS Code Copilot reads this
file automatically. Restart VS Code after the first ingest to activate the tools.

To confirm it is active, open Copilot Chat and type:

```
@ecommerceapp-rag list_adrs()
```

You should get a list of ADR IDs from the index.

---

## Running tests

```bash
cd tools/rag
.venv\Scripts\activate
python -m pytest tests/ -v
```

> **What this does:** runs 111 unit tests covering the chunker, common utilities,
> ingest pipeline helpers, and query engine. No Qdrant or embedding model needed —
> all external dependencies are replaced with stubs.

---

## Re-indexing after docs change

Run the ingest pipeline again (step 3). It compares SHA-256 hashes against
`.rag/manifest.json` and only re-embeds files that changed.

To force a full rebuild (e.g. after changing `config.yaml` settings):

```bash
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

---

## Local development (no Docker)

If you prefer to run directly without Docker:

```bash
cd tools/rag
.venv\Scripts\activate

# Dry-run — no Qdrant needed
python ingest.py --mode memory --dry-run

# Full run with in-memory Qdrant (index lost when process exits)
python ingest.py --mode memory

# Query against the in-memory index (same process, see query.py --mode memory)
python query.py "what is the coupon max per order?" --mode memory
```

> `--mode memory` uses an in-memory Qdrant instance — no server or Docker required.
> The index is not persisted between runs.

---

## Configuration

All settings live in `tools/rag/config.yaml`. Key fields:

| Field | Default | Effect |
|-------|---------|--------|
| `source.roots` | `[docs, .github/context]` | Directories scanned for `.md` files |
| `source.exclude_globs` | see file | Patterns excluded from indexing |
| `embedder.model` | `paraphrase-multilingual-MiniLM-L12-v2` | HuggingFace model for embeddings |
| `chunker.max_tokens` | `800` | Maximum tokens per chunk |
| `chunker.overlap_tokens` | `80` | Token overlap between consecutive chunks |
| `vector_store.collection` | `ecommerceapp_docs` | Qdrant collection name |

> Changing `embedder.model`, `chunker.max_tokens`, or `metadata_rules` requires a
> `--force-full` re-index because existing vectors are incompatible with the new settings.

---

## Troubleshooting

**`No results found` from Copilot tools**  
→ Run the ingest pipeline (step 3) — the index may be empty or stale.

**`Snapshot not found` error**  
→ You are using `--mode memory` but no snapshot exists. Run ingest first.

**Model download fails or is slow**  
→ The model is ~500 MB. It is cached in `.venv` after the first download.
   If your network blocks HuggingFace, set `HF_ENDPOINT` to a mirror.

**`[ingest] nothing changed` but you expected a re-index**  
→ The file content has not changed. Use `--force-full` to override the manifest.
