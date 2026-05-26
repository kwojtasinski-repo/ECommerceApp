# RAG Setup Guide — From Zero to First Query

This guide walks through setting up the RAG (retrieval-augmented generation) system
that lets VS Code Copilot Chat answer questions about this codebase using the project's
own documentation, ADRs, and context files.

> **What this does in practice:** instead of Copilot guessing answers from its training
> data, it retrieves the specific chunk from *your* docs before answering. Questions like
> "What does ADR-0016 say about coupon limits?" or "What are the known issues with
> the refund flow?" get answered from the actual files.

---

## Step 0 — Choose your implementation

> **TL;DR: use Python.** It's simpler, more accurate, and fully supported.
> Only choose .NET if you have a specific reason (e.g., no Python allowed in your environment).

| | Python | .NET |
|---|---|---|
| **Status** | ✅ Production-ready | ⚠️ Experimental |
| **Recommended for** | All developers | .NET-primary / advanced |
| **Prerequisites** | Docker Desktop | Docker Desktop + .NET 10 SDK |
| **Model accuracy** | ✅ Full (correct tokenizer) | ⚠️ Slightly reduced (WordPiece workaround) |
| **Polish query support** | ✅ Yes | ⚠️ Reduced |

> **What is Docker Desktop?** A free tool that lets you run Linux containers on Windows/macOS.
> Download from [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop/).
> You do NOT need to know Docker to use this setup — the commands are copy-paste.

---

## Python implementation

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- VS Code with the **GitHub Copilot** extension
- That's it — no Python install needed for the Docker path

> **Check Docker is running:** open a terminal and run `docker ps`. You should see a table header (even if empty). If you get an error, start Docker Desktop first.

### One-time setup

```powershell
# From the repo root

# 1. Build the Python RAG image (downloads the model ~450 MB — first build only)
docker compose build rag-tools

# 2. Start Qdrant (the vector database)
docker compose --profile rag up qdrant -d

# 3. Index the documentation
#    Scans docs/ and .github/context/, splits into chunks, embeds, upserts to Qdrant.
#    Takes 1–3 minutes first time; subsequent runs are incremental (seconds).
docker compose --profile rag run --rm rag-tools python ingest.py
```

After step 3, verify the index is ready:

```powershell
Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs | ConvertTo-Json
# Expect: "status": "green" with a non-zero points_count
```

### Connect VS Code

The MCP server is already configured in [`.vscode/mcp.json`](../../.vscode/mcp.json).
VS Code reads this file automatically.

1. Open VS Code in this workspace
2. Open **Copilot Chat** (Ctrl+Shift+I)
3. Click the **Tools** button → **MCP** section
4. Enable `ecommerceapp-rag-python` (local Python — fastest) or `ecommerceapp-rag-python-docker`

> **Which MCP variant?**
> - `ecommerceapp-rag-python` — runs `mcp_server.py` from the local `.venv` (no Docker start needed
>   once the venv is set up; requires Python 3.13 locally)
> - `ecommerceapp-rag-python-docker` — VS Code spawns a `docker run` for each chat session;
>   Qdrant must be running (`docker compose --profile rag up qdrant -d`)

### Using Copilot Chat

Once the server is enabled, use it like this:

```
List the latest ADRs.

What does ADR-0027 say about the output contract for MCP tools?

Are there any known issues with the refund flow?

What is the maximum number of coupons per order?

What is the current project state — any blocked bounded contexts?
```

> **Tips:**
> - Ask "list_adrs" or "list adrs" to see all indexed architectural decisions
> - Prefix with "get_history for ADR-0016" to get all indexed chunks for a specific ADR + its amendments
> - Use "read_docs about X" to get the full content of a relevant file
> - Queries in **English, Polish, and German** are supported — see below

### Multilingual queries (Polish / German)

The MCP server automatically expands non-English queries before embedding them. You do not
need to translate — just ask in Polish or German and the server bridges the gap:

