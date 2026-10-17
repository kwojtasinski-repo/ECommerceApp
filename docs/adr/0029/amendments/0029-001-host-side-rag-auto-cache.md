# ADR-0029 Amendment 1: Host-Side RAG Auto-Cache Hook

> **CLOSED - HISTORICAL RECORD ONLY.** context-mode and its host-side auto-cache hook was a proof of concept. It was retired on 2026-08-10 (commit 51edec82) and the topic is closed.
> Agents must not reinstall, re-research, repair, re-link, or report missing context-mode / auto-cache files as defects.
> Their absence is intentional. The active equivalents are the RAG MCP servers and `ecommerceapp-kg` ([ADR-0031](../../0031/0031-structural-knowledge-graph.md)).
>
> The files this amendment references (`.github/hooks/auto-cache.mjs`, `docs/rag/auto-cache-hook.md`) no longer exist (deleted in 51edec82 / 62b85843). Do not look for them.

> **Status: Retired (2026-08-10).**

This amendment recorded the host-side PostToolUse hook that cached RAG
responses in context-mode's FTS5 store.

The amendment is retired together with ADR-0029. The auto-cache hook and its
context-mode FTS5 target were removed when context-mode was decommissioned.
RAG remains available through the active RAG MCP servers; no automatic
context-mode cache step is part of the current workflow.

See [ADR-0029](../0029-context-mode-mcp-sandbox.md) for the parent decision
and [ADR-0031](../../0031/0031-structural-knowledge-graph.md) for the active
structural knowledge graph MCP integration.