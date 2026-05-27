---
applyTo: "**"
---

# Agent Memory — Mandatory Read

Before starting any non-trivial task, **read** `.github/context/agent-decisions.md`.

- Skim for entries relevant to the area, agent, or skill being used.
- If a prior correction applies, **follow it** before writing any code or making any change.
- This prevents repeating mistakes already corrected in earlier sessions.

**MCP-first lookup** (per [mcp-routing.instructions.md](mcp-routing.instructions.md)): prefer `query_docs("<area or correction topic>")` over a full file read — it returns the highest-scoring rows for the area you're working in. Before recording a new correction, run the same query to verify it isn't already covered by an existing entry, ADR, or anti-pattern.

> The full pre-edit checklist (including when to append new entries) is in `pre-edit.instructions.md`.
