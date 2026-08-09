# ADR-0029 Amendment 1: Host-Side RAG Auto-Cache Hook

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