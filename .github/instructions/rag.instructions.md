---
applyTo: "**"
---

# RAG — when to use the MCP tools

The repo ships an MCP server (`ecommerceapp-rag`) backed by a local Qdrant index over `docs/`.
It exposes 3 tools:

| Tool                                | When to use                                                                                                                                          |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `query_docs(question, bc?, top_k?)` | Free-form semantic search across all docs. Use as a fallback when the routing table in `docs-index.instructions.md` doesn't surface an obvious file. |
| `get_adr_history(adr_id)`           | The user asks how a decision evolved, or you suspect amendments override the main ADR. Returns main + all amendments in chronological order.         |
| `list_adrs()`                       | Orientation queries ("what ADRs exist?", "is there an ADR about X?"). Cheap; safe to call early.                                                     |

## Routing precedence — do NOT replace the existing flow

1. **First** consult `docs-index.instructions.md` — it is the deterministic router.
2. **Then** read the matching ADR / instruction / pattern file with `read_file`.
3. **Use the RAG tools when:**
   - The query is exploratory ("does the app support X?")
   - The query spans multiple ADRs / amendments
   - You're not sure which BC owns the concept
   - The router gives a vague answer

RAG complements the routing table, it does not replace it.

## Output discipline

`query_docs` returns raw retrieved chunks (no LLM synthesis). Treat each hit as a pointer:

- Always cite `rel_path` and the line range in your answer.
- Verify by `read_file` before quoting prescriptive rules — embeddings may surface adjacent prose.
- If `weight < 0.5` and the hit is the top result, suspect noise; consider re-querying with a `bc` filter.

## Refresh policy

The index is built manually:

```
python tools/rag/ingest.py
```

If the user reports stale answers ("the ADR says X but the tool returned Y"), suggest re-running `ingest.py` rather than guessing.
