# RAG Tools (.NET) — Setup & Usage Guide

.NET 10 implementation of the RAG pipeline — same semantic search as the Python version
but using ONNX Runtime for embeddings and a compiled MCP server.

Four MCP tools exposed to Copilot Chat:

- `query_docs` — free-form semantic search (top chunks ranked)
- `read_docs` — top-ranked **unique files** (chunk view by default; full-content mode when the question contains intent phrases like "all details" / "whole file")
- `list_adrs` — list all indexed ADRs
- `get_adr_history` — fetch all chunks for a specific ADR

> **Before you start:** this path requires a running Qdrant server (HTTP + gRPC).
> The Python path (`tools/rag`) uses embedded Qdrant and is simpler for local use.

---

## Quick start (Docker, recommended)

### 1. Build the Docker image

```bash
# From the repo root
docker compose build rag-dotnet
```

> **What this does:** runs a multi-stage Docker build:
>
> 1. `curlimages/curl` stage — downloads the **pre-exported ONNX bundle** straight from HuggingFace
>    (`/onnx/model.onnx` + `vocab.txt` + `tokenizer.json` + `config.json`).
>    No Python, no `optimum-cli`, no venv — same bytes the maintainers publish.
> 2. .NET SDK stage — restores packages, publishes `RagTools.Ingest` and `RagTools.Mcp`.
> 3. Runtime stage — combines the ONNX model and .NET binaries into a lean final image.
>
> First build takes ~2–4 minutes (model download + NuGet restore). Subsequent builds use Docker cache.

---

### 2. Start Qdrant

```bash
docker compose --profile rag-dotnet up qdrant -d
```

> **What this does:** starts a Qdrant container at `localhost:6333` (HTTP) and `localhost:6334`
> (gRPC). The .NET client uses gRPC. Data is persisted to the `qdrant_data` Docker volume
> so the index survives container restarts.

---

### 3. Run the ingest pipeline

```bash
# From the repo root
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll
```

> **What this does:** scans all Markdown files under `docs/` and `.github/context/`,
> splits them into chunks (heading-based, 800-token max), generates embeddings using the
> ONNX model, and upserts them into Qdrant. The manifest is written to `.rag/manifest.json`
> so subsequent runs are incremental.

**Useful flags:**

| Flag              | Effect                                                       |
| ----------------- | ------------------------------------------------------------ |
| `-- --dry-run`    | Scan and chunk only — prints stats, no embeddings or upserts |
| `-- --force-full` | Re-index everything even if files are unchanged              |

```bash
# Example: dry-run
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll -- --dry-run
```

---

### 4. Verify the index

Check Qdrant dashboard at http://localhost:6333/dashboard — you should see a collection
named `ecommerceapp_docs_dotnet` with points.

Or run a quick query using curl:

```bash
curl -s http://localhost:6333/collections/ecommerceapp_docs_dotnet | python -m json.tool
```

> A `"status": "green"` response with a non-zero `vectors_count` confirms the index is ready.

---

### 5. Run the MCP server

```bash
# VS Code starts this automatically via .github/copilot/mcp.json.
# To test manually:
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/mcp/mcp_server.dll
```

> **What this does:** starts a stdio MCP server. VS Code Copilot spawns this process
> automatically when you open a chat and reads `.github/copilot/mcp.json` for the command.
> You do not need to run this manually — just restart VS Code after the first ingest.

To confirm Copilot sees the tools, open Copilot Chat and type:

```
@ecommerceapp-rag list_adrs()
```

---

## Running tests

No Docker or Qdrant needed for the unit tests:

```bash
cd tools/rag-dotnet

# Build once
dotnet build src/RagTools.Tests/RagTools.Tests.csproj -q

# Run (use --no-build to avoid VS Code file-lock issues)
dotnet test src/RagTools.Tests/RagTools.Tests.csproj --no-build -q
```

> **What this does:** runs all unit tests covering token counting, chunking, manifest
> change detection, ONNX embedder internals, config deserialization, and the 4-way
> config / workspace resolution. Tests that require a real `vocab.txt` are guarded
> with `[SkippableFact]` and skipped automatically when the model isn't downloaded.

Expected output:

```
Success!  — Failed: 0, Passed: 100, Skipped: 0, Total: 100
```

---

## Local development (no Docker)

You need:

- .NET 10 SDK (`dotnet --version` should show `10.x`)
- Qdrant running locally (`docker compose up qdrant -d`)
- ONNX model downloaded once via the included PowerShell script — **no Python required**

### One-time: download the ONNX model

```powershell
pwsh tools/rag-dotnet/download-model.ps1
```

> Downloads the pre-exported ONNX bundle directly from HuggingFace into
> `tools/rag-dotnet/model/` (gitignored). Idempotent — skips files already present.
> Total size: ~490 MB on disk.

### Run from Visual Studio / VS Code (F5)

