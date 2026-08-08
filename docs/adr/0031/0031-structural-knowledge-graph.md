# ADR-0031: A code-derived structural knowledge graph over this repository

## Status

Accepted — all seven build phases are implemented and validated. This ADR records
the final decision, the operational boundaries, and the measured outcome so the
toolchain is not reconstructed from phase plans or chat history.

## Date

Opened 2026-08-08 (after Phase 5 validation).

## Context

The repository is large enough that structural questions — *which module owns
this action, what breaks if I change this message, which pages call this
endpoint* — are answered today by grep, by reading `docs/adr/`, or by asking a
model to re-derive the architecture from source on every session. All three are
slow, and the last one is unreliable in a way that is hard to notice: a model
that infers an architecture confidently and wrongly produces plausible answers.

The existing retrieval stack does not cover this. ADR-0028's RAG MCP server
(`ecommerceapp-rag`) does semantic search over *documentation*; ADR-0029's
`context-mode` sandbox reduces *runtime tool output* in the context window.
Neither can answer a question whose answer is a path through the code's
structure, because neither has a structure to walk.

### Forces

- The answer must come from **code**, not from prose about code. A graph built
  from a design document goes stale the moment either drifts, and nothing fails.
- The graph must be **rebuildable in CI from source**, so staleness is a build
  signal rather than a discovery months later.
- This repo's integration mechanism is homegrown — an Outbox/Inbox message broker
  plus a synchronous `ModuleClient` query path — and is the main thing worth
  modelling. An off-the-shelf ontology would not have it.
- Extraction is **convention-dependent by nature**. Some conventions are enforced
  by the compiler (an `IMessage` base-list entry); some are enforced by nothing
  at all (a RequireJS `define([...])` at column zero). The second kind fails
  *silently*: the convention stops applying, the parser returns zero nodes, and
  the build stays green.
- Nothing may be guessed. A graph that fabricates edges is worse than no graph,
  because it is trusted.

## Decision

Build a knowledge graph over the repository's real structure, in three parts:

1. **An ontology** (`tools/kg/seed/ontology.json` + `ontology.cypher`) —
   node labels and the exhaustive list of allowed `(source, type, target)`
   triples, layered: Layer 1 structure (`System`, `Host`, `Module`), Layer 2
   surface (`Entity`, `Repository`, `Action`, `Endpoint`, `Page`, `Role`,
   `Policy`, `ScriptModule`), Layer 3 integration (`Message`, `MessageHandler`,
   `Query`, `QueryHandler`, `Job`).
2. **`kg-codegen`** (`tools/kg/kg-codegen/`) — a .NET tool that parses the
   repository and emits a `.cypher` seed file. Roslyn for everything C#; a
   narrow regex convention parser for the one non-C# source (client-side JS).
   `GraphValidator` fails the run on any triple the ontology does not declare.
3. **A Neo4j instance + an MCP server** (Phases 6–7) exposing ten tiered,
  read-only stdio query tools so an agent asks the graph instead of re-deriving
  the architecture.

### The rules that make the output trustworthy

These are the load-bearing part of the decision, and they cost real yield:

- **Warn, never fabricate.** A parser that cannot resolve a reference *confidently*
  emits a warning and no edge. A clean run today prints ~209 warnings; that is the
  intended state, not a backlog. Yield is deliberately traded for correctness —
  e.g. only 4 of 9 `Job` nodes get a `triggerMode`, because the other trigger
  modes are rows in a runtime table that no syntax parser can see.
- **Warn only about genuine non-matches.** The mirror rule, and the one Phase 5
  validation had to enforce: an expected non-match must be *silent*. A parser
  that reports every ordinary case as a failure buries the signal it exists to
  produce.
- **Convention-dependent parsers are flagged in the ontology itself**, with their
  failure mode written out (Guardrail 5). `ScriptModule` is the specimen: if the
  frontend migrates off AMD, the parser finds nothing and nothing errors.
- **No undeclared triple, ever** — including where the code plainly exhibits a
  relationship the ontology does not model. Two such gaps are recorded rather
  than papered over: `MessageHandler-[:PUBLISHES]->Message` and
  `Page-[:USES]->Page`.
- **Pinned real-repo tests.** Exact counts and exact ids from the real tree, not
  thresholds. A `>= 1` where the fact is "exactly 2" is a weakened pin.

### Build status

| Phase | Scope | State |
|---|---|---|
| 0–2 | Ontology seed, spine (`System`/`Host`/`Module`), `Entity`/`Repository`/`Action` | ✅ Built |
| 3a | `Endpoint`, `Page` | ✅ Built |
| 3b | `Role`, `Policy` (usage-derived, alias-splitting) | ✅ Built |
| 4a | `Message`, `MessageHandler` (async Outbox/Inbox side) | ✅ Built |
| 4b | `Query`, `QueryHandler` (sync `ModuleClient` side) | ✅ Built |
| 4c | `Job` + `SCHEDULES` | ✅ Built |
| 5 | `ScriptModule` (RequireJS/AMD convention parser) | ✅ Built, validated 2026-08-08 |
| 6 | Neo4j Compose service, typed `overrides.yaml`, job runtime facts, full ontology+seed loader | ✅ Built, validated 2026-08-08 |
| 7 | Separate `kg-mcp` solution, ten read-only stdio tools, Claude/Copilot wiring, documentation close-out | ✅ Built, validated 2026-08-08 |

Current census from the loaded Phase 6 graph: 201 `Action`, 176 `Page`, 53 `MessageHandler`, 49 `Endpoint`,
33 `Entity`, 28 `Repository`, 26 `Message`, 14 `Module`, 10 `ScriptModule`,
9 `Job`, 3 `Query`, 3 `QueryHandler`, 3 `Role`, 2 `Host`, 1 `Policy`,
1 `System`; **1330 generated structural edges** and **1441 loaded edges** including ontology metadata.

## Outcome and implementation summary

Phase 6 added the reproducible local graph runtime. `overrides.yaml` is loaded
once into typed records, supplies the 14 module facts and optional job runtime
facts, and is applied before validation. `SpineCatalog` now consumes those facts
without changing its byte output. `load-graph.ps1` selects the newest generated
seed, wipes stale projection data, loads both `ontology.cypher` and the seed,
strips null properties rejected by Neo4j, waits for indexes, and prints counts.
The Neo4j Compose service is opt-in under the `kg` profile with loopback-only
ports and `NEO4J_AUTH=none` permitted only for this local development graph.

Phase 7 added the sibling `tools/kg/kg-mcp/` solution so Neo4j and
`Testcontainers.Neo4j` do not enter the codegen build. `KgMcp.Core` owns the
read transactions, shared ambiguity-safe id resolution, and graph DTOs;
`KgMcp.Server` owns stdio framing, input clamping, MCP descriptions, and one
sanitized error guard. Exactly ten Tier-1 tools are exposed. No write Cypher,
HTTP transport, seed regeneration, graph mutation, effort estimate, pattern
classification, test-coverage claim, or git-authorship answer is provided.

The server is registered as `ecommerceapp-kg` in VS Code and the tracked root
`.mcp.json` for Claude Code. It is deliberately absent from
`.github/copilot/mcp.json`: GitHub Copilot Web cannot reach local Bolt or spawn
the repository's `dotnet run` process. The human and agent routing documentation
now distinguishes structural KG intent from RAG documentation intent.

Validation completed with `dotnet build` for both solutions, `KgMcp.Tests`
passing, the unchanged `KgCodegen.Tests` baseline, `docker compose config`,
Neo4j health/load checks, and live stdio startup. The remaining graph-model
questions are intentionally preserved: ontology declares `triggerModes` while
emitted job nodes use `triggerMode`, and cron/runtime rows are not statically
captured. `StockAvailableQuery` and `CompletedOrderCountQuery` remain known
query false positives because their adapter callers do not emit Action nodes.

This is the final phase in the roadmap. No Phase 8 is implied by this ADR.

## Alternatives considered

- **Keep using grep + documentation.** Rejected: it does not answer transitive
  questions, and it is exactly the status quo whose cost prompted this.
- **Derive the graph from the ADRs and architecture docs.** Rejected: it would
  encode intent rather than reality, and drift would be invisible — the failure
  mode this whole design is built to avoid.
- **Reuse the RAG MCP server (ADR-0028) with a graph-shaped index.** Rejected:
  semantic similarity cannot answer a bounded traversal, and a wrong-but-plausible
  chunk is precisely what a structural question must not return.
- **Adopt an off-the-shelf code-graph tool.** Rejected: none models this repo's
  homegrown Outbox/Inbox + `ModuleClient` split, which is the highest-value part
  of the graph and the reason a generic call graph would miss the interesting
  edges.
- **Add a JavaScript AST parser for `ScriptModule`.** Rejected for 12 files in a
  Roslyn-only tool. The regex approach's limitations are documented in
  `tools/kg/kg-codegen/README.md` rather than left implied.

## Consequences

**Positive**

- Structural questions get an answer derived from code, with the extraction rule
  for every node type written down and testable.
- Every modelling gap the codebase exposes is now recorded as a gap instead of
  being silently smoothed over by an invented edge.
- The tool is a CI-runnable check (`--check`), so convention drift becomes
  reviewable rather than archaeological.

**Negative / accepted costs**

- ~209 warnings per clean run. This is the design working, but it needs the
  taxonomy in `tools/kg/kg-codegen/README.md` to stay readable.
- Deliberately incomplete coverage where static analysis cannot see: runtime job
  trigger rows, `User.IsInRole(...)` branches in action bodies, `@Url.Action(…)`
  URLs, Adapter-class query sites.
- Guardrail 5 is **half implemented**: `YieldTracker` detects a zero yield in the
  current run; detecting a *previously* non-zero parser dropping to zero needs a
  count persisted across runs and does not exist yet.
- Phases 6–7 are unbuilt, so today the graph is a static `.cypher` file with no
  live instance and no MCP server. See "Querying the graph today" in the design
  document.

## Related

- Design and phase ledger: [`docs/architecture/knowledge-graph-ontology-design.md`](../../architecture/knowledge-graph-ontology-design.md)
- Tool: [`tools/kg/kg-codegen/README.md`](../../../tools/kg/kg-codegen/README.md)
- Ontology seed: `tools/kg/seed/ontology.json`, `tools/kg/seed/ontology.cypher`
- ADR-0028 — RAG MCP server (documentation search; complementary, not overlapping)
- ADR-0029 — context-mode MCP sandbox (runtime tool output; complementary)
