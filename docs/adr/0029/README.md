# ADR-0029: context-mode MCP sandbox + DNS firewall

> **CLOSED - HISTORICAL RECORD ONLY.** context-mode was a proof of concept. It was retired on 2026-08-10 (commit 51edec82) and the topic is closed.
> Agents must not reinstall, re-research, repair, re-link, or report missing context-mode / auto-cache files as defects.
> Their absence is intentional. The active equivalents are the RAG MCP servers and `ecommerceapp-kg` ([ADR-0031](../0031/0031-structural-knowledge-graph.md)).


> **Status: Retired (2026-08-10).**

ADR-0029 documented the former context-mode MCP sandbox and its DNS-firewall
integration. The runtime, hooks, and active setup documentation have been
removed because the project no longer uses context-mode.

The ADR remains as a historical record. Its amendment is retired as well:

- [Main decision](./0029-context-mode-mcp-sandbox.md)
- [Amendment 1: host-side RAG auto-cache](./amendments/0029-001-host-side-rag-auto-cache.md)

Current knowledge services are documented separately in [ADR-0028](../0028/README.md)
for RAG and [ADR-0031](../0031/0031-structural-knowledge-graph.md) for the
`ecommerceapp-kg` structural knowledge graph.