---
name: kg-implementation-planner
description: Turn a confirmed knowledge-graph ontology into a phased, verifiable implementation roadmap for the codegen tool that populates it — one phase at a time. Use when the ontology design is done (via kg-ontology-designer or equivalent) and the human needs the build planned, handed off to an unattended agent, or implemented phase by phase. Derives phase ordering from the ontology's relationship triples, then discovers markers against real code because the ontology contains none. Requires an explicitly supplied ontology file. Not for designing or extending an ontology, and not for querying an already-built graph.
---

# KG Implementation Planner

Plans and drives the build of the codegen tool that populates a knowledge graph, starting from a
**confirmed** ontology. Project-agnostic: paths, build commands and conventions are discovered,
not assumed.

**Read `docs/architecture/kg-implementation-planning/README.md` first.** It is the hub; each step
delegates to its own file there. This SKILL.md carries Claude Code mechanics only — extend the
methodology in those docs, never here.

Companion to `kg-ontology-designer`, which is design-only and stops exactly where this starts.

## Precondition — stop if unmet

The human must supply the **path to the ontology file** produced by the ontology-design skill.
Offer discovered candidates as a closed-set choice if helpful, but never pick one silently. No
confirmed ontology → stop and send them to `kg-ontology-designer`. This skill never designs or
edits an ontology.

## Flow

Read the linked file when you reach the step; don't preload all of them.

1. **Profile + state** — resolve where everything lives, then reconcile every independent signal
   of what is already built. Stop on disagreement rather than picking a winner.
   → `project-profile.md`
2. **Derive phases** — topological sort over the ontology's triples gives the build order for
   free. Present as a draft, stop for confirmation of boundaries.
   → `phase-derivation.md`
3. **Marker discovery** — read real code. The ontology is pure schema and contains no markers;
   this is ~80 % of the work and is identical in every output mode.
   → `marker-discovery.md`
4. **Emit** in the chosen mode: artifact pair (default), unattended handoff prompt, or implement
   here. → `output-modes.md`
5. **Execute** (implement-here mode only), then hand validation to a fresh session.
   → `output-modes.md`

Verification runs *after every step*, not at the end → `verification.md`.

## Asking

Use `AskUserQuestion` for bounded choices — output mode, phase boundaries, which ontology file is
authoritative, transport choices with no default. Use plain free text for anything open-ended
about intent or scope; never compress a design decision into an option list.

## Guardrails (see the reference docs for the incidents behind each)

- **Explicit ontology, or stop.** Never glob-and-guess the spec everything derives from.
- **Discover, don't assume.** Paths, naming, build commands and status ledgers are resolved at
  runtime; only three assumptions are irreducible (see the README).
- **Verify-before-trust.** Documentation is a hypothesis even when it says "confirmed" — including
  documentation you wrote yourself. Re-check every marker claim against real code.
- **Hand-trace before generalizing.** 2–3 real instances through the proposed heuristic, reported
  either way — a clean pass is real signal.
- **One phase per pass.** Never plan or build past a phase that isn't both implemented and
  validated.
- **Validation stays independent**, in every mode — the implementer is never the validator.
- **Coverage is a number.** Report `X of Y resolved`, never "non-zero".
- **The planned test list is a contract.** Every test the plan enumerates either exists or is
  declined explicitly in the report. Shipping fewer is a FAIL on its own, however green the suite.
- **Silence is not success.** A previously-nonzero label yielding zero is a failure.
- **Noise is not success either.** Expected non-matches must be silent; only a genuine unresolved
  reference warns. An over-warning parser hides the drop-to-zero signal as effectively as a silent
  one. Pin the warning count against the real tree, not just the node count.
- **Name the layer that catches it.** Three test layers — source-level, fixture, real-input
  end-to-end — and each is structurally blind to what the others catch. For every defect class the
  phase can plausibly ship, name the layer able to *observe* it; a fixture suite alone is not
  enough, and a fourth assertion in a blind layer is not a fix. A serving surface needs the
  real-input layer. → `verification.md`
- **A fixture is only as good as the topology it contains.** Add the shape the code can be wrong
  about, then break the code and watch the test go red. Assert the error paths — "does not exist"
  and "wrong kind" — not only the happy one.
- **Publish numbers with their reproduction command; pin facts, not totals.** Every count written
  into durable docs ships with a one-liner that regenerates it and states what population it
  counts. Tests pin things true regardless of size, never a total that any legitimate commit
  breaks. → `verification.md`
- **Propagate corrections everywhere** the docs state the fact — phase artifacts get deleted on
  PASS, so a correction living only there is lost.
- **Per-series phase numbering.** Unrelated series may share numbers; never renumber someone
  else's.

## Relationship to the GitHub Copilot version

`.github/skills/kg-implementation-planner/SKILL.md` covers the same methodology for Copilot-driven
sessions. Shared content lives once in `docs/architecture/kg-implementation-planning/`; each
wrapper carries only its own host's invocation mechanics. Keep both pointed at the same docs
rather than letting either accumulate a copy.
