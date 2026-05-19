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

| | Python | .NET |
|---|---|---|
| **Status** | ✅ Production-ready | ⚠️ Experimental |
| **Recommended for** | All developers | .NET-primary / advanced |
| **Prerequisites** | Docker (+ Python 3.13 for local dev) | Docker + .NET 10 SDK |
| **Model accuracy** | ✅ Full (correct tokenizer) | ⚠️ Reduced (WordPiece workaround) |
| **Polish query support** | ✅ Yes | ⚠️ Reduced |
| **Already indexed?** | ✅ 816 chunks in Qdrant | ❌ Must run ingest |

**Use Python unless you have a specific reason to use .NET.**

---

## Python implementation

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows/Mac/Linux)
- VS Code with the **GitHub Copilot** extension
- That's it — no Python install needed for the Docker path

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
> - Prefix with "get_adr_history for ADR-0016" to get the full text of a specific ADR
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

The active model is set in [`tools/rag/config.yaml`](../../tools/rag/config.yaml)
under `embedder.model`. Both implementations share this file.

> **Warning:** changing the model requires a full re-index (`--force-full`).
> Old vectors are incompatible with new model embeddings.
> The `config.yaml` `version` field is automatically checked — increment it
> to force a full re-index on all machines.

### Python — change model

Edit `tools/rag/config.yaml`:

```yaml
embedder:
  model: "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"  # current (multilingual)
  # model: "sentence-transformers/all-MiniLM-L6-v2"    # English-only, smaller, no workaround needed
  dimensions: 384   # must match the new model's output size
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

## Quick reference

| Task | Command |
|------|---------|
| Start Qdrant (Python) | `docker compose --profile rag up qdrant -d` |
| Build Python image | `docker compose build rag-tools` |
| Ingest docs (Python) | `docker compose --profile rag run --rm rag-tools python ingest.py` |
| Force full re-index (Python) | `docker compose --profile rag run --rm rag-tools python ingest.py --force-full` |
| Start Qdrant (.NET) | `docker compose --profile rag-dotnet up qdrant -d` |
| Build .NET image | `docker compose build rag-dotnet` |
| Ingest docs (.NET) | `docker compose --profile rag-dotnet run --rm rag-dotnet dotnet /app/ingest/ingest.dll` |
| Download .NET ONNX model | `pwsh tools/rag-dotnet/download-model.ps1` |
| Check index (Python) | `Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs` |
| Check index (.NET) | `Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs_dotnet` |
| Qdrant dashboard | http://localhost:6333/dashboard |
