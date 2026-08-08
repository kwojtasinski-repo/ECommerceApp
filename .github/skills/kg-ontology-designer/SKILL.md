---
name: kg-ontology-designer
description: >
  Interactively design a code-derived knowledge-graph ontology (node/edge
  structure) for this codebase — modules/bounded contexts, endpoints,
  entities, and the homegrown Outbox/Inbox + sync-query message-broker flow —
  plus an MCP tool-tiering plan. Runs a mandatory dry-run against real code
  before any ontology file is written and reports the outcome either way.
  Not for querying an already-built graph.
argument-hint: "[--extend=<area>] [--target-questions=<free text>]"
---

# KG Ontology Designer

Use this skill when the human wants to design (not implement) the
node/edge structure of a Neo4j-style knowledge graph over ECommerceApp's real
architecture, or extend the confirmed model already in place.

**Source of truth**: `docs/architecture/knowledge-graph-ontology-design.md`.
It holds the confirmed ECommerceApp ontology, the four-layer pattern, the
relationship-vocabulary discipline, and the guardrails below with the
concrete evidence behind each. This file is a thin wrapper — extend the model
there, not here.

Living artifact: `tools/kg/seed/ontology.json` / `ontology.cypher`.

Phase 6 operational state: the local graph is available through the `kg`
Compose profile and `tools/kg/load-graph.ps1`, which loads both ontology and
instance seed. Before changing Job properties, compare the declared
`Ontology:Property` layer with emitted nodes; the known `triggerModes` versus
`triggerMode` and runtime-cron mismatch is an explicit follow-up decision.

When you make that comparison, expect declared properties to be **absent** on
some nodes rather than null: Neo4j rejects a null inside a `MERGE` map, so
`CypherEmitter` omits a property the parser could not infer (185 such today —
171 `route`, 6 `key`, 5 `triggerMode`, 3 `httpMethod`). Absence therefore means
"not inferable", never "not declared", and a property you add to the ontology
must stay meaningful when missing. If you need to distinguish "no value" from
"parser could not tell", that is a new property, not a null.

## When to use

- The human wants a new node/edge type added to the graph, or a new
  bounded context/integration mechanism modeled.
- The human wants to sanity-check whether an existing draft ontology
  actually matches the real code (dry run).
- The human wants the MCP tool-tiering table for a new set of target
  questions.

## When not to use

- The human wants to *query* an already-built graph — that belongs to the
  MCP server + its own query skill once `kg-codegen` exists, not this one.
- The human wants `kg-codegen` implementation work (parsers, emitter,
  compose.yml) — that's tracked separately; this skill is design-only.

## Required checkpoints

### Checkpoint 1 — target questions approval

Stop and ask the human before drafting any node/edge type if the 5–10
concrete questions the graph must answer are missing. Never infer them from
context alone.

### Checkpoint 2 — dry-run report (mandatory, not skippable)

Before any ontology file is written or amended: trace 2–3 real cascades in
the actual codebase against the draft triples. Report the result to the
human **either way** — a clean pass ("ran it against X and Y, everything
checked out") is exactly as valid an outcome as finding a problem, and must
be reported just as explicitly, not silently treated as "nothing to say."

### Checkpoint 3 — write approval

Stop and get explicit confirmation before writing/editing
`tools/kg/seed/ontology.json` or `.cypher`.

**Host-aware asking** (same mechanism as `pre-edit.instructions.md`
§Clarification policy — do not invent a different one for this skill):
- VS Code: `vscode_askQuestions`, freeform input, options only when the
  choice is a genuinely closed set.
- Visual Studio / other interactive host: plain chat question with numbered
  options, then stop and wait.
- Non-interactive host: fail loudly and state exactly what's missing — never
  guess a target question or a node/edge type into existence.

## Workflow

1. **Classify the ask**: new area to model, extension of the existing
   ontology, or a dry-run/validation request on an existing draft.
2. **Verify markers against real code before proposing anything** — grep or
   read the actual convention (e.g. `IEntityTypeConfiguration<T>` +
   `ToTable(...)` for entities, `MessageTypeRegistry` for message keys). A
   generic stack cheat sheet is a hypothesis, never a citation — see Hard
   Rules.
3. **Draft** the node/edge delta as a table (layer, type, properties,
   relation triples), reusing the existing 10-verb vocabulary unless a new
   relationship genuinely has no fit (see the reference doc for the bar that
   `HANDLED_BY`/`SCHEDULES` had to clear).
4. **Dry run** — Checkpoint 2.
5. **Tier MCP tooling** for each target question: Tier-1 curated tool
   (name, typed params, one-line intent) or explicit Tier 0 (`read_cypher`)
   if too open-ended.
6. **Write**, only after Checkpoint 3.

## Hard rules

- MUST verify a marker/convention claim against this repo's actual code
  before writing it into the ontology — never from memory or a generic
  cheat sheet alone.
- MUST run the dry run (Checkpoint 2) before any ontology file changes.
- MUST report the dry-run outcome explicitly, including a clean pass.
- MUST NOT emit a node for an alias/convenience constant (e.g. a role
  constant that is actually several comma-joined real roles) — resolve to
  the atomic real values first.
- MUST NOT materialize a derived/aggregatable relationship (e.g.
  module-to-module coupling) as its own stored edge when it can be computed
  from edges already in the graph — expose it as an MCP tool/query instead.
- MUST flag any parser that depends on a specific code convention still
  holding (e.g. AMD/RequireJS for `ScriptModule`), and state what happens if
  that convention changes — a validator that only checks schema triples will
  not catch a convention silently going stale.
- MUST NOT draft node/edge types before target questions are collected
  (Checkpoint 1).
- MUST ship every count it writes into documentation with the one-line command
  that regenerates it and a statement of what population it counts. The figures
  quoted above are measurements of a moving tree, not decisions; a generated
  total and a loaded total have already been compared as one population when
  they were two, and that read as a defect. Provenance table:
  `docs/reference/kg-mcp-tools.md` §"Where the numbers come from".
- MUST state, for every tool it tiers, what an empty result means. If "nothing
  found" and "wrong question" are indistinguishable to the caller, the tool
  needs an error path rather than a fallback — that ambiguity is the root cause
  of five defects in the served layer so far.
- MUST keep this file's content limited to Copilot-specific mechanics —
  the model and guardrail rationale live in
  `docs/architecture/knowledge-graph-ontology-design.md`, not here.

## Stop format

```text
KG ONTOLOGY DESIGNER: dry run complete
Checked: <cascades traced>
Result: <what held up / what needs refinement, or "clean pass">
Awaiting human approval before writing ontology files: APPROVE / REVISE / ABORT
```
