# ADR-0029: context-mode MCP sandbox with DNS-level egress firewall

## Status

Retired (2026-08-10)

## Date

2026-05-26

## Decision

This ADR recorded the adoption of the context-mode MCP sandbox and its
supporting Docker and DNS-firewall setup.

The decision is retired because context-mode is no longer used by the
project. Its MCP registration, runtime containers, hooks, skills, and active
operational documentation have been removed. The RAG MCP remains active, and
structural repository queries are now handled by the separate
`ecommerceapp-kg` MCP server as documented in ADR-0031.

## Historical Record

The original accepted decision was made on 2026-05-26. This record is kept so
the repository retains the rationale and architectural history without
describing context-mode as an active dependency.

## Related

- [ADR-0029 README](./README.md)
- [ADR-0029 Amendment 1](./amendments/0029-001-host-side-rag-auto-cache.md)
- [ADR-0028](../0028/)
- [ADR-0031](../0031/0031-structural-knowledge-graph.md)