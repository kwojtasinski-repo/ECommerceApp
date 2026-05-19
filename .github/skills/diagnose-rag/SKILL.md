---
name: diagnose-rag
description: >
  Diagnose why the RAG MCP server returns bad results, fails to start, returns
  errors, or produces low-quality rankings. Covers both the Python and .NET
  server implementations. Run this skill before making any changes.
argument-hint: "[symptom: bad-results | not-starting | wrong-language | low-scores | errors]"
---

# Diagnose RAG MCP Issues

Follow the decision tree below to identify the root cause before changing anything.
Most issues fall into one of five categories.

---

## Quick triage — what is the symptom?

| Symptom | Jump to |
|---------|---------|
| MCP server not starting / no tools shown in VS Code | § 1 |
| Tools visible but every call returns an error | § 2 |
| Results returned but wrong document at #1 | § 3 |
| Results correct in English but not in Polish/German | § 4 |
| Scores are all very low (< 0.25) | § 5 |
| .NET server builds fail / DLL lock | § 6 |
| Index seems stale (old content returned) | § 7 |

---

## § 1 — MCP server not starting

### Check VS Code MCP status

1. Open Copilot Chat → **Tools** button → **MCP** section
2. Is the server listed? Is it enabled?
3. If listed with an error icon, hover to see the error message

### Check server process

```powershell
# Python server
Get-Process python | Where-Object { $_.CommandLine -like "*mcp_server*" }

# .NET server
Get-Process | Where-Object { $_.Name -like "*RagTools*" }
```

### Python — common startup failures

| Error | Cause | Fix |
|-------|-------|-----|
| `ModuleNotFoundError: sentence_transformers` | venv not activated or wrong Python | Check `.vscode/mcp.json` — python path must point to `.venv/Scripts/python` |
| `FileNotFoundError: config.yaml` | Wrong working directory | Set `cwd` in `.vscode/mcp.json` to the repo root |
| `ConnectionRefusedError: localhost:6333` | Qdrant not running | `docker compose --profile rag up qdrant -d` |
| `No collection 'ecommerceapp_docs'` | Index never built | `docker compose --profile rag run --rm rag-tools python ingest.py` |

### .NET — common startup failures

| Error | Cause | Fix |
|-------|-------|-----|
| `The model directory does not contain tokenizer.json` | ONNX model not downloaded | Run `pwsh tools/rag-dotnet/download-model.ps1` |
| `Unable to connect to gRPC endpoint localhost:6334` | Qdrant gRPC port not mapped | `docker compose --profile rag-dotnet up qdrant -d` and confirm port 6334 is open |
| `System.IO.FileNotFoundException: *.dll` | DLLs locked or build output missing | Stop MCP server → `dotnet build RagTools.sln` → restart MCP server |

### Verify Qdrant is healthy

```powershell
Invoke-RestMethod http://localhost:6333/collections/ecommerceapp_docs | ConvertTo-Json
# Expect: "status": "green" and non-zero "points_count"
```

---

## § 2 — Tools visible but every call errors

### Check if the collection exists and has data

```powershell
Invoke-RestMethod http://localhost:6333/collections | ConvertTo-Json -Depth 3
# Look for "ecommerceapp_docs" in the list with points_count > 0
```

If the collection is missing or empty → run ingest:
```powershell
docker compose --profile rag run --rm rag-tools python ingest.py
```

### Python — `KeyError: 'hits'` or `TypeError: QueryHit() takes no arguments`

- **Root cause**: usually a bug in `query.py` (e.g., `@dataclass` decorator missing from `QueryHit`)
- Check `tools/rag/query.py` — `QueryHit` class must have `@dataclass` decorator
- Restart the MCP server after fixing (kill the python process; VS Code will restart it)

### Python — tool timeout after 60 s on first call

- **Root cause**: model loading is lazy; first embedding takes 30–90 s
- This is normal on cold start; subsequent calls are fast
- Fix: wait 90 s and retry, OR set `device: cuda` if a GPU is available

### .NET — `Grpc.Core.RpcException: Status(StatusCode="Unavailable")`

- Qdrant gRPC endpoint (port 6334) unreachable
- Verify: `docker ps` — confirm qdrant container is running
- Check docker-compose port mapping: `6334:6334` must be present in the qdrant service

---

## § 3 — Results returned but wrong document at #1

### Step A — Check if the right document is indexed at all

```
query_docs("<concept>", top_k=20)
```

Scan the full list of 20 results. Is the expected file anywhere in the list?

- **Not in top-20**: the doc is not indexed → re-run ingest, check `source.roots` and `exclude_globs` in `config.yaml`
- **In top-10 but not #1**: weight or threshold issue → see `tune-rag-weights` skill

### Step B — Check the score gap

If the wrong document scores 0.65 and the right one scores 0.62, the gap is too small for
weight tuning alone. Possible causes:

- Query is ambiguous — try a more specific query with domain terms (e.g., `CouponsOptions`, `KI-008`)
- The right document lacks the specific terms → add them to the doc (rare) or improve the query
- Language gap → step C

