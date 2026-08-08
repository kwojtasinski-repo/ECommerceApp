---
name: kg-implementation-planner
description: >
  Turn a confirmed knowledge-graph ontology into a phased, verifiable
  implementation roadmap for the codegen tool that populates it — one phase at
  a time. Derives phase ordering from the ontology's relationship triples, then
  discovers markers against real code because the ontology contains none.
  Requires an explicitly supplied ontology file. Emits a phase artifact pair, an
  unattended handoff prompt, or implements the phase directly. Not for designing
  or extending an ontology, and not for querying an already-built graph.
argument-hint: "--ontology=<path> [--mode=plan|handoff|implement] [--phase=<id>]"
---

# KG Implementation Planner

Use this skill when the ontology design is finished and the human needs the **build** planned,
handed off, or implemented phase by phase.

**Source of truth**: `docs/architecture/kg-implementation-planning/README.md` — the hub, with one
file per step. This file is a thin wrapper; extend the methodology there, not here.

Project-agnostic by design: paths, naming conventions, build commands and the phase-status ledger
are discovered at runtime, never assumed.

## When to use

- A confirmed ontology exists and no phased build plan does.
- The next phase needs an artifact pair for this repo's review pipeline.
- A phase needs a self-contained prompt for an unattended agent.
- The human wants the next phase implemented now, still one phase at a time.

## When not to use

- The ontology is not designed, or not dry-run validated → `kg-ontology-designer`.
- The human wants to *query* an already-built graph → that is the serving layer's own skill.
- The human wants several phases pushed through in one sitting → refuse the batching, offer the
  next single phase.

## Required checkpoints

### Checkpoint 0 — ontology precondition (hard gate)

The human must supply the path to the ontology file. Discovered candidates may be offered as a
closed-set choice; never pick one silently. No confirmed ontology → **stop**. Never design or
edit an ontology in this skill.

### Checkpoint 1 — state reconciliation

Collect what is already built from every independent signal separately: artifacts on disk, VCS
status, codegen check-mode counts, the phase-status ledger, and any un-deleted phase artifacts.
**On disagreement, stop and report the conflict** — do not pick a winner.

### Checkpoint 2 — phase boundaries approval

Present the derived phase count and boundaries as a draft and stop. Granularity is the human's
call. No artifact is written before this approval.

### Checkpoint 3 — marker dry run (mandatory, not skippable)

Before generalizing any marker into a parser design: hand-trace 2–3 **real** code instances
through the proposed marker and resolution heuristic. Report the outcome **either way** — a clean
trace is exactly as valid a report as a broken one, and must be stated explicitly.

**Host-aware asking** (self-contained on purpose — this skill must work outside this repo):
- VS Code: `vscode_askQuestions`, freeform input, options only for a genuinely closed set.
- Visual Studio / CLI agents / other interactive hosts: plain chat question with numbered
  options, then stop and wait.
- Non-interactive host: fail loudly listing what is missing — never guess a marker or a phase
  boundary into existence.

## Workflow

1. **Resolve the profile and reconcile state** (Checkpoints 0–1) → `project-profile.md`.
2. **Derive phases** — topological sort over the ontology triples fixes the build order
   mechanically; split on unrelated source domains or differing parsing risk (Checkpoint 2)
   → `phase-derivation.md`.
3. **Discover markers against real code** — the ontology is pure schema; this is ~80 % of the work
   and is identical in every mode (Checkpoint 3) → `marker-discovery.md`.
4. **Emit** in the chosen mode — artifact pair (default), unattended handoff prompt, or implement
   here → `output-modes.md`.
5. **Execute and hand off validation** to a fresh session (implement mode only)
   → `output-modes.md`.

Verification runs after **every** step, not at the end → `verification.md`.

## Hard rules

- MUST have an explicitly confirmed ontology file before anything else.
- MUST NOT design, extend, or edit an ontology.
- MUST discover paths, naming, build commands and the status ledger rather than assuming them.
- MUST stop and report when state signals disagree about what is built.
- MUST get phase-boundary approval before writing any artifact.
- MUST re-verify every marker claim against real code, even one the docs call "confirmed".
- MUST report the marker dry run explicitly, including a clean pass.
- MUST emit exactly one phase's artifacts per pass, and never for a phase depending on one not yet
  implemented **and** validated.
- MUST keep validation independent of implementation in every mode, including implement-here.
- MUST report heuristic coverage as `X of Y resolved`, never as "non-zero".
- MUST treat the planned test list as a contract: every enumerated test either exists or is
  declined explicitly in the report. Shipping fewer is a FAIL however green the suite.
- MUST treat a previously-nonzero label yielding zero as a failure, not as success.
- MUST require silence for every *expected* non-match, and a warning only for a genuine unresolved
  reference. An over-warning parser defeats the zero-yield guardrail as thoroughly as a silent one,
  because the signal arrives into noise nobody reads. Pin the warning count, not just the node count.
- MUST name, for every defect class the phase can plausibly ship, the test layer able to observe
  it — source-level, fixture, or real-input end-to-end. The three are structurally blind to each
  other's failures; a green fixture suite is not evidence, and another assertion in a blind layer
  is not a fix. A phase producing a serving surface MUST have the real-input layer.
- MUST add to the fixture the shape the code can be wrong about, and MUST assert the error paths —
  input that does not exist, input of the wrong kind — not only the happy path. An empty result for
  either makes a typo indistinguishable from a true negative.
- MUST ship every published count with a one-line command that regenerates it and a statement of
  what population it counts. MUST pin facts rather than totals in tests: a total breaks on every
  legitimate commit and gets weakened or deleted.
- MUST propagate a corrected fact to every place the docs state it — phase artifacts are deleted
  on PASS and cannot hold durable knowledge.
- MUST treat phase numbering as per-series with a distinguishing slug; never renumber another
  series.
- MUST keep this file limited to Copilot-specific mechanics — methodology lives in
  `docs/architecture/kg-implementation-planning/`.

## Stop format

```text
KG IMPLEMENTATION PLANNER: <checkpoint name>
Ontology: <confirmed path>
State: <reconciled, or the conflict found>
Phase: <id + scope, or "boundaries pending approval">
Dry run: <what was traced / what held / what needs adjusting, or "clean pass">
Awaiting human decision: APPROVE / REVISE / ABORT
```
