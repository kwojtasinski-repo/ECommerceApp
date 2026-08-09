---
applyTo: "**"
---

# MCP routing

> Routing for the repository's active MCP servers. Keep this file short and stable.

## Top rules

- Knowledge from repository docs, ADRs, project state, or roadmaps -> RAG first.
- Structural questions about code ownership, dependencies, handlers, schedulers, or blast radius -> KG first.
- Local code, logs, analysis, transformations, and execution -> bounded direct tools.
- Project-related URLs -> use the approved project URL retrieval path.
- Do not mix unrelated MCP families for one atomic lookup.
- Infer the route from the task; the user does not need to name a tool.

## MCP ownership

| Intent | Primary source | Core tools |
|---|---|---|
| Repository knowledge | RAG | `list_adrs`, `query_docs`, `read_docs`, `get_history` |
| Structural code graph | KG | `GetNodeNeighbors`, `GetBlastRadius`, `GetNodeDependencies`, `GetModuleDependencies`, `GetModuleOwnership`, `GetActionExposure`, `GetOrphanContracts`, `GetJobSchedulers`, `GetGovernedActions`, `FindStructurallySimilarActions` |
| Local code and execution | Direct tools | bounded search, file reads, focused commands |

RAG servers: `ecommerceapp-rag-python`, `ecommerceapp-rag-dotnet`, or `ecommerceapp-rag`.

The structural graph server is `ecommerceapp-kg`.

## RAG rules

Use RAG for:
- ADRs and architectural decisions.
- `.github/context/*.md` knowledge.
- Roadmaps and bounded-context maps.

When a RAG lookup is empty or low-signal, retry without filters and with a reworded domain query before using a bounded direct-file fallback. State the fallback in the result.

## KG rules

KG ids are exact and case-sensitive. An empty result means the node exists and has no reported relationships; an error means the id or server request is invalid. The graph is static and may be stale after code changes, so do not infer runtime behavior or test coverage from it.

## Implementation rules

For implementation work, inspect the smallest relevant files first, make the smallest exact patch, and run a focused executable validation immediately after the first edit. Use direct tools for local code and command execution; use RAG or KG only when their specific knowledge or structural evidence is needed.
