# KG MCP tools (`ecommerceapp-kg`)

`ecommerceapp-kg` is a **read-only, stdio-only** MCP server over the Neo4j projection that
`kg-codegen` generates from this repository's source. It answers structural questions — what
connects to what, what breaks if I change X, who handles this message, what does role X govern —
without a model re-deriving the architecture from source each session.

It exposes **exactly ten** Tier-1 tools. The model itself is not documented here: see
[`docs/architecture/knowledge-graph-ontology-design.md`](../architecture/knowledge-graph-ontology-design.md)
for node labels, the allowed triples, and the extraction rules. Decision record:
[ADR-0031](../adr/0031/0031-structural-knowledge-graph.md).

## Prerequisites

The server queries a graph; it never builds one. Both steps must have run:

```powershell
docker compose --profile kg up -d neo4j     # opt-in profile, loopback-only, no auth
pwsh tools/kg/load-graph.ps1                # loads ontology.cypher + the newest generated seed
```

Without them every tool returns an error envelope naming `KG_NEO4J_URL` and these two commands.

## Tools

`nodeId` is always an exact, case-sensitive id (usually a fully-qualified type or member name).
Every id-taking tool resolves the id *before* traversing and fails loudly on a bad one: an id
matching no node returns an error, and an id matching more than one returns an error naming the
matched labels — never an empty list, never an arbitrary pick. **So an empty result always means
the node exists and genuinely has nothing to report**, which is what makes the empty results in
the section below safe to read as answers.

| Tool | Parameters | Returns (per row) |
|---|---|---|
| `GetNodeNeighbors` | `nodeId` | `nodeId`, `nodeLabel`, `edgeType`, `fromId`, `toId`, `neighborId`, `neighborLabel` |
| `GetBlastRadius` | `nodeId`, `maxDepth` (1..5, default 3) | `nodeId`, `label`, `depth` |
| `GetNodeDependencies` | `nodeId`, `maxDepth` (1..5, default 3) | `nodeId`, `label`, `depth` |
| `GetModuleDependencies` | `moduleId` | `moduleId` (the depended-on module), `contractId`, `contractKind` (`Message` \| `Query`) |
| `GetModuleOwnership` | `moduleId` | `id`, `label` |
| `GetActionExposure` | `nodeId` (Action or Job) | `targetId`, `targetLabel`, `edgeType`, `surfaceId`, `surfaceLabel` |
| `GetOrphanContracts` | none | `Id`, `Label`, `Confidence`, `Reason` |
| `GetJobSchedulers` | `jobId` | `jobId`, `triggerMode`, `schedulerIds[]` |
| `GetGovernedActions` | `roleOrPolicyId` | `actionId` (nullable), `surfaceId`, `surfaceLabel`, `governorLabel`, `governorId` |
| `FindStructurallySimilarActions` | `actionId`, `limit` (1..25, default 10) | `actionId`, `candidateShape[]`, `overlap`, `shapeCoverage` |

Depth bounds are clamped inside the query layer, so an out-of-range `maxDepth` is silently
narrowed rather than rejected — an unbounded traversal over ~1300 edges would flood the caller's
context.

`GetBlastRadius` and `GetNodeDependencies` return **one row per node, and `depth` is the shortest
distance** to it. A node reachable by several paths is not repeated per path length: on the real
graph `GetBlastRadius("ECommerceApp", 5)` reaches 602 nodes, and a per-path-length result would
report 940 rows for them. Read `depth` as a distance, not as evidence of how many ways there are
to reach something — the graph carries no path multiplicity in this answer.

### Where each answer comes from, when it matters

Three tools traverse an edge whose direction is easy to assume backwards. They are spelled out
here because getting one wrong produces an empty list, which reads as a true negative:

- **`GetActionExposure`** branches on the resolved label. An `Action` is exposed through its
  **outgoing** `EXPOSED_BY` edges to `Endpoint`/`Page`. A `Job` is reached through **incoming**
  `SCHEDULES` edges. Passing anything else returns an explicit error, not an empty list.
- **`GetGovernedActions`** starts at the `Role`/`Policy`. Authorization attaches to the
  **exposure surface**: the ontology declares `GOVERNED_BY` only from `Endpoint` and `Page`,
  never from `Action`. The action behind each governed surface is derived through `EXPOSED_BY`,
  and `actionId` is `null` where a governed surface has no action behind it.
- **`GetModuleDependencies`** derives module-to-module edges through the contracts that actually
  cross the boundary (`Action → PUBLISHES → Message → HANDLED_BY → MessageHandler`, and the
  `USES → Query → HANDLED_BY → QueryHandler` path). There is no direct module-to-module edge in
  the ontology, so an empty result means *no contract crosses*, not *unrelated modules*.

## `GetOrphanContracts` — read the confidence, not the row count

The tool reports contracts missing an edge the ontology leads you to expect. It is **not** a dead-code
list, and each row carries its own grade:

| Confidence | Meaning | Safe action |
|---|---|---|
| `high` | A genuine structural contradiction — e.g. a Message published but with no `HANDLED_BY` handler, or a Query with no handler at all. | Investigate as probable dead contract. |
| `contradiction` | A Job declaring `triggerMode: Deferred`, which requires a static `SCHEDULES` edge, but having none. | Investigate: the declaration and the wiring disagree. |
| `ambiguous` | A known false-positive class. The graph cannot see the caller. | **Never delete on this alone.** Verify by hand. |

Three `ambiguous` classes exist today, all explainable:

1. **Jobs with no static scheduler.** Five of the nine real jobs have zero `SCHEDULES` in-edges
   *by design*: `Scheduled` and `Manual` triggers are rows in the runtime `ScheduledJob` table,
   which no syntax parser can see.
2. **`StockAvailableQuery` and `CompletedOrderCountQuery`.** Both are genuinely sent — from
   `Infrastructure/Sales/Coupons/Adapters/`, which produces no `Action` node, so no `USES` edge
   can exist. They are reported rather than suppressed: hiding them behind a hardcoded ignore
   list would make the tool lie the day the parser is extended to cover adapters.
3. **Messages with no static publisher.** The ontology declares no
   `MessageHandler -[:PUBLISHES]-> Message` triple, so a message enqueued only from a handler
   looks unpublished. `StockReconciliationRequired` is the specimen: three real enqueues, zero
   edges, deliberately no warning.

## `FindStructurallySimilarActions` — a heuristic, and labelled as one

It ranks actions by overlap of outgoing **edge-type shape**, as a starting point for "has someone
built this shape here before". It is a structural proxy, **not** an authoritative pattern or
archetype match. Read the candidates before reusing them, and do not present the ranking to a
human as a design judgement.

## Explicitly out of scope

The graph is structural. It does **not** answer, and no tool should be added that fakes:

- effort, duration, or time estimates;
- which architectural pattern or archetype something is;
- test coverage, or whether something is tested;
- git authorship, ownership, or change history;
- runtime behaviour — call frequency, performance, actual message volume.

Each is either not derivable from a static structure graph or belongs to a different tool. A tool
answering one of these under an innocent name (`EstimateImpact`, `SuggestPattern`) is the failure
mode being guarded against; `KgMcp.Tests` fails the build on tool names matching that shape.

## Errors

A failed call returns an error envelope, never an empty result set, so a caller cannot mistake a
failure for "nothing found":

```json
{ "Tool": "GetActionExposure", "Error": "...", "Remedy": "Check KG_NEO4J_URL, then run: ..." }
```

Domain errors keep their own message and carry **no** `Remedy`, because the caller can fix the
question themselves. Only infrastructure failures carry the bring-up `Remedy`. Three domain
errors exist:

| Situation | Message says | What to do |
|---|---|---|
| No node has this id | Ids are exact and case-sensitive; the graph may predate the code | Check spelling and fully-qualified form. If the code is newer than the graph, regenerate and reload. |
| The id matches several nodes | Names every matched label | Ask against the intended label, or use a more specific id. |
| The node is the wrong kind for the tool | Names the label found and the kinds the tool answers for | Use the tool that matches the label — e.g. `GetJobSchedulers` needs a `Job`, `GetModuleOwnership` a `Module`. |

Getting an error here is the tool working. The alternative — an empty list — would make a typo
indistinguishable from a real answer, and that failure mode has now been the root cause of five
separate defects in this server.

## Client configuration

| Client | File | Notes |
|---|---|---|
| Claude Code | `.mcp.json` (repo root, tracked) | Root key `mcpServers`. Tracked only because `.gitignore` carries an explicit `!.mcp.json` negation past its blanket `*.json` rule. |
| VS Code / Copilot | `.vscode/mcp.json` | Eighth entry, `ecommerceapp-kg`. The header's "only ONE server enabled at a time" rule concerns the RAG variants; it does not mean the KG server must be disabled alongside them. |
| Copilot Web | *(intentionally absent)* | `.github/copilot/mcp.json` gets no entry: Copilot Web cannot reach `bolt://localhost:7687` or spawn the repository's `dotnet run`. An entry there could only ever fail. |

## Smoke check

```powershell
docker compose --profile kg up -d neo4j
pwsh tools/kg/load-graph.ps1
$env:KG_NEO4J_URL = "bolt://localhost:7687"
dotnet run --project tools/kg/kg-mcp/KgMcp.Server/KgMcp.Server.csproj --no-launch-profile
```

The server prints one `[kg-mcp] ready; transport=stdio; ...` line **to stderr** and then speaks
JSON-RPC on stdout. Anything else appearing on stdout corrupts the transport, so every diagnostic
in `KgMcp.Core` and `KgMcp.Server` uses `Console.Error`; a test enforces this.