```
# Polish
Jaka jest maksymalna liczba kuponów na zamówienie?
Znane błędy — FluentAssertions, aktualizacja dotnet 8?
Jak działa saga kompensacyjna przy nieudanym zamówieniu?

# German
Maximale Anzahl Gutscheine pro Bestellung?
Bekannte Fehler FluentAssertions Aktualisierung dotnet 8?
Wie funktioniert die Saga-Kompensation bei fehlgeschlagener Bestellung?
```

**How it works:** before embedding, the query is matched against
`tools/rag/multilingual-glossary.yaml`. Any matching entry's English terms are appended 3×
to the query so English concepts outweigh the non-English words in mean pooling.
No re-indexing is needed — this is query-time only.

**Adding a new language or concept:** open the glossary YAML (both `tools/rag/` and
`tools/rag-dotnet/` copies), add a pattern entry, commit. Done — no Docker rebuild, no
re-index.

**Benchmark (2026-05-19):** EN 5/5, PL 5/5, DE 3–4/5 top-1 correct across both servers.

### Re-indexing after doc changes

Ingest is incremental — re-run it whenever you add or change files under `docs/` or
`.github/context/`:

```powershell
docker compose --profile rag run --rm rag-tools python ingest.py
```

Or force a full re-index (e.g., after changing the model or chunk settings):

```powershell
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

### Local Python setup (no Docker for ingest)

Only needed if you want to develop or debug the Python RAG code:

```powershell
# Requires Python 3.13 (NOT 3.14 — torch wheels are not available for 3.14)
python -m venv tools/rag/.venv
tools/rag/.venv/Scripts/Activate.ps1
pip install -r tools/rag/requirements.txt

# Run ingest from the repo root
cd <repo-root>
python tools/rag/ingest.py

# Run MCP server manually (VS Code usually does this for you)
python tools/rag/mcp_server.py
```

---

## .NET implementation

> **When to use .NET:** if your team uses VS Code Dev Containers or Docker, and
> you want a zero-Python setup. Note the [tokenizer limitation](../../tools/rag-dotnet/README.md)
> — semantic ranking quality is somewhat lower than the Python path.

### Prerequisites

- Docker Desktop
- .NET 10 SDK (for local dev without Docker)
- VS Code with GitHub Copilot extension

### One-time setup

```powershell
# From the repo root

# 1. Build the .NET RAG image
#    (Downloads 450 MB ONNX model in the Docker build — first build takes ~5 min)
docker compose build rag-dotnet

# 2. Start Qdrant (shared with Python — skip if already running)
docker compose --profile rag-dotnet up qdrant -d

# 3. Index the documentation
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll
```

Verify:

```powershell
Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs_dotnet | ConvertTo-Json
# Expect: "status": "green" with points_count > 0
```

### Connect VS Code

The .NET MCP server entries are already configured in [`.vscode/mcp.json`](../../.vscode/mcp.json):

- `ecommerceapp-rag-dotnet` — runs `dotnet run` from source (requires .NET 10 SDK + model downloaded)
- `ecommerceapp-rag-dotnet-docker` — spawns `docker run` (requires `rag-dotnet` image built)

1. Open VS Code in this workspace
2. Open **Copilot Chat** → **Tools** → **MCP**
3. Enable `ecommerceapp-rag-dotnet` or `ecommerceapp-rag-dotnet-docker`

### Local .NET development (no Docker for ingest)

```powershell
# One-time: download the ONNX model (~490 MB total)
pwsh tools/rag-dotnet/download-model.ps1

# Start Qdrant (needed for ingest and MCP)
docker compose --profile rag-dotnet up qdrant -d

# Run ingest
cd tools/rag-dotnet
dotnet run --project src/RagTools.Ingest

