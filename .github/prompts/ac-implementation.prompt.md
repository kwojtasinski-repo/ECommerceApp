# AC Implementation Prompt

> **Usage**: `#file:.github/prompts/ac-implementation.prompt.md` then append:
> `Implement Stage <N> of the Architect Consultant Blueprint.`

---

## Before doing anything

1. Read `docs/architect-consultant/IMPLEMENTATION_RULES.md`.
2. Read `docs/architect-consultant/IMPLEMENTATION_STATE.md` to find the current stage.
3. Confirm the requested stage matches the current stage. If it does not, refuse and say so.

## Delegate via @coordinator

Invoke `@coordinator` with the stage number. Do not implement directly — the Coordinator
owns delegation to `@stage-validator`, `@repository-analyzer`, `@planner`, `@implementer`,
`@code-reviewer`, `@verifier`, and `@documentation-governance`.

## Do not

- Do not skip Definition of Ready verification.
- Do not implement a stage other than the current one.
- Do not redesign architecture, rename concepts, or merge responsibilities.
- Do not mark a stage complete without a `@stage-validator` DONE verdict.