Both projects ship `Properties/launchSettings.json` profiles pre-configured for the
repo layout. Set the startup project to **RagTools.Ingest** or **RagTools.Mcp** and
press **F5** — env vars (`RAG_WORKSPACE`, `RAG_CONFIG`, `RAG_MODEL_DIR`, `QDRANT_URL`)
are wired automatically and `workingDirectory` points at the repo root.

### Run from CLI

```powershell
cd tools/rag-dotnet

# Ingest (incremental — re-embeds only changed files)
dotnet run --project src/RagTools.Ingest

# Ingest dry-run (no embeddings, no upserts)
dotnet run --project src/RagTools.Ingest -- --dry-run

# MCP server (stdio — usually launched by VS Code, but runs standalone for testing)
dotnet run --project src/RagTools.Mcp
```

> The startup banner prints the resolved config path + size, workspace, collection,
> Qdrant URL, and model dir. **If config or model are missing, the process exits with
> code 1 and a diagnostic message** — no silent fallback to wrong defaults.

---

## Environment variables

| Variable         | Default                    | Effect                                    |
| ---------------- | -------------------------- | ----------------------------------------- |
| `RAG_WORKSPACE`  | derived from config path   | Absolute path to the repo root            |
| `RAG_MODEL_DIR`  | `<binary dir>/model`       | Path to the downloaded ONNX model         |
| `RAG_CONFIG`     | see resolution below       | Path to `config.yaml`                     |
| `QDRANT_URL`     | value from config.yaml     | Qdrant HTTP URL (`http://host:6333`)      |
| `RAG_COLLECTION` | value from config.yaml     | Qdrant collection name override           |

### Config-path resolution (4-way priority — Python parity)

1. Explicit `--config <path>` argument (if/when CLI exposes one)
2. `RAG_CONFIG` env var
3. `RAG_WORKSPACE` env var → derived path `<workspace>/tools/rag/config.yaml`
4. `AppContext.BaseDirectory/config.yaml` (Docker bundle / published output)

### Workspace-path resolution (3-way priority)

1. Grandparent of the loaded config path — `<ws>/tools/rag/config.yaml` → `<ws>`
2. `RAG_WORKSPACE` env var
3. `Directory.GetCurrentDirectory()`

> The `Workspace` instance property prevents shell env from leaking into runs that
> pass an explicit config path. Tests cover all priority paths.

---

## Re-indexing after docs change

```bash
# Incremental (default) — only re-embeds changed files
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll

# Force full rebuild
docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll -- --force-full
```

> Use `--force-full` after changing `config.yaml` chunker settings, switching embedding
> models, or modifying `metadata-rules.yaml`. Existing vectors are incompatible
> with different model outputs.

---

## Configuration

Settings are read from `tools/rag/config.yaml` (shared between Python and .NET).
The .NET runtime loads this file at startup via the `RAG_CONFIG` env var.

Key fields that affect the .NET path:

| Field                     | Default              | Notes                                              |
| ------------------------- | -------------------- | -------------------------------------------------- |
| `embedder.dimensions`     | `384`                | Must match the ONNX model output size              |
| `vector_store.collection` | `ecommerceapp_docs`  | Overridden in Docker to `ecommerceapp_docs_dotnet` |
| `chunker.max_tokens`      | `800`                | Hard upper bound per chunk                         |
| `storage.manifest_path`   | `.rag/manifest.json` | Relative to `RAG_WORKSPACE`                        |

---

## Architecture

```
RagTools.Core          — BertTokenCounter, OnnxEmbedder, MarkdownChunker,
                         ManifestService, QdrantStore, RagConfig
RagTools.Ingest        — CLI: scan → chunk → embed → upsert
RagTools.Mcp           — stdio MCP server: query_docs, read_docs,
                         get_adr_history, list_adrs
RagTools.Tests         — 100 unit tests (no external services required)
```

The MCP server mirrors the Python `mcp_server.py` tools exactly — both implementations
read the same `config.yaml` and target the same Qdrant schema, so you can switch between
them without re-indexing (as long as they use the same collection name).

---

## Troubleshooting

**`Collection not found` error on first ingest**  
→ The ingest creates the collection automatically. Make sure Qdrant is running (step 2).

**`Unable to connect to Qdrant` / gRPC error**  
→ Check that Qdrant is running on port 6334 (gRPC). The .NET client uses gRPC, not HTTP.

**`Model file not found` error**  
→ `RAG_MODEL_DIR` does not point to a directory containing `model.onnx` and `tokenizer.json`.
Run `pwsh tools/rag-dotnet/download-model.ps1` or build the Docker image which includes the model.

**MCP tools not showing in Copilot**  
→ Restart VS Code after the first ingest. Check `.github/copilot/mcp.json` contains
the correct command pointing to the `rag-dotnet` container.

**Build fails with file-lock error on `*.CoreCompileInputs.cache`**  
→ VS Code's C# extension locks build artifacts. Run `dotnet test --no-build` after a
prior successful `dotnet build`. This is a known VS Code issue, not a project bug.
