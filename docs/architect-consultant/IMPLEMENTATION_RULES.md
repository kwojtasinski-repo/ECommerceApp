# IMPLEMENTATION_RULES

## Purpose

These rules govern implementation of the first version of the Architect Consultant framework.

They are mandatory.

They are the operational guardrail for implementation models of different capability levels.

---

## Core Rules

- ALWAYS treat the architecture as frozen.
- ALWAYS use Implementation Blueprint v1 as the single source of truth.
- NEVER redesign architecture during implementation.
- NEVER introduce new architectural concepts.
- NEVER rename existing concepts.
- NEVER merge responsibilities that are already separated.
- NEVER expand scope beyond the current implementation stage.
- ALWAYS implement one stage at a time.
- NEVER continue to the next stage before the current stage satisfies its Definition of Done.
- ALWAYS verify Definition of Ready before starting a stage.
- ALWAYS verify Definition of Done before claiming a stage complete.
- ALWAYS verify outputs before continuing.
- NEVER continue after failed verification.
- ALWAYS prefer incremental delivery over broad speculative implementation.
- ALWAYS prefer fail-cheap validation over large upfront implementation.
- NEVER optimize before the first pilot.
- NEVER add abstractions that are not required for the current stage.
- ALWAYS apply YAGNI strictly.
- ALWAYS consume existing repository knowledge. NEVER duplicate it.
- ALWAYS use the frozen source priority when collecting evidence.
- ALWAYS ground confidence in evidence, assumptions, or explicit gaps.
- NEVER invent confidence without support.
- NEVER guess when evidence is insufficient.
- ALWAYS ask targeted questions when only the user can resolve the gap.
- ALWAYS abstain with reason when evidence is insufficient and the gap cannot be resolved.
- ALWAYS keep the implementation simple enough for weaker models to follow.
- ALWAYS use explicit directives.
- NEVER infer missing requirements.
- ALWAYS report blockers immediately.

---

## Stage Rules

- START with Stage 0.
- COMPLETE Stage 0 before Stage 1.
- COMPLETE Stage 1 before Stage 2.
- COMPLETE Stage 2 before Stage 3.
- COMPLETE Stage 3 before Stage 4.
- NEVER skip a stage.
- NEVER start the next stage with an incomplete previous stage.
- IF a stage cannot meet its Definition of Done, STOP and report the blocker.

---

## Definition of Ready Rules

Before starting a stage, verify all of the following:

- Previous stage is complete
- Previous Definition of Done is satisfied
- Required inputs exist
- No active blocker prevents the stage
- Architecture remains unchanged

If any item is missing, do not start the stage.

---

## Definition of Done Rules

A stage is complete only when all of the following are true:

- The required work for the stage is finished
- The stage output is produced
- Definition of Done checks are completed
- Verification is recorded
- Remaining risks are explicit
- The next recommended stage is clear

If any item is missing, the stage is incomplete.

---

## Blocker Rules

- IF implementation requires a new architectural concept, STOP.
- IF implementation changes the frozen pipeline structure, STOP.
- IF implementation merges existing responsibilities, STOP.
- IF implementation requires an abstraction that is not already justified by the current stage, STOP.
- IF implementation cannot proceed without redefining the frozen architecture, STOP.
- WHEN a blocker occurs, create a clear blocker report and wait for human decision.

---

## Escalation Policy

Escalate immediately when:

- Multiple solutions exist and the implementation cannot choose safely
- Architecture must change
- Definition of Done cannot be satisfied
- The ambiguity cannot be resolved from documentation
- The implementation would require a new architectural concept
- The implementation would require a new abstraction without clear evidence

Escalation procedure:

1. Stop implementation
2. Create BLOCKER.md
3. Record the blocker clearly
4. Wait for human decision

---

## Verification Rules

- ALWAYS verify before continuation.
- ALWAYS verify outputs against the stage Definition of Done.
- ALWAYS verify that evidence is explicit.
- ALWAYS verify that confidence is traceable.
- ALWAYS verify that questions are targeted and useful.
- ALWAYS verify that the implementation remains within the frozen architecture.

---

## Repository Rules

- NEVER create documentation that duplicates existing documentation.
- PREFER extending existing files over creating new files.
- AVOID introducing new folders unless required by the current stage.
- KEEP commits stage-focused.
- DO NOT leave TODO comments without creating a deferred item.
- DO NOT solve deferred items during the first implementation wave.

---

## Anti-Patterns

- Premature abstraction
- Generic helper classes without a clear need
- Future-proofing before the first pilot
- Hidden architecture changes
- Silent scope expansion
- TODO-driven development
- Refactoring outside the current stage

---

## Prompting Rules for Implementation Models

- FOLLOW the current stage objective only.
- FOLLOW the Definition of Done exactly.
- PREFER explicit instructions over flexible interpretation.
- IF the task is ambiguous, ask for clarification before changing the architecture.
- IF the task is outside the current stage, defer it.
- IF the task would change architecture, stop and raise a blocker.
