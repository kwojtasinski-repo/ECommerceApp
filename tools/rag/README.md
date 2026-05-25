# RAG Tools — Python Implementation

Semantic search over the project's documentation, exposed as MCP tools to GitHub Copilot.

> **Getting started?** See the full user guide at
> [`docs/rag/SETUP-GUIDE.md`](../../docs/rag/SETUP-GUIDE.md).
> This file is the developer reference for the Python source code.

Three tools are available in Copilot Chat:

- `query_docs` — free-form semantic search over all indexed docs
- `list_adrs` — list all indexed ADRs with titles and amendment counts
- `get_adr_history` — fetch the full text of one ADR + all its amendments

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

### 2. Build the Docker image (one-time)

```bash
# From the repo root
docker compose build rag-tools
```

> **What this does:** packages `ingest.py`, `mcp_server.py`, and all Python dependencies
> into a Docker image called `rag-tools`. The embedding model (~450 MB) is downloaded
> during the first build — subsequent builds are cached.
> When running in Docker the server uses an **embedded Qdrant** (no separate server needed).
> When running locally (outside Docker) Qdrant must be started separately.

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

| Flag           | Effect                                                    |
| -------------- | --------------------------------------------------------- |
| `--dry-run`    | Scan and chunk only — prints stats, no embeddings written |
| `--force-full` | Re-index everything even if files are unchanged           |

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

```powershell
# From the repo root

# Unit tests only — no Qdrant or embedding model needed (~3 s)
cd tools/rag
.venv/Scripts/python.exe -m pytest tests/ -v -m "not http_streamable"

# All tests including E2E (requires Qdrant running)
pwsh tools/rag/run-tests.ps1 -StartQdrant

# Full suite: Python unit + E2E + .NET
pwsh tools/rag/run-all-tests.ps1
```

> **Unit + integration tests (278):** cover `OperationStore`, `IngestWorker`, HTTP routes,
> auth middleware, `QueryEngine`, and MCP tool handlers.
> No Qdrant or embedding model needed — all external dependencies are stubbed.
> Tests live under `tests/` (not at the repo root).
>
> **E2E tests (13, `http_streamable` marker):** spin up a real MCP server + uvicorn,
> test the full HTTP round-trip against a live Qdrant instance.

---

## Re-indexing after docs change

Run the ingest pipeline again (step 3). It compares SHA-256 hashes against
`.rag/manifest.json` and only re-embeds files that changed.

To force a full rebuild (e.g. after changing `rag-config.yaml` settings):

```bash
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

---

## Local Python setup (no Docker for ingest)

Only needed if you want to develop or debug the Python RAG code:

```bash
# Requires Python 3.13 (NOT 3.14 — torch wheels are not available for 3.14)
python -m venv tools/rag/.venv
tools/rag/.venv/Scripts/Activate.ps1      # Windows
# source tools/rag/.venv/bin/activate    # Linux / macOS
pip install -r tools/rag/requirements.txt

# Dry-run — parse + chunk only, no Qdrant or embeddings needed
python ingest.py --dry-run

# Full run — Qdrant must be running (docker compose --profile rag up qdrant -d)
python ingest.py

# MCP server (VS Code usually starts this automatically)
python mcp_server.py
```

> **Requires Qdrant:** unlike the in-process Docker image, local Python runs
> always connect to a Qdrant server. Use `docker compose --profile rag up qdrant -d`
> to start one before running `ingest.py` or `mcp_server.py`.

---

## Configuration

All settings live in `tools/rag/rag-config.yaml`. Key fields:

| Field                     | Default                                 | Effect                                   |
| ------------------------- | --------------------------------------- | ---------------------------------------- |
| `source.roots`            | `[docs, .github/context]`               | Directories scanned for `.md` files      |
| `source.exclude_globs`    | see file                                | Patterns excluded from indexing          |
| `embedder.model`          | `paraphrase-multilingual-MiniLM-L12-v2` | HuggingFace model for embeddings         |
| `chunker.max_tokens`      | `800`                                   | Maximum tokens per chunk                 |
| `chunker.overlap_tokens`  | `80`                                    | Token overlap between consecutive chunks |
| `vector_store.collection` | `ecommerceapp_docs`                     | Qdrant collection name                   |

> **`VECTOR_MODE` env var** controls whether the Python code connects to an embedded Qdrant
> (file on disk) or a separate Qdrant server over HTTP.
> - `VECTOR_MODE=local` — embedded, no server needed (default inside the Docker container).
> - `VECTOR_MODE=docker` — connects to Qdrant at `vector_store.url` (default for local dev).
>
> The Docker container sets `ENV VECTOR_MODE=local` automatically.
> docker-compose sets `VECTOR_MODE=docker` for its services.
> You can also override per-run with `--mode docker` on the `ingest.py` CLI.

> Changing `embedder.model`, `chunker.max_tokens`, or `metadata_rules` requires a
> `--force-full` re-index because existing vectors are incompatible with the new settings.

---

## Troubleshooting

**`No results found` from Copilot tools**  
→ Run the ingest pipeline (step 3) — the index may be empty or stale.

**Model download fails or is slow**  
→ The model is ~500 MB. It is cached in `.venv` after the first download.
If your network blocks HuggingFace, set `HF_ENDPOINT` to a mirror.

**`[ingest] nothing changed` but you expected a re-index**  
→ The file content has not changed. Use `--force-full` to override the manifest.
