# Multi-Agent Pipeline — ECommerceApp

> **Read this** before invoking any pipeline agent (`@planner`, `@implementer`, `@pr-commit`).
> This is the orchestration spec — agent files describe each stage; this file describes the **flow**.
>
> **Note**: `@verifier` and `@code-reviewer` are **embedded inside `@implementer`** — do NOT invoke them separately during a pipeline run. They remain available as standalone agents for one-off use outside the pipeline.

---

## When to use this pipeline

Use the **generic pipeline** for ordinary feature work, bug fixes, and refactors:

- Adding an endpoint with validation
- Fixing a defect
- Implementing a small slice of a roadmap item
- Generic refactor (also see `/refactor.prompt.md`)

Use a **domain agent** instead when the task matches its trigger:

| Task                               | Use                                       |
| ---------------------------------- | ----------------------------------------- |
| Atomic legacy → new BC switch      | `@bc-switch` (max-iter 10, has own HITLs) |
| Generating a new ADR               | `@adr-generator` (HITL before write)      |
| Syncing `.github/` config / `.sln` | `@copilot-setup-maintainer`               |
| One-off review of an existing PR   | `@code-reviewer` standalone               |

---

## Pipeline flow

```
USER REQUEST
    │
    ▼
@planner ────► produces file-level PLAN
    │            max-iter: 3
    │
    ▼ ═══════ HITL CHECKPOINT 1 ═══════
    │  Human: APPROVE / REJECT / REVISE
    │  No auto-handoff. Period.
    ▼
@implementer ──► executes APPROVED plan
    │             max-iter: 5 (passes)
    │             scope: limited to plan files
    │
    │  ┌─── EMBEDDED: 3-probe verification loop ────────────────────┐
    │  │  Probe = build + unit tests + integration tests (full suite)│
    │  │  Probe 1 → fix in scope → Probe 2 → fix → Probe 3          │
    │  │  All 3 PASS → proceed to embedded review                    │
    │  │  All 3 exhausted → FAIL REPORT → HITL (see below)          │
    │  └────────────────────────────────────────────────────────────┘
    │
    │  ┌─── EMBEDDED: inline code review ───────────────────────────┐
    │  │  Checks: anti-patterns, legacy, BC boundaries, security,   │
    │  │          style (advisory), test coverage (advisory)         │
    │  │  BLOCKS MERGE → surface immediately, NEVER auto-fix        │
    │  └────────────────────────────────────────────────────────────┘
    │
    ├─ Probes exhausted ──► HITL ──► human chooses:
    │                                FIX (manual) + RETRY / ABORT / REVISE PLAN → @planner
    │
    ├─ BLOCKS MERGE ──────► HITL ──► human chooses:
    │                                FIX description → @implementer applies + re-runs probes
    │                                ABORT
    │
    ▼ APPROVED (probes PASS + no blocks)
═══════ HITL CHECKPOINT 2 ═══════
    │  Human: ready for commit?
    │  Last chance to inspect.
    ▼
@pr-commit ────► branch name + Conventional Commit + PR description
    │             max-iter: 2
    │             does NOT run git
    ▼
HUMAN runs git commands
    │
    ▼
@copilot-setup-maintainer ── post-task sync (always)
    Workflow 11 (close-out check) → Workflow 12 (pipeline sync) if agents changed
    → Workflow 7 (changelog update)
```

---

## HITL checkpoints — which are mandatory

| Checkpoint                    | Where                                     | Why                                                                         |
| ----------------------------- | ----------------------------------------- | --------------------------------------------------------------------------- |
| **HITL 1 — Plan approval**    | After `@planner`                          | Catches scope creep / wrong approach before any edit                        |
| **HITL on probes exhausted**  | Inside `@implementer` after probe 3 fails | 3 auto-fixes tried — human must decide: fix manually / revise plan / abort  |
| **HITL on BLOCKS MERGE**      | Inside `@implementer` after inline review | Human decides fix description; implementer never auto-fixes review findings |
| **HITL 2 — Commit readiness** | After `@implementer` APPROVED             | Last inspection before commit text is generated                             |
| **HITL on bc-switch Step 1**  | Inside `@bc-switch` (domain agent)        | Pre-delete safety gate — never auto-delete legacy                           |
| **HITL on adr-generator**     | Inside `@adr-generator` (domain agent)    | ADRs are permanent — review draft before write                              |

