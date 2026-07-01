# Architect Consultant — Tooling Approach

This is a short pointer, not an architectural redesign of anything described elsewhere
in `docs/architecture/`.

## What this is

The **Architect Consultant** is a separate, self-contained framework: an orchestration
layer for architectural decision-support work (classification, evidence-grounded
confidence, interactive confirmation gates). It has its own frozen pipeline and its own
knowledge library. It is not a bounded context of ECommerceApp and does not appear in
`bounded-context-map.md`.

Its full documentation lives in [`docs/architect-consultant/`](../architect-consultant/README.md).

## How it relates to this repository's existing tooling

The framework introduces an **orchestration layer above** the repository's existing
Copilot agent pipeline (`@planner`, `@implementer`, `@code-reviewer`, `@verifier`,
`@pr-commit`, `@adr-generator`, etc.). It does not replace or duplicate those agents —
its `@coordinator` agent delegates to them for planning, implementation, review,
verification, and commit preparation. It only adds new agents for capabilities that did
not already exist: `@repository-analyzer`, `@stage-validator`, `@architecture-guardian`,
`@knowledge-librarian`, `@documentation-governance` (see
`.github/agents/coordinator.md` for the full delegation map).

## Status

Implementation has not started. See
[`docs/architect-consultant/IMPLEMENTATION_STATE.md`](../architect-consultant/IMPLEMENTATION_STATE.md)
for current stage and status.
