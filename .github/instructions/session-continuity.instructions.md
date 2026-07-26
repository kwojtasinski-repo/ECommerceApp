---
applyTo: "**"
---

# Session continuity — phased plan/validation files

> **Applies to every agent, every model, every tool** — this is not specific to any one Copilot model
> or to the `@planner`/`@implementer` pipeline in [`../AGENT-PIPELINE.md`](../AGENT-PIPELINE.md). It is
> a general convention for how non-trivial work is checkpointed across context/session boundaries,
> regardless of who or what executes it.

## Why

Sessions end — context windows fill, connections drop, budgets run out. Without an explicit checkpoint,
resuming means re-deriving "where did we leave off" from scratch, which is slow and error-prone.
Splitting complex work into numbered phases, each with a written plan and a written validation
checklist, solves two problems at once:

1. **Resumability** — the plan file *is* the checkpoint. Any agent (or the human) can pick it up cold.
2. **Independent verification** — validation happens in a separate session/context from the one that
   implemented. A fresh context checking its own work is a weaker signal than a fresh context checking
   *someone else's* work — the same reasoning `.github/agents/verifier.md` gives for staying LLM-free
   and independent of `@implementer`'s own claims.

This composes with, but is distinct from, the role-based pipeline in `AGENT-PIPELINE.md`
(`@planner` → `@implementer` → HITL). That pipeline is about **who** does each step and where the human
gates are. This convention is about **how work in any single step survives a session boundary**. A
`@planner` plan can itself be written as phase 1's implementation file if the work is expected to span
sessions.

## When to use it

Use phased plan/validation files when a task is either:
- **Complex enough to need breaking into phases** (multiple independent-ish chunks of work), or
- **Likely to outlive the current session/context** (long-running, large diff, or explicitly expected to
  pause and resume later).

Skip it for small, single-session, single-file changes — the overhead isn't worth it.

## Convention

**Location**: `.github/plans/` — a dedicated folder, separate from `.github/context/` (stable context
agents load every run) and from `docs/` (permanent, curated documentation).

**Naming**: `NN-phase-<name>-implementation.md` and `NN-phase-<name>-validation.md`, e.g.:
```
.github/plans/01-phase-cart-restore-implementation.md
.github/plans/01-phase-cart-restore-validation.md
.github/plans/02-phase-outbox-implementation.md
.github/plans/02-phase-outbox-validation.md
```

**Implementation file** — follow the plan structure already defined in `.github/agents/planner.md`
(Scope, Files to add/modify/delete, Tests required, Steps, Verification commands, Risks, Rollback).
Reuse that structure even when `@planner` isn't formally invoked — it's a good plan shape regardless.

**Validation file** — a checklist for the *next* session (different context, ideally a different model
than whatever implemented) to independently confirm: deterministic verification (build + tests, don't
trust the implementing session's own claims), test-coverage checklist, spec-conformance checklist
(the decisions the implementer might "improve" away from what was actually intended), and a standard
code-review pass. End with an explicit cleanup step (see below).

## Committing — not by default

Files under `.github/plans/` are **git-ignored by default** (see `.gitignore`). They are working
scratch, not project history — committing every phase's plan/validation pair would clutter the repo
with artifacts nobody needs once the phase is done.

**Exception**: commit a specific phase's pair with `git add -f` when there's a concrete continuity
reason to survive beyond the local working copy — e.g. the phase is incomplete and must resume on a
specific future date, or a token/context budget cutoff is expected before the phase finishes. If you
force-add for this reason, say so explicitly (commit message or handoff note) so the next session knows
it's an intentional exception, not an oversight.

## Cleanup rules (exact — do not improvise)

After a phase's validation passes:

1. Delete **only that phase's two files** (its `-implementation.md` and `-validation.md`).
2. Never delete another phase's files, and never delete unrelated files that happen to live in
   `.github/plans/`.
3. Delete the `.github/plans/` folder itself **only** when the phase that just completed was the last
   one remaining in it (i.e. the folder would otherwise be empty).
4. Update the permanent tracking doc (the roadmap entry / ADR / spec this phase belongs to) to reflect
   completion — the plan and validation files are disposable, the outcome record is not.

On a validation **FAIL**: do not delete anything. Report findings per the validation file's format and
let the human decide (send back for fixes / fix directly / abort) — same HITL discipline as the rest of
the pipeline (see `AGENT-PIPELINE.md` § HITL checkpoints).
