# Refactor Prompt

> **Usage**: Reference this file in Copilot Chat with `#file:.github/prompts/refactor.prompt.md`
> then append the refactor target. Examples:
>
> ```
> #file:.github/prompts/refactor.prompt.md
> Refactor `OrderService.PlaceAsync` for clarity. No behavioral change.
> ```
>
> ```
> #file:.github/prompts/refactor.prompt.md
> Refactor `Areas/Backoffice/Controllers/CouponsController` to remove
> duplication with `CategoriesController`. No behavioral change.
> ```
>
> This prompt is for **structural / readability / duplication** refactors.
> It is NOT for adding features, changing semantics, or migrating BCs (use `@bc-switch` for that).

---

## Core principle — Refactor preserves behavior

- **No behavioral change** without an accompanying test update.
- If you find a bug while refactoring → STOP, surface it, do NOT fix in this PR.
- If you discover a missing ADR → suggest one, do NOT bend rules silently.
- The diff should make the existing tests pass **without modification**.

---

## Refactor steps (follow in order)

### Step 0 — Pre-edit gate

Run the entire pre-edit checklist from `.github/instructions/pre-edit.instructions.md`:

1. Read `.github/context/agent-decisions.md` for prior corrections in the area.
2. Read the target file(s) and direct callers.
3. Read the relevant ADR for the BC touched.
4. Read the per-stack instruction file (auto-loaded via `applyTo:` glob).
5. Identify the relevant entries in `.github/context/anti-patterns-critical.context.md`.

### Step 1 — Identify scope

Produce a short scope summary:

| Item                | Value                                               |
| ------------------- | --------------------------------------------------- |
| Target file(s)      | <path>                                              |
| Bounded context     | <BC name from `bounded-context-map.md`>             |
| Governing ADR       | <ADR-NNNN>                                          |
| Test files affected | <UnitTests/... and IntegrationTests/...>            |
| Public API touched? | yes / no — if yes, list every public symbol changed |
| Behavioral risk     | low / medium / high — explain                       |

### Step 2 — Plan the refactor

List the concrete moves you will make. Use one bullet per atomic change:

- Extract method `X` from `Y` for clarity (no behavior change).
- Replace duplicated block in `A` and `B` with shared private helper.
- Rename local variable `tmp` → `pendingPayment`.
- Inline single-use private method `Z` into its only caller.

**STOP after producing the plan.** Wait for human approval before editing.
This is the refactor's HITL checkpoint.

### Step 3 — Apply the refactor

After approval:

1. Apply changes in the smallest reviewable units.
2. Re-read the target file end-to-end after each change.
3. Run `dotnet build` and the **existing** unit + integration tests after every meaningful step.
4. If a test fails → STOP, surface the failure, do NOT modify the test to pass.

### Step 4 — Verify behavior preservation

- All pre-existing tests pass without changes.
- Public API surface is unchanged (or changes are explicitly listed in Step 1 with caller updates).
- No new dependencies on other BCs (would require `IMessageBroker` and an ADR).
- No new entries violate `anti-patterns-critical.context.md`.

### Step 5 — Report

Produce a structured summary:

```
## Refactor Summary

| Metric              | Before | After |
|---------------------|--------|-------|
| Lines (target file) | <n>    | <n>   |
| Public methods      | <n>    | <n>   |
| Cyclomatic est.     | <n>    | <n>   |

## Changes
- <bullet per atomic change>

## Behavior preservation
- Tests run: <count> unit, <count> integration — all green
- Public API: unchanged / changed (list)

## Suggestions (do NOT auto-apply)
- <ADR update suggestion if applicable>
- <agent-decisions.md entry suggestion if a recurring issue was found>
```

---

## What this prompt does NOT do

- ❌ Does NOT add new features.
- ❌ Does NOT change exception types or status codes.
- ❌ Does NOT modify database schema or EF configurations.
- ❌ Does NOT touch `Infrastructure/Migrations/`.
- ❌ Does NOT cross BC boundaries (would need `IMessageBroker` design).
- ❌ Does NOT rename `given_when_should` legacy tests (per anti-patterns).

For any of the above → use a different prompt or agent (`@bc-switch`, `@adr-generator`, or a regular feature task).

---

## Rules

- **One refactor per invocation.** Do not bundle unrelated cleanups.
- **Read before editing.** The pre-edit gate is non-negotiable.
- **HITL checkpoint after Step 2.** Never start editing without approval of the plan.
- **Surface, do not silently fix.** Bugs, missing tests, missing ADRs → reported, not patched.
- **Append to `agent-decisions.md`** if you were corrected on something not yet logged (per `pre-edit.instructions.md` post-edit rule).
