---
applyTo: "**"
---

# RAG — when to use the MCP tools

The repo ships an MCP server (`ecommerceapp-rag`) backed by a local Qdrant index over `docs/`.
It exposes 4 tools:

| Tool                                    | When to use                                                                                                                                                             |
| --------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `list_adrs()`                           | Orientation queries ("what ADRs exist?", "is there an ADR about X?"). Cheap; safe to call early.                                                                        |
| `query_docs(question, bc?, top_k?)`     | Discovery — find which files are relevant. Returns ranked chunks with file paths, line ranges, and scores. Use as a pointer, then follow up with `read_docs`.           |
| `read_docs(question, bc?, top_files?)`  | **Preferred for reasoning.** Returns the full content of the top-ranked unique files. Use when you need complete ADR rationale, conformance checklist, or amendments.   |
| `get_adr_history(adr_id)`               | The user asks how a specific decision evolved. Returns the main ADR + all amendments in chronological order.                                                           |

## Recommended flow

```
list_adrs()          → orientation: what exists?
query_docs(q)        → discovery: which files score highest?
read_docs(q)         → depth: full content of those files — reason from the complete document
get_adr_history(id)  → evolution: full amendment chain for a specific ADR
```

Prefer `read_docs` over `query_docs` when you need to quote rules, check conformance, or understand rationale — chunks can miss context across section boundaries.

## Refresh policy

The index is built manually:

```
python tools/rag/ingest.py
```

If the user reports stale answers ("the ADR says X but the tool returned Y"), suggest re-running `ingest.py` rather than guessing.