### Step C — Test the English equivalent

Run the same query in English. If the English version returns the right document at #1:
→ Language gap → use `expand-rag-glossary` skill

If English also fails:
→ Weight, doc content, or index problem → use `tune-rag-weights` skill or re-index

---

## § 4 — Results correct in English but wrong in Polish/German

This is a **language gap** — the glossary does not cover this concept in the failing language.

### Confirm with expansion test

```python
# Quick test — see if the query expands at all
import sys; sys.path.insert(0, "tools/rag")
from query import _expand_query
import yaml

with open("tools/rag/multilingual-glossary.yaml", encoding="utf-8") as f:
    raw = yaml.safe_load(f)
entries = [(e["english"], e["patterns"]) for e in raw["entries"]]

print(_expand_query("twój zapytanie po polsku", entries, repeat=3))
```

- If output == input (no expansion): no glossary entry matched → add one (use `expand-rag-glossary` skill)
- If output has expansion but still wrong: the expansion anchor is incorrect → check what English terms were appended vs. what the target doc contains

### Common language-specific gaps

| Pattern missing | German compound | Fix |
|----------------|-----------------|-----|
| Compound German nouns (`Entitäts-Bezeichner`) | The prefix `entitäts` and noun `bezeichner` need separate pattern entries | Add both to glossary |
| Polish genitive forms (`zamówień`, `kuponów`) | Not the same as nominative | Add the genitive form as a separate pattern |
| Technical abbreviations (`BC`, `DI`, `CQRS`) | Same in all languages — do NOT add to glossary | These already work as-is |

---

## § 5 — All scores are very low (< 0.25)

### Check the threshold setting

```yaml
# tools/rag/config.yaml
ranking:
  score_threshold: 0.30    # Results below this are dropped
```

If threshold is 0.30 and all raw scores are 0.18–0.22, either:
- The query is completely off-topic for the indexed content
- The embedding model is mismatched (query vs. index used different models)

### Check model consistency

The ingest model and the query-time model MUST be identical:

```yaml
embedder:
  model: "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
  dimensions: 384
```

If you changed the model in config.yaml after the index was built → force-full re-index:

```powershell
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

### .NET — lower scores are normal

The .NET server uses a WordPiece (BERT) tokenizer instead of SentencePiece (XLM-RoBERTa).
This produces semantically different embeddings — average similarity scores are ~0.35–0.55
vs. ~0.55–0.75 for Python. The ranking order is still correct; only the absolute values differ.

Do not lower the threshold to compensate — instead check whether the **ranking order** is right.

---

## § 6 — .NET server build fails / DLL locked

**Root cause**: VS Code holds the .NET MCP server process alive while it is enabled.
`dotnet build` tries to overwrite the DLLs that the running process has open → Access Denied.

**Fix sequence:**
1. Open VS Code Copilot Chat → **Tools** → **MCP** → disable the .NET MCP server
2. Wait for the process to terminate: `Get-Process | Where-Object { $_.Name -like "*RagTools*" }`
3. Run the build: `dotnet build tools/rag-dotnet/RagTools.sln`
4. Re-enable the .NET MCP server in VS Code

**Never** run `dotnet build` while the .NET MCP server is active — always stop it first.

---

## § 7 — Index is stale (old content returned)

### Check when the index was last built

```powershell
# manifest.json records the last ingest timestamp
Get-Content tools/rag/.rag/manifest.json | ConvertFrom-Json | Select-Object last_ingest, version
```

### Incremental re-index (fast — only changed files)

```powershell
docker compose --profile rag run --rm rag-tools python ingest.py
```

### Force full re-index (slow — required after model or metadata-rules changes)

```powershell
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

### When is force-full required?

| Change | Incremental sufficient? |
|--------|------------------------|
| New/edited markdown file | ✅ Yes |
| Deleted markdown file | ✅ Yes |
| `config.yaml` ranking.weights changed | ✅ Yes (query-time only) |
| `multilingual-glossary.yaml` changed | ✅ Yes (query-time only) |
| `queries.yaml` changed | ✅ Yes (not used during ingest) |
| `metadata-rules.yaml` changed | ❌ No — force-full required |
| `embedder.model` changed | ❌ No — force-full required |
| `chunker.*` settings changed | ❌ No — force-full required |

---

## Checklist before escalating

Before concluding the RAG system is fundamentally broken, verify:

- [ ] Qdrant container is running and collection is green (`/collections/ecommerceapp_docs`)
- [ ] Collection has points: `points_count > 0`
- [ ] Python MCP venv path in `.vscode/mcp.json` points to `.venv/Scripts/python`
- [ ] The failing query works in English → if yes, use `expand-rag-glossary`
- [ ] The expected document is in the index: `query_docs(English query, top_k=20)` includes it
- [ ] Index is fresh: incremental ingest was run after last doc change
- [ ] No pending `metadata-rules.yaml` changes that need force-full reindex