> **Rule**: agents never call the next agent automatically. Every transition is a human handoff (paste, approve, or explicit `@` invocation).
> **`@verifier` and `@code-reviewer` standalone**: still valid for one-off use outside the pipeline (e.g. reviewing an existing PR, running deterministic checks on a hotfix).

---

## Max iterations — full table

| Agent                           | Max iter | Iteration definition                                              |
| ------------------------------- | -------- | ----------------------------------------------------------------- |
| `@planner`                      | 3        | One full plan revision (after REVISE)                             |
| `@implementer`                  | 5        | One full pass: impl steps + 3 probes + inline review              |
| `@verifier` _(standalone)_      | 1        | One full build + test sweep — no auto-retry (standalone use only) |
| `@code-reviewer` _(standalone)_ | 3        | One full review pass (re-review after fix = +1, standalone only)  |
| `@pr-commit`                    | 2        | One commit/PR text revision                                       |
| `@bc-switch`                    | 10       | One full switch attempt (Step 1 → report)                         |
| `@adr-generator`                | 2        | One ADR draft (revision after feedback = +1)                      |
| `@copilot-setup-maintainer`     | n/a      | Per-workflow; audit can iterate per finding                       |

After hitting the cap → **STOP, report what was tried, ask the human**. No silent continuation.

---

## When to skip the pipeline

Skip and edit directly when:

- You are the human, doing your own work.
- The change is one-line (typo, comment, log message).
- You are operating inside `@copilot-setup-maintainer` workflows.
- You are operating inside `@adr-generator` (it is its own flow).

Do NOT skip when:

- The change touches a BC under the project-state "blocked" list — STOP, ask first.
- The change touches `Infrastructure/Migrations/` — STOP, needs explicit human approval (per `safety.instructions.md`).
- The change crosses BC boundaries — needs `@adr-generator` first if no ADR covers it.

---

## Failure modes & how the pipeline mitigates them

| Failure mode                          | Mitigation                                              |
| ------------------------------------- | ------------------------------------------------------- |
| LLM ping-pong (fix A, break B, etc.)  | `@verifier` deterministic + `@implementer` max-iter 5   |
| Scope creep                           | `@planner` HITL 1 + `@implementer` plan-only discipline |
| "Looks correct" code that fails build | `@verifier` runs build/test before `@code-reviewer`     |
| Reviewer + verifier correlated panic  | `@verifier` is LLM-free — guaranteed independent signal |
| Forgotten lessons across sessions     | `agent-decisions.md` (read pre-edit, append post-edit)  |
| Stale config drift                    | `@copilot-setup-maintainer` Workflow 6 audit            |
| Cross-cutting unilateral decision     | New ADR via `@adr-generator` (HITL before write)        |

---

## Mandatory reading per stage

Each agent file lists its required reads. Summary:

- `@planner` → agent-decisions, project-state, known-issues, docs-index, ADR(s), per-stack instructions, anti-patterns-critical
- `@implementer` → APPROVED plan + agent-decisions + per-stack instructions
- `@verifier` → handoff message only (no architectural reads — it's deterministic)
- `@code-reviewer` → anti-patterns-critical, project-state, per-stack instructions, ADR per BC, agent-decisions (per area)
- `@pr-commit` → reviewer output + plan + bounded-context-map (for branch scope)

---

## Reporting & continuous improvement

- After every pipeline run: append to `agent-decisions.md` if a correction was given.
- After 2× same correction: promote to permanent rule (anti-pattern / instruction / new ADR).
- After config changes: trigger `@copilot-setup-maintainer` Workflow 6 (full audit) periodically — quarterly or after major batches.
