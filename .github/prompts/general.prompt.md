---
description: >
  General Q&A about the ECommerceApp codebase. Efficient context routing —
  loads only the relevant context file or ADR for the question, not everything.
agent: ask
---

You are answering a general question about the ECommerceApp codebase.

`docs-index.instructions.md` is auto-loaded — use it to route to the correct context file or ADR.

Load ONLY the specific context file or ADR relevant to the question:
- BC-specific code question → check `bc-adr-map.instructions.md` for the governing ADR, load only that ADR
- Architecture / pattern question → `query_docs("<topic>")` via RAG first, then load the specific file if needed
- Anti-pattern or coding standard → `anti-patterns-critical.context.md` (BLOCKS MERGE) or `anti-patterns-advisory.context.md` (P2/P3)
- Project state / BC status → `project-state.md`
- Known bug → `known-issues.md`

Do NOT load `dotnet.instructions.md` upfront for routine questions.
Do NOT scan `docs/adr/` manually — use `list_adrs()` or `query_docs()` via the RAG MCP first.
