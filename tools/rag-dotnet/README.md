# RAG Tools (.NET) — Setup & Usage Guide

.NET 10 implementation of the RAG pipeline — same semantic search as the Python version
but using ONNX Runtime for embeddings and a compiled MCP server.

Three MCP tools exposed to Copilot Chat:

- `query_docs` — free-form semantic search
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
> 1. Python stage — exports `paraphrase-multilingual-MiniLM-L12-v2` to ONNX format using `optimum`.
>    This bakes the model into the image so the .NET runtime never needs Python.
> 2. .NET SDK stage — restores packages, publishes `RagTools.Ingest` and `RagTools.Mcp`.
> 3. Runtime stage — combines the ONNX model and .NET binaries into a lean final image.
>
> First build takes 5–10 minutes (model export + NuGet restore). Subsequent builds use Docker cache.

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

> **What this does:** runs 94 unit tests covering token counting, chunking, manifest
> change detection, ONNX embedder internals, and config deserialization. Tests that
> require a real `vocab.txt` are guarded with `[SkippableFact]` and skipped automatically.

Expected output:

```
Success!  — Failed: 0, Passed: 94, Skipped: 0, Total: 94
```

---

## Local development (no Docker)

You need:

- .NET 10 SDK (`dotnet --version` should show `10.x`)
- Qdrant running locally (Docker or binary)
- ONNX model exported to a local directory

### Export the ONNX model locally

```bash
pip install optimum[onnxruntime] sentence-transformers
optimum-cli export onnx \
  --model sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2 \
  --task sentence-similarity \
  --opset 14 \
  model/
```

> This exports the model to `tools/rag-dotnet/model/`. Point `RAG_MODEL_DIR` at this directory.

### Run ingest directly

```bash
cd tools/rag-dotnet

# Set env vars (PowerShell)
$env:RAG_WORKSPACE   = "C:\path\to\repo"
$env:RAG_MODEL_DIR   = "model"
$env:QDRANT_URL      = "http://localhost:6333"
$env:RAG_CONFIG      = "..\rag\config.yaml"

dotnet run --project src/RagTools.Ingest -- --dry-run
dotnet run --project src/RagTools.Ingest
```

### Run MCP server directly

```bash
dotnet run --project src/RagTools.Mcp
```

---

## Environment variables

| Variable         | Default                    | Effect                                    |
| ---------------- | -------------------------- | ----------------------------------------- |
| `RAG_WORKSPACE`  | current directory          | Absolute path to the repo root            |
| `RAG_MODEL_DIR`  | `<binary dir>/model`       | Path to the exported ONNX model directory |
| `RAG_CONFIG`     | `<binary dir>/config.yaml` | Path to `config.yaml`                     |
| `QDRANT_URL`     | value from config.yaml     | Qdrant HTTP URL (`http://host:6333`)      |
| `RAG_COLLECTION` | value from config.yaml     | Qdrant collection name override           |

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
RagTools.Mcp           — stdio MCP server: query_docs, get_adr_history, list_adrs
RagTools.Tests         — 94 unit tests (no external services required)
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
Run the ONNX export step or build the Docker image which includes the model.

**MCP tools not showing in Copilot**  
→ Restart VS Code after the first ingest. Check `.github/copilot/mcp.json` contains
the correct command pointing to the `rag-dotnet` container.

**Build fails with file-lock error on `*.CoreCompileInputs.cache`**  
→ VS Code's C# extension locks build artifacts. Run `dotnet test --no-build` after a
prior successful `dotnet build`. This is a known VS Code issue, not a project bug.
