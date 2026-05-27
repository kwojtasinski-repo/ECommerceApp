---
applyTo: "**"
---

# RAG — maintenance & operations

> **Tool routing, precedence, and the MCP flow diagram live in [mcp-routing.instructions.md](mcp-routing.instructions.md) — the single source of truth.** This file covers RAG-specific operations only: how to re-index, refresh policy, and which maintenance skill to load for which symptom.

For *when to call which tool*, see `mcp-routing.instructions.md`. For *how the index is built and how to fix it*, keep reading.

## Refresh policy

The index is built manually:

```
python tools/rag/ingest.py
```

If the user reports stale answers ("the ADR says X but the tool returned Y"), suggest re-running `ingest.py` rather than guessing.

## Re-index requirements

| Change | Re-index needed? |
|--------|-----------------|
| `multilingual-glossary.yaml` edited | ❌ Query-time only |
| `rag-config.yaml` ranking weights changed | ❌ Query-time only |
| `queries.yaml` edited | ❌ Not used at ingest |
| Any `docs/` or `.github/context/` file changed | ✅ Incremental (`ingest.py`) |
| `metadata-rules.yaml` changed | ✅ Force-full (`ingest.py --force-full`) |
| `embedder.model` or `chunker.*` changed | ✅ Force-full |

## Which maintenance skill to load

| Symptom | Skill |
|---------|--------------|
| MCP not starting, tool errors, all scores < 0.25, DLL lock | `diagnose-rag` |
| Correct English query works but PL/DE returns wrong doc | `expand-rag-glossary` |
| Right doc consistently at #3–5 instead of #1 | `tune-rag-weights` |
| New `docs/` folder added or wrong `doc_kind` on a file | `generate-rag-rules` |
| A file has no named eval query covering it | `generate-eval-questions` |
| Full maintenance cycle (ingest + eval + coverage check) | `/rag-sync` prompt |

## Server variants

- VS Code: `ecommerceapp-rag-python` (local Python venv) or `ecommerceapp-rag-dotnet` (local .NET) in the MCP panel.
- GitHub.com Copilot: `ecommerceapp-rag` (see [.github/copilot/mcp.json](../copilot/mcp.json)).
- Tool names are identical across all variants.