# Run MCP server (usually started by VS Code)
dotnet run --project src/RagTools.Mcp
```

---

## Switching models

The active model is set in [`tools/rag/rag-config.yaml`](../../tools/rag/rag-config.yaml)
under `embedder.model`. Both implementations share this file.

> **Warning:** changing the model requires a full re-index (`--force-full`).
> Old vectors are incompatible with new model embeddings.
> The `rag-config.yaml` `version` field is automatically checked — increment it
> to force a full re-index on all machines.

### Python — change model

Edit `tools/rag/rag-config.yaml`:

```yaml
embedder:
  model: "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"  # current (multilingual)
  # model: "sentence-transformers/all-MiniLM-L6-v2"    # English-only, smaller, no workaround needed
```

Then rebuild the image and re-index:

```powershell
docker compose build rag-tools
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

### .NET — change model

The .NET path downloads the ONNX model separately. To switch:

1. Edit `tools/rag-dotnet/download-model.ps1` — update `$Base` to the new model's
   HuggingFace URL. If the new model is BERT-based (has native `vocab.txt`), remove
   the `$BertBase` workaround and set `vocab.txt` back to `"$Base/vocab.txt"`.

2. Edit `tools/rag-dotnet/Dockerfile` — update `ARG MODEL_BASE` to match.

3. Delete `tools/rag-dotnet/model/` and re-run the download script:
   ```powershell
   Remove-Item -Recurse tools/rag-dotnet/model
   pwsh tools/rag-dotnet/download-model.ps1
   ```

4. Rebuild and re-ingest:
   ```powershell
   docker compose build rag-dotnet
   docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll
   ```

**Drop-in alternatives for .NET** (no tokenizer mismatch, same 384-d):

| Model | Size | Languages | Notes |
|-------|------|-----------|-------|
| `all-MiniLM-L6-v2` | 90 MB | English | Fastest, semantically accurate |
| `all-MiniLM-L12-v2` | 133 MB | English | Better quality than L6 |
| *(current)* `paraphrase-multilingual-MiniLM-L12-v2` | 450 MB | 50+ | BERT vocab workaround |

---

## Troubleshooting

### MCP tools not showing in Copilot

- Restart VS Code after first-time setup
- Check the MCP panel (Copilot Chat → Tools → MCP) — the server must be toggled on
- Run `docker ps` to confirm Qdrant is running

### "No results found" / empty answers

- The index might be empty. Run the ingest step and check `points_count` in Qdrant:
  ```powershell
  Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs
  ```
- If `points_count` is 0, re-run ingest

### Ingest fails with "connection refused"

- Qdrant is not running. Start it:
  ```powershell
  docker compose --profile rag up qdrant -d    # Python path
  docker compose --profile rag-dotnet up qdrant -d   # .NET path
  ```

### Python `.venv` gives "torch not found" on Python 3.14+

- PyTorch wheels are not available for Python 3.14. Use Python 3.13:
  ```powershell
  py -3.13 -m venv tools/rag/.venv
  ```
- Docker always uses the pinned 3.13 image — no action needed for Docker path

### .NET: "Model file not found"

- Run `pwsh tools/rag-dotnet/download-model.ps1` to download the ONNX model
- The script downloads to `tools/rag-dotnet/model/` (gitignored)

### .NET: Docker build fails at curl step

- The Dockerfile downloads the model during build. If HuggingFace is slow or
  unavailable, the build may time out. Retry or pre-download locally with the
  PowerShell script and mount the `model/` directory instead of downloading in Docker.

---

## Remote ingest mode (--remote)

Both the Python and .NET ingest tools support a `--remote` flag that sends documents
to a running MCP server over HTTP instead of embedding locally. This is useful in CI
or when you want to run ingest without the ONNX model installed locally.

### When to use --remote

- CI pipeline: build once, index once, then push from any machine
- Dev container: server is containerized; local machine has no Python/model
- Team shared index: one "indexer" machine runs the server; others push to it

### How to start the server in HTTP mode

```powershell
# Python HTTP server (port 3002, MCP Streamable HTTP, mcp>=1.8.0)
docker compose --profile rag-python-http up -d rag-python-http

# .NET HTTP server (port 3001)
docker compose --profile rag-dotnet-http up -d rag-dotnet-http
```

Verify it's up:

```powershell
Invoke-WebRequest http://localhost:3002/admin/stats   # Python
Invoke-WebRequest http://localhost:3001/admin/stats   # .NET
```

### Push documents via --remote

```powershell
# Python ingest → Python HTTP server
python tools/rag/ingest.py --remote http://localhost:3002

# .NET ingest → .NET HTTP server (from tools/rag-dotnet/)
dotnet run --project src/RagTools.Ingest -- --remote http://localhost:3001

# With API key (if RAG_API_KEY is set on the server)
python tools/rag/ingest.py --remote http://localhost:3002 --api-key mysecret
$env:RAG_API_KEY = 'mysecret'   # alternative: set env var
```

### Monitor ingest status

After uploading, documents are processed asynchronously. Check operation status:

```powershell
# List recent operations for a collection
Invoke-RestMethod http://localhost:3002/ingest/ecommerceapp_docs/operations

# Poll a specific operation to completion
$opId = 'abc123'
Invoke-RestMethod "http://localhost:3002/ingest/ecommerceapp_docs/operations/$opId"
# status: Queued → Processing → Completed (or Failed with errorMessage)
```

Operations are retained in memory for **1 hour** after enqueueing (same for both
Python `RETENTION_HOURS = 1` and .NET `RetentionPeriod = TimeSpan.FromHours(1)`).

### HTTP API reference

The `--remote` CLI flag uses these HTTP endpoints internally. If you need to integrate
directly (e.g., from a CI script without Python or .NET tooling), here is the full contract.

Document classification (`doc_kind`, `adr_id`) is applied by the CLI locally before upload
and sent as part of each request body — no separate config upload step is needed.

**Authentication** — all `/ingest/*` and `/admin/*` routes require:
```
X-Api-Key: <value of RAG_API_KEY env var on the server>
```
Omit the header when `RAG_API_KEY` is not set (dev mode).

**Error envelope** — all error responses (auth failures, malformed body, unhandled
exceptions, MCP tool failures) follow the same shape on both stacks:
```json
{ "error": "<sanitized message, max 500 chars, paths stripped>", "code": "<bucket>" }
```
Buckets: `BadRequest`, `Unauthorized`, `HttpError`, `NotImplemented`, `InternalServerError`.
Stack traces are never returned to the client (full traces stay in server logs).
See [`rag-architecture.md` §14](rag-architecture.md#14-error-handling-sanitisation-and-middleware)
for the full middleware / handler layering.

---

#### Step 1 — Upload one document (`POST /ingest/{collection}`)

Send one document per request. Repeat for every file to index.

```powershell
$body = @{
    relPath  = 'docs/adr/0016/0016-sales-coupons.md'
    content  = Get-Content docs/adr/0016/0016-sales-coupons.md -Raw
    doc_kind = 'adr'          # optional; server infers from /config rules if omitted
} | ConvertTo-Json

$resp = Invoke-RestMethod http://localhost:3002/ingest/ecommerceapp_docs `
    -Method POST `
    -ContentType 'application/json' `
    -Headers @{ 'X-Api-Key' = $env:RAG_API_KEY } `
    -Body $body

$opId = $resp.operationId
```

Request body:

| Field | Type | Required | Description |
|---|---|---|---|
| `relPath` / `rel_path` | `string` | ✅ | Relative path used as the document identifier |
| `content` | `string` | ✅ | Full text of the document |
| `doc_kind` / `docKind` | `string` | ❌ | Classification hint; auto-detected from `/config` rules if omitted |

Response `202 Accepted`:
```json
{
  "operationId": "ecommerceapp_docs:docs-adr-0016-...:...",
  "status": "Queued",
  "location": "/ingest/ecommerceapp_docs/operations/<opId>"
}
```

Response `503 Service Unavailable` — queue is full (capacity 100 Python / 1000 .NET); retry.

---

#### Step 2 — Poll until complete (`GET /ingest/{collection}/operations/{opId}`)

```powershell
do {
    Start-Sleep 2
    $status = Invoke-RestMethod `
        "http://localhost:3002/ingest/ecommerceapp_docs/operations/$opId" `
        -Headers @{ 'X-Api-Key' = $env:RAG_API_KEY }
    Write-Host $status.status
} until ($status.status -in 'Completed', 'Failed')

if ($status.status -eq 'Failed') { Write-Error $status.errorMessage }
```

Poll response:

| Field | Description |
|---|---|
| `status` | `Queued` → `Processing` → `Completed` \| `Failed` |
| `operationId` | Same ID from step 1 |
| `relPath` | Document path |
| `errorMessage` | Non-null only when `status == "Failed"` |
| `completedAt` | ISO-8601 timestamp, non-null when done |

---

---

#### Step 1b — Upload many documents in one request (`POST /ingest/{collection}/batch`)

Pack multiple files into a ZIP archive and POST the raw bytes. Useful for initial bulk
loads or CI pipelines that process many docs at once.

```powershell
# Build a ZIP from a folder
$zip = [System.IO.Path]::GetTempFileName() + '.zip'
Compress-Archive docs/* $zip -Force
$bytes = [System.IO.File]::ReadAllBytes($zip)

$resp = Invoke-RestMethod http://localhost:3002/ingest/ecommerceapp_docs/batch `
    -Method POST `
    -ContentType 'application/zip' `
    -Headers @{ 'X-Api-Key' = $env:RAG_API_KEY } `
    -Body $bytes

$resp.count       # number of files queued
$resp.operations  # array of {operationId, relPath, statusUrl}
```

Response `202 Accepted`:
```json
{
  "batchId": "<uuid>",
  "count": 3,
  "operations": [
    { "operationId": "<id1>", "relPath": "docs/file-a.md", "statusUrl": "/ingest/.../operations/<id1>" },
    { "operationId": "<id2>", "relPath": "docs/file-b.md", "statusUrl": "/ingest/.../operations/<id2>" }
  ]
}
```

| Status | Meaning |
|--------|----------|
| `202` | All files queued; poll each `statusUrl` from step 2 |
| `400` | Body is not a valid ZIP, or the ZIP contains no files |
| `503` | Queue cannot fit all files; reduce batch size or retry later |

Directory entries inside the ZIP are silently skipped.

---

#### All endpoints at a glance

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/ingest/{collection}` | ✅ | Queue one document; returns 202 + operationId |
| `POST` | `/ingest/{collection}/batch` | ✅ | Queue many documents from a ZIP; returns 202 + operations list |
| `GET` | `/ingest/{collection}/operations/{opId}` | ✅ | Poll status of one operation |
| `GET` | `/ingest/{collection}/operations` | ✅ | List all recent operations for a collection |
| `GET` | `/admin/stats` | ✅ | Queue depth + retention hours |

Both Python (port `3002`) and .NET (port `3001`) servers expose the same endpoints with
the same request/response schema.

---

## Running the test suite

### Python tests (unit + E2E)

```powershell
# Unit tests only — no Qdrant needed, ~2s
pwsh tools/rag/run-tests.ps1 -UnitOnly

# All tests — auto-starts and stops Qdrant
pwsh tools/rag/run-tests.ps1 -StartQdrant

# All tests — Qdrant already running
pwsh tools/rag/run-tests.ps1

# Verbose output
pwsh tools/rag/run-tests.ps1 -StartQdrant -Verbose
```

Test files:

| File | Tests | Needs Qdrant | Description |
|------|-------|-------------|-------------|
| `tools/rag/tests/test_ingest_api.py` | 54 | No | Unit tests for OperationStore, IngestWorker, routes, auth middleware, batch endpoint |
| `tools/rag/test_ingest_e2e.py` | 16 | Yes (1 test) | E2E: real uvicorn server, full HTTP round-trip, one Qdrant pipeline test |

### .NET tests (unit + E2E)

```powershell
# Download model first (one-time, ~490 MB)
pwsh tools/rag-dotnet/download-model.ps1

# Unit tests only — no Qdrant or model needed
pwsh tools/rag-dotnet/run-tests.ps1 -UnitOnly

# All tests — auto-starts and stops Qdrant
pwsh tools/rag-dotnet/run-tests.ps1 -StartQdrant

# E2E tests with QDRANT_URL already set
$env:QDRANT_URL = 'http://localhost:6333'
pwsh tools/rag-dotnet/run-tests.ps1 -E2EOnly
```

### Run everything

```powershell
# Full suite — Python (unit + E2E) + .NET — Qdrant started/stopped automatically
pwsh tools/rag/run-all-tests.ps1

# Skip .NET (e.g., model not downloaded)
pwsh tools/rag/run-all-tests.ps1 -SkipDotNet

# Skip Python
pwsh tools/rag/run-all-tests.ps1 -SkipPython
```

---

## Quick reference

| Task | Command |
|------|---------|
| Start Qdrant (Python) | `docker compose --profile rag up qdrant -d` |
| Build Python image | `docker compose build rag-tools` |
| Ingest docs (Python) | `docker compose --profile rag run --rm rag-tools python ingest.py` |
| Force full re-index (Python) | `docker compose --profile rag run --rm rag-tools python ingest.py --force-full` |
| **Start Python HTTP server** | `docker compose --profile rag-python-http up -d rag-python-http`  *(or VS Code task `RAG: Start Python HTTP`)* |
| **Stop Python HTTP server** | `docker compose --profile rag-python-http down` |
| Remote ingest (Python) | `python tools/rag/ingest.py --remote http://localhost:3002` |
| Python unit tests | `pwsh tools/rag/run-tests.ps1 -UnitOnly` |
| Python all tests | `pwsh tools/rag/run-tests.ps1 -StartQdrant` |
| Start Qdrant (.NET) | `docker compose --profile rag-dotnet up qdrant -d` |
| Build .NET image | `docker compose build rag-dotnet` |
| Ingest docs (.NET) | `docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll` |
| **Start .NET HTTP server** | `docker compose --profile rag-dotnet-http up -d rag-dotnet-http`  *(or VS Code task `RAG: Start .NET HTTP`)* |
| **Stop .NET HTTP server** | `docker compose --profile rag-dotnet-http down` |
| Remote ingest (.NET) | `dotnet run --project src/RagTools.Ingest -- --remote http://localhost:3001` |
| **Stop both HTTP servers** | `docker compose --profile rag-dotnet-http --profile rag-python-http down` |
| **Verify HTTP server is up** | `Invoke-WebRequest http://localhost:3001/admin/stats`  *(.NET)*  /  `…:3002…`  *(Python)* |
| **Poll an ingest operation** | `Invoke-RestMethod http://localhost:3002/ingest/<collection>/operations/<opId>` |
| Download .NET ONNX model | `pwsh tools/rag-dotnet/download-model.ps1` |
| .NET unit tests | `pwsh tools/rag-dotnet/run-tests.ps1 -UnitOnly` |
| .NET all tests | `pwsh tools/rag-dotnet/run-tests.ps1 -StartQdrant` |
| Full test suite (Python + .NET) | `pwsh tools/rag/run-all-tests.ps1` |
| Check index (Python) | `Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs` |
| Check index (.NET) | `Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs_dotnet` |
| Qdrant dashboard | http://localhost:6333/dashboard |
