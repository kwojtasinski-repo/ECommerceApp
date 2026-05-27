---
applyTo: "**"
---

# RAG — when to use the MCP tools

The repo ships an MCP server backed by a local Qdrant index over `docs/`.
In VS Code: enable `ecommerceapp-rag-python` (local Python venv) or `ecommerceapp-rag-dotnet` (local .NET) in the MCP panel.
On GitHub.com Copilot: the server is named `ecommerceapp-rag` (see `.github/copilot/mcp.json`).
It exposes 4 tools:

| Tool                                    | When to use                                                                                                                                                             |
| --------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `list_adrs()`                           | Orientation queries ("what ADRs exist?", "is there an ADR about X?"). Cheap; safe to call early.                                                                        |
| `query_docs(question, bc?, top_k?)`     | Discovery — find which files are relevant. Returns ranked chunks with file paths, line ranges, and scores. Use as a pointer, then follow up with `read_docs`.           |
| `read_docs(question, bc?, top_files?)`  | **Preferred for reasoning.** Returns the full content of the top-ranked unique files. Use when you need complete ADR rationale, conformance checklist, or amendments.   |
| `get_history(id)`                       | The user asks how a specific decision evolved. Returns all indexed chunks for that history group in chronological order (sorted by start_line).                         |

## Recommended flow

```
list_adrs()       → orientation: what exists?
query_docs(q)     → discovery: which files score highest?
read_docs(q)      → depth: full content of those files — reason from the complete document
get_history(id)   → evolution: full amendment chain for a specific ADR
```

Prefer `read_docs` over `query_docs` when you need to quote rules, check conformance, or understand rationale — chunks can miss context across section boundaries.

## Refresh policy

The index is built manually:

```
python tools/rag/ingest.py
```

If the user reports stale answers ("the ADR says X but the tool returned Y"), suggest re-running `ingest.py` rather than guessing.

## RAG maintenance — which skill to use

When the RAG system needs maintenance, load the appropriate skill before making any changes:

| Symptom | Skill to load |
|---------|--------------|
| MCP not starting, tool errors, all scores < 0.25, DLL lock | `diagnose-rag` |
| Correct English query works but PL/DE returns wrong doc | `expand-rag-glossary` |
| Right doc consistently at #3–5 instead of #1 | `tune-rag-weights` |
| New `docs/` folder added or wrong `doc_kind` on a file | `generate-rag-rules` |
| A file has no named eval query covering it | `generate-eval-questions` |
| Full maintenance cycle (ingest + eval + coverage check) | Use `/rag-sync` prompt |

Re-index requirements (quick reference):

| Change | Re-index needed? |
|--------|-----------------|
| `multilingual-glossary.yaml` edited | ❌ Query-time only |
| `rag-config.yaml` ranking weights changed | ❌ Query-time only |
| `queries.yaml` edited | ❌ Not used at ingest |
| Any `docs/` or `.github/context/` file changed | ✅ Incremental (`ingest.py`) |
| `metadata-rules.yaml` changed | ✅ Force-full (`ingest.py --force-full`) |
| `embedder.model` or `chunker.*` changed | ✅ Force-full |

---

## context-mode — when to use the `ctx_*` tools

> Status: applies once the `context-mode` MCP is registered and its hooks are wired (see [ADR-0029](../../docs/adr/0029/0029-context-mode-mcp-sandbox.md) and the [context-mode roadmap](../../docs/roadmap/context-mode-integration.md)). Until then, only the RAG table above is live.

| Tool                          | When to use                                                                                          |
| ----------------------------- | ---------------------------------------------------------------------------------------------------- |
| `ctx_stats()`                 | "how much context have we saved this session?" / sanity check                                        |
| `ctx_execute(lang, code)`     | One-shot sandboxed execution — math, parsing, regex on a small string                                |
| `ctx_execute_file(path)`      | Run analysis over a file and return only the **summary** (not the body)                              |
| `ctx_fetch_and_index(url)`    | Pull an allowlisted external URL through AdGuard and add it to context                               |
| `ctx_insight()`               | Open the local web UI to inspect what's currently in context                                         |

## MCP precedence (HARD RULES)

When more than one MCP could plausibly answer a request, apply this
order strictly:

1. **Knowledge questions** (anything that quotes docs, ADRs, BC rules,
   project state, known issues, roadmap) → **RAG first**. Never
   substitute `ctx_execute` for it.
2. **Sandboxed execution** (run a snippet, compute a value, summarise a
   file's structure without loading its content) → **context-mode
   first**. Never substitute `read_file` + manual summary for it when
   the body is large.
3. **External fetch** (any URL the user pastes) → **`ctx_fetch_and_index`
   only**. Never raw `fetch_webpage` for project work — it bypasses the
   AdGuard allowlist.
4. **Both MCPs empty / unhealthy** → fall back to direct `read_file` /
   `grep_search` and tell the user which MCP failed.
5. **NEVER call both MCPs for the same atomic intent.** If unsure, pick
   the one whose table matches the user's verb ("what does ADR-X say"
   → RAG; "run this snippet" → context-mode).

Full rationale, ASCII flow diagram, hook plug-in points, common wiring
failures, and verification prompts:
[docs/rag/mcp-first-routing-migration-playbook.md §13](../../docs/rag/mcp-first-routing-migration-playbook.md#13-coexistence-with-a-second-mcp-server-worked-example-context-mode).
