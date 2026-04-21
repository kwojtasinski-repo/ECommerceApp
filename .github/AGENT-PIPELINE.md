# Multi-Agent Pipeline — ECommerceApp

> **Read this** before invoking any pipeline agent (`@planner`, `@implementer`, `@verifier`, `@code-reviewer`, `@pr-commit`).
> This is the orchestration spec — agent files describe each stage; this file describes the **flow**.

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
    │             max-iter: 5
    │             scope: limited to plan files
    ▼
@verifier ────► DETERMINISTIC: build + unit + integration + arch
    │            max-iter: 1 (no auto-retry)
    │            Verdict: PASS or FAIL (verbatim output)
    │
    ├─ FAIL ──► HITL ──► human chooses: FIX / ABORT / REVISE PLAN
    │                    routes back to @implementer or @planner
    │
    ▼ PASS
@code-reviewer ► ADR + anti-patterns + BC boundaries + tests + security
    │             max-iter: 3
    │             Verdict: BLOCKS MERGE or APPROVED
    │
    ├─ BLOCKS MERGE ──► HITL ──► human fixes or routes back to @implementer
    │
    ▼ APPROVED
═══════ HITL CHECKPOINT 2 ═══════
    │  Human: ready for commit?
    │  Last chance to inspect.
    ▼
@pr-commit ────► branch name + Conventional Commit + PR description
    │             max-iter: 2
    │             does NOT run git
    ▼
HUMAN runs git commands
```

---

## HITL checkpoints — which are mandatory

| Checkpoint                    | Where                                  | Why                                                     |
| ----------------------------- | -------------------------------------- | ------------------------------------------------------- |
| **HITL 1 — Plan approval**    | After `@planner`                       | Catches scope creep / wrong approach before any edit    |
| **HITL on FAIL**              | After `@verifier` if FAIL              | No auto-retry — flaky tests + LLM panic = infinite loop |
| **HITL on BLOCKS MERGE**      | After `@code-reviewer` if blocks       | Human decides whether to fix or override                |
| **HITL 2 — Commit readiness** | After `@code-reviewer` APPROVED        | Last inspection before commit text is generated         |
| **HITL on bc-switch Step 1**  | Inside `@bc-switch` (domain agent)     | Pre-delete safety gate — never auto-delete legacy       |
| **HITL on adr-generator**     | Inside `@adr-generator` (domain agent) | ADRs are permanent — review draft before write          |

> **Rule**: agents never call the next agent automatically. Every transition is a human handoff (paste, approve, or explicit `@` invocation).

---

## Max iterations — full table

| Agent                       | Max iter | Iteration definition                          |
| --------------------------- | -------- | --------------------------------------------- |
| `@planner`                  | 3        | One full plan revision (after REVISE)         |
| `@implementer`              | 5        | One full pass of the plan                     |
| `@verifier`                 | 1        | One full build + test sweep (no auto-retry)   |
| `@code-reviewer`            | 3        | One full review pass (re-review after fix=+1) |
| `@pr-commit`                | 2        | One commit/PR text revision                   |
| `@bc-switch`                | 10       | One full switch attempt (Step 1 → report)     |
| `@adr-generator`            | 2        | One ADR draft (revision after feedback=+1)    |
| `@copilot-setup-maintainer` | n/a      | Per-workflow, audit can iterate per finding   |

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
