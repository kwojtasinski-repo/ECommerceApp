---
name: ecommerceapp-kg-query
description: >
  Answer questions about ECommerceApp's structure (modules, entities,
  endpoints, pages, message/query contracts, jobs, RBAC) by querying the
  code-derived knowledge graph via the `ecommerceapp-kg` MCP server. Use
  whenever the user asks about blast radius, module ownership/dependencies,
  action exposure, job scheduling, orphaned contracts, or RBAC coverage —
  instead of grepping the codebase. Not for designing or extending the
  ontology — see kg-ontology-designer for that.
---

# ECommerceApp Knowledge Graph Query Skill (Copilot)

Answer questions about ECommerceApp's real structure by querying the
knowledge graph instead of reading source. The KG models Modules (14 bounded
contexts), Entities, Repositories, Actions, Endpoints, Pages, ScriptModules,
the async Message/MessageHandler and sync Query/QueryHandler contracts, Jobs,
and RBAC (Role/Policy).

**Source of truth for labels/relationships**:
`.claude/skills/ecommerceapp-kg-query/references/labels.md` (owned by the
Claude Code version of this skill — do not fork a local copy here).
**Source of truth for tool contracts**: `docs/reference/kg-mcp-tools.md`.
Design rationale: ADR-0031 and
`docs/architecture/knowledge-graph-ontology-design.md`.

## When to use

- **Neighbors / connections**: "how does X work, what's it connected to" → `GetNodeNeighbors`
- **Blast radius**: "what breaks if I change X" → `GetBlastRadius`
- **Dependencies**: "what does X need to run/load" → `GetNodeDependencies`
- **Module integration**: "which BCs talk to which, what crosses module boundaries" → `GetModuleDependencies`
- **Ownership**: "where's the source of truth for BC X" → `GetModuleOwnership`
- **Exposure**: "does page/endpoint X trigger action Y, where else is Y called from" → `GetActionExposure`
- **Dead contracts**: "who handles message/query X, any orphaned contracts" → `GetOrphanContracts`
- **Job scheduling**: "who schedules job X, what's its trigger mode" → `GetJobSchedulers`
- **RBAC audit**: "which actions/pages require role/policy X" → `GetGovernedActions`
- **Reuse search**: "is there an existing pattern I can reuse for X" → `FindStructurallySimilarActions`

## When not to use

- Effort/time estimates, "which architectural pattern/archetype", test
  coverage, git authorship/history, or runtime behavior (call frequency,
  actual message volume) — explicitly out of scope, see
  `docs/reference/kg-mcp-tools.md`.
- Designing or extending the ontology itself — that is
  `.github/skills/kg-ontology-designer/SKILL.md`.

## Prerequisites

```powershell
docker compose --profile kg up -d neo4j
pwsh tools/kg/load-graph.ps1
```

Without both steps, every tool returns an error envelope naming
`KG_NEO4J_URL` and these two commands.

## How to use

1. **Enable the server** if it isn't already: Chat ▸ MCP tab ▸ toggle on
   `ecommerceapp-kg` (registered in `.vscode/mcp.json`). Unlike the RAG
   variants in the same file, it does not need to be the only server
   enabled — it can run alongside a RAG server. Its ten tools then appear
   directly in Copilot Chat's tool picker; there is no separate schema-load
   step.
2. **Pick the one tool that matches the question** (see table below). These
   are fixed, typed, read-only tools — there is no free-form Cypher escape
   hatch on this server.
3. **`nodeId` is exact and case-sensitive** — usually a fully-qualified type
   or member name (e.g. `ECommerceApp.Application.Orders.OrderService.PlaceOrder`)
   or a module name (e.g. `Payments`). See
   `.claude/skills/ecommerceapp-kg-query/references/labels.md` for valid
   module names and node labels.
4. **An error is not "nothing found."** An id matching no node raises an
   unknown-id error; an id matching several nodes names every match; a node
   of the wrong kind for the tool names what it needs. An **empty result**
   (no error) means the node exists and genuinely has nothing to report —
   safe to read as a real answer.
