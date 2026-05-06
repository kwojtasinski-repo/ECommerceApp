---
applyTo: ".github/**, docs/**"
---

# Docs Index — Copilot Routing Table

> **RAG-first**: before reading any file from this index, call `query_docs` or `list_adrs` via the
> `ecommerceapp-rag` MCP server. Load individual files only when RAG returns a specific path to follow.
> Human-oriented docs start at `docs/README.md`. Full routing table -> `docs-index.full.md` (read on demand only).

## When to use RAG vs. read directly

| Situation | Action |
|---|---|
| "Which ADR covers X?" | `list_adrs()` then `get_adr_history(id)` |
| "What does the project say about Y?" | `query_docs("Y")` |
| "How did decision Z evolve?" | `get_adr_history(adr_id)` |
| Known file path already in hand | `read_file` directly, skip RAG |
| Known-issues / agent-decisions / project-state | `query_docs(question, bc="context")` |

## Fixed entry points (load directly, no RAG needed)

| Need | File |
|---|---|
| BC blocked? | `.github/context/project-state.md` |
| Bug already tracked? | `.github/context/known-issues.md` |
| Prior corrections? | `.github/context/agent-decisions.md` |
| Full routing table | `.github/instructions/docs-index.full.md` |
| Pipeline spec | `.github/AGENT-PIPELINE.md` |

## MCP tools

```
query_docs(question, bc?, top_k?)   -- semantic search across docs + .github/context
get_adr_history(adr_id)             -- main ADR + amendments in order
list_adrs()                         -- all ADR ids and titles
```

## Prompt files

| Need | File |
|---|---|
| Analyze a BC structure | `.github/prompts/bc-analysis.prompt.md` |
| Implement a BC slice | `.github/prompts/bc-implementation.prompt.md` |
| Review a PR | `.github/prompts/pr-review.prompt.md` |
| Refactor guidance | `.github/prompts/refactor.prompt.md` |
| **Analyze a user-facing flow (bidirectional)** | `.github/prompts/flow-analysis.prompt.md` |
