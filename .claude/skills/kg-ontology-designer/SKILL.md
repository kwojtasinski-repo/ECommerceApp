---
name: kg-ontology-designer
description: Interactively design a code-derived knowledge-graph ontology (node/edge structure) and an MCP tool-tiering plan for this codebase or a related one. Use when the user wants to build a structural knowledge graph (Neo4j or similar) over a system's real architecture — modules/bounded contexts, endpoints, entities, and this repo's homegrown message-broker (Outbox/Inbox + sync query) flow — and needs help deciding node types, relationship types, and MCP tools. Runs a mandatory dry-run validation against real code before any ontology file is written, and reports the outcome either way (problems found, or a clean pass). Not for querying an already-built graph.
---

# KG Ontology Designer (ECommerceApp)

Helps design (not implement) the node/edge structure of a knowledge graph for
this project, plus the MCP tool tiering to serve it — grounded in real code,
not assumption.

**Read `docs/architecture/knowledge-graph-ontology-design.md` before proposing
anything.** It has the confirmed ECommerceApp ontology (what exists today —
`tools/kg/seed/ontology.json`/`.cypher`), the four-layer pattern, the
relationship-vocabulary discipline, and the guardrails below in full, with the
concrete evidence behind each one. This SKILL.md is a thin wrapper — don't
duplicate that content here, extend it there if the model changes.

Phase 6 operational state: the local graph is available through the `kg`
Compose profile and `tools/kg/load-graph.ps1`, which loads both ontology and
instance seed. Before changing Job properties, compare the declared
`Ontology:Property` layer with emitted nodes; the known `triggerModes` versus
`triggerMode` and runtime-cron mismatch is an explicit follow-up decision.

## Flow

### 1. Ask the structural questions

One `AskUserQuestion` call covering: architecture style (if extending beyond
what's already confirmed), variability points present, and any docs/vector-DB
to cross-link. Do NOT ask about backend stack or existing module list if this
is ECommerceApp itself — that's already established in the reference doc;
re-verify specific claims in code instead of re-asking the human (Guardrail 1
below applies to *you*, not just to what the human tells you).

### 2. Ask for target questions, in free text

> "What are the 5–10 real questions you want this graph to answer?"

Open-ended, not `AskUserQuestion`. Don't skip this even under time pressure —
it's what keeps the ontology demand-driven.

### 3. Propose a draft (or an extension to the confirmed model), and stop for review

Table format: layer, node types, properties, relation triples. If extending
the existing ECommerceApp ontology, show only the delta, not the whole thing
again.

### 4. Dry run — mandatory, not optional

Before treating any draft as final: pick 2–3 real cascades in the actual
codebase (grep/read, don't guess) and manually check every node and edge
against the declared triples. **Report the result to the human either way** —
"ran the simulation against X, here's what checked out and what needs
refinement" is exactly as valuable a message as finding nothing wrong. A
clean pass is real signal, especially interactively — say so plainly, don't
just silently proceed.

### 5. Tier the MCP tooling

Per target question: Tier-1 curated tool (name, typed params, one-line
intent) or explicit Tier 0 (`read_cypher`) if too open-ended for a fixed
template.

### 6. Only after confirmation, write files

Update `tools/kg/seed/ontology.json`/`.cypher`, sketch parser additions, sketch
MCP tool contracts. Never write ontology files before step 4 has actually run.

## Guardrails (non-negotiable — see the reference doc for the incidents behind each)

- **Verify-before-trust**: a marker/convention claim is worthless until
  grepped/read in this repo. A generic stack cheat sheet is a hypothesis, not
  a citation.
- **Dry run before locking any draft**, reporting both outcomes (problems
  found, or confirmed clean) — see step 4.
- **Never emit a node for an alias/convenience constant** (e.g. a C# role
  constant that's actually several comma-joined real roles) — resolve to the
  atomic real values before writing a node.
- **Never materialize a derived/aggregatable fact as its own stored edge** if
  it can drift from the edges it's computed from — expose it as a query/tool
  instead (see `GetModuleDependencies` in the reference doc for the worked
  example).
- **Flag convention-dependent parsers explicitly**, including what happens
  when the convention changes (silent zero-yield is not success — the
  codegen must warn on a previously-nonzero parser suddenly returning zero).
- **Target-questions-first** — never draft node/edge types before step 2.

## Relationship to the GitHub Copilot version

`.github/skills/kg-ontology-designer/SKILL.md` covers the same methodology
for Copilot-driven sessions. The shared content lives once in
`docs/architecture/knowledge-graph-ontology-design.md`; each wrapper only
carries its own tool's invocation mechanics. Keep both wrappers pointed at
the same reference doc rather than letting either accumulate its own copy of
the model.