5. **Verify before recommending action.** The graph is a point-in-time
   snapshot from the last `load-graph.ps1` run. If the user is about to act
   on a KG fact (edit, call, delete) and the working tree may have changed
   since, say so.

## Quick recipes (tool → question)

| Tool | Question it answers | Params |
|---|---|---|
| `GetNodeNeighbors` | "How does X work / what's it connected to?" | `nodeId` |
| `GetBlastRadius` | "What does changing X affect?" | `nodeId`, `maxDepth` (1–5, default 3) |
| `GetNodeDependencies` | "What does X need to run/load?" | `nodeId`, `maxDepth` (1–5, default 3) |
| `GetModuleDependencies` | "Which BCs talk to which?" | `moduleId` |
| `GetModuleOwnership` | "Where's the source of truth for BC X?" | `moduleId` |
| `GetActionExposure` | "Does page/endpoint X trigger action Y? Where else is Y called from?" | `nodeId` (Action or Job) |
| `GetOrphanContracts` | "Who handles message/query X? Any dead contracts?" | none |
| `GetJobSchedulers` | "Who schedules job X, what's its trigger mode?" | `jobId` |
| `GetGovernedActions` | "Which actions/pages require role/policy X?" | `roleOrPolicyId` |
| `FindStructurallySimilarActions` | "Is there an existing pattern I can reuse for X?" | `actionId`, `limit` (1–25, default 10) |

## Direction gotchas (read backwards if you assume the obvious edge)

- **`GetActionExposure`** — an `Action` is exposed via **outgoing**
  `EXPOSED_BY` to `Endpoint`/`Page`; a `Job` is reached via **incoming**
  `SCHEDULES`. Wrong label → explicit error, not an empty list.
- **`GetGovernedActions`** — starts at `Role`/`Policy`. `GOVERNED_BY` only
  exists from `Endpoint`/`Page`, never from `Action` directly; `actionId`
  comes back `null` when a governed surface has no action behind it.
- **`GetModuleDependencies`** — derived through the contracts crossing the
  boundary (`Action→PUBLISHES→Message→HANDLED_BY→MessageHandler`, and the
  `Query` equivalent), not a stored module-to-module edge. Empty means *no
  contract crosses*, not *unrelated*.

## `GetOrphanContracts` — read the confidence, not the row count

| Confidence | Meaning | Safe action |
|---|---|---|
| `high` | Genuine contradiction (message/query with no handler) | Investigate as probable dead contract |
| `contradiction` | Job declares `Deferred` but has no static `SCHEDULES` edge | Investigate — declaration and wiring disagree |
| `ambiguous` | Known false-positive class, graph can't see the caller | **Never delete on this alone** — verify by hand |

## Rules

- **Never invent tool names or params.** There are exactly ten, listed above
  and in `docs/reference/kg-mcp-tools.md`. No raw-Cypher escape hatch exists
  on this server (unlike `neo4j-aj-kb` in the AJ repo).
- **The graph lags the working tree.** New code not yet run through
  `kg-codegen` + `load-graph.ps1` won't be in the graph. When you suspect a
  gap, say so and offer to grep the source instead.
- **Present results as a small markdown table**, not raw JSON — the user
  reads tables faster.
- **Depth is a distance, not a path count.** `GetBlastRadius`/
  `GetNodeDependencies` return one row per node at its shortest distance — a
  node reachable multiple ways is not repeated.

## Relationship to the Claude Code version

`.claude/skills/ecommerceapp-kg-query/SKILL.md` covers the same tools and
recipes for Claude Code sessions. The only real difference is step 1 of "How
to use": Claude Code loads MCP tool schemas on demand via `ToolSearch`
(deferred tools), while Copilot's tools are live directly once
`ecommerceapp-kg` is toggled on in the MCP panel. Node labels and the
relationship vocabulary live once, in
`.claude/skills/ecommerceapp-kg-query/references/labels.md` — extend that
file, not this wrapper, if the ontology changes.
