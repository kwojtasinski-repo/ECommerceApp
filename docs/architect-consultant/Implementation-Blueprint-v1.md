# Implementation Blueprint v1

## 1. Purpose

This document is the execution plan for the first implementation wave of the Architect Consultant framework.

It answers two questions only:

- What are we building?
- In what order?

The architecture is frozen. This document does not redesign it. This document does not expand scope. This document does not introduce new concepts.

---

## 2. Implementation Contract

Every implementation task MUST end with the following:

- Completed work
- Files changed
- Definition of Done verification
- Remaining work
- Risks
- Next recommended stage

If any of the above is missing, the task is incomplete.

---

## 3. Implementation Priorities

Apply the following priorities in order.

Priority 1 — Architecture Blueprint

- Use the frozen architecture as the implementation boundary.
- Do not redesign architecture.
- Do not rename concepts.
- Do not merge responsibilities.

Priority 2 — Current Stage Objective

- Complete the current stage before starting the next one.
- Do not expand scope to future stages.

Priority 3 — Definition of Done

- A stage is not complete until its Definition of Done is satisfied.
- Do not continue with an incomplete stage.

Priority 4 — Existing Code

- Prefer existing repository assets over new infrastructure.
- Reuse existing knowledge sources where possible.
- Consume, do not duplicate.

Priority 5 — Optimization

- Do not optimize before the first pilot.
- Do not add abstractions for speculative future needs.

Lower priorities must never override higher priorities.

---

## 4. Frozen Architecture Baseline

The following remain fixed for this implementation:

- Pipeline: Request → Intake → Classification → Context Collection → Reasoning → Review → Output
- Knowledge library with five roles:
  - archetypes
  - architectural-patterns
  - heuristics
  - review-gates
  - output-contracts
- Workflow operates on metadata, not folder structure alone
- The framework advises; it does not decide on behalf of the user
- Source priority:
  1. Business Specification
  2. Architecture Specification
  3. ADR
  4. Repository Code
  5. External Knowledge
- Uncertainty handling follows this sequence:
  1. retrieve available evidence
  2. ask the user when only the user can resolve the gap
  3. abstain with reason when evidence is insufficient
- Confidence is earned from evidence, not estimated as a number

If implementation requires any change to this baseline, stop and create a blocker.

---

## 5. First Implementation Objective

Build the smallest vertical slice that can validate the highest-risk assumption of the framework.

The first implementation must validate this assumption:

The interactive evidence-driven loop can produce a meaningful result beyond a generic prompt.

The first slice must test:

- source intake by priority
- evidence collection
- classification
- confidence grounded in evidence
- interactive confirmation and question asking

The first implementation is not a feature MVP. It is a foundation pilot.

---

## 6. Execution Strategy

Implement in stages.

Do not start the next stage until the current stage satisfies its Definition of Done.

Recommended first slice:

Request → Intake → Classification → Classification Confirmation Gate → Stop

This slice is first because it tests the most important implementation risk:

Can the framework ask better questions, ground confidence in evidence, and pause when evidence is insufficient?

---

## 7. Stage 0 — Harness and Pilot Preparation

### What is being built?

A minimal harness to run the first slice and record pilot outcomes.

### Why now?

The first pilot must be run with explicit criteria.

### Inputs

- One real architectural problem case
- Success criteria
- Hypotheses to validate

### Outputs

- A runnable pilot entry point
- A recorded list of pilot hypotheses
- A recorded list of success criteria

### Definition of Ready

Before starting this stage, verify:

- [ ] A real input case is available
- [ ] The expected output shape is known
- [ ] Success criteria are written down
- [ ] Hypotheses are written down
- [ ] No active blocker prevents this stage

### Allowed Decisions

- Create a minimal pilot harness
- Define the expected output shape
- Record the pilot hypotheses
- Record the success criteria

### Forbidden Decisions

- Redesign the architecture
- Expand beyond the pilot harness scope
- Introduce future-stage abstractions

### Definition of Done

- The pilot can be started with a single input case
- The hypotheses are written before the first run
- The success criteria are written before the first run
- The expected output shape is defined

### Verification

- Confirm that the harness accepts a single input case
- Confirm that hypotheses are documented
- Confirm that success criteria are explicit

### Exit Criteria

Proceed to Stage 1 only when Stage 0 is complete.

---

## 8. Stage 1 — Source Intake and Evidence Collection

### What is being built?

A source intake path that gathers evidence according to the frozen source priority.

### Why now?

All later stages depend on access to evidence.

### Inputs

- Business Specification
- Architecture Specification
- ADR
- Repository Code
- External Knowledge, if available

### Outputs

- An evidence inventory
- A list of missing inputs
- A list of source gaps that may reduce decision quality

### Definition of Ready

Before starting this stage, verify:

- [ ] Stage 0 is complete
- [ ] The input sources are available or explicitly absent
- [ ] No active blocker prevents evidence collection
- [ ] The source priority is known

### Allowed Decisions

- Collect evidence from the configured source priority
- Surface missing inputs explicitly
- Record source gaps

### Forbidden Decisions

- Read sources in random order
- Ignore missing documentation
- Skip source gap reporting

### Definition of Done

- The system can collect evidence from the configured source priority
- The output clearly identifies what was found and what was missing
- Missing information is surfaced explicitly

### Verification

- Verify that sources are processed in the defined priority order
- Verify that missing inputs are listed explicitly
- Verify that the output can be used by later stages

### Exit Criteria

Proceed to Stage 2 only when evidence intake is working and evidence gaps are explicit.

---

## 9. Stage 2 — Minimal Knowledge Library and Candidate Selection

### What is being built?

A minimal knowledge library with the initial seed entries needed to populate candidate options.

### Why now?

The first interactive gate needs candidate options to compare.

### Inputs

- The minimal seed library
- The evidence inventory from Stage 1

### Outputs

- A list of candidate archetypes, patterns, heuristics, or review gates
- A short explanation for why each candidate is relevant

### Definition of Ready

Before starting this stage, verify:

- [ ] Stage 1 is complete
- [ ] Evidence inventory is available
- [ ] The minimal seed library is available
- [ ] No active blocker prevents candidate generation

### Allowed Decisions

- Add new seed entries
- Improve metadata for existing entries
- Fix parsing or lookup bugs in the knowledge library

### Forbidden Decisions

- Create new knowledge types
- Change the metadata schema
- Introduce a ranking engine
- Introduce embeddings or new retrieval infrastructure

### Definition of Done

- The system returns candidates based on the available evidence
- The candidates are traceable to the evidence inventory
- The output does not require a new architecture abstraction

### Verification

- Verify that each candidate is connected to evidence or metadata
- Verify that the library remains small and testable
- Verify that the output remains understandable to a weaker implementation model

### Exit Criteria

Proceed to Stage 3 only when candidate output is readable and evidence-linked.

---

## 10. Stage 3 — Classification and Confidence

### What is being built?

A classification step that produces an initial understanding of the problem and a confidence assessment grounded in evidence.

### Why now?

The confirmation gate depends on a coherent initial understanding.

### Inputs

- Evidence inventory from Stage 1
- Candidate set from Stage 2

### Outputs

- A classification result
- A current understanding summary
- Confidence levels tied to explicit evidence, assumptions, or gaps

### Definition of Ready

Before starting this stage, verify:

- [ ] Stage 2 is complete
- [ ] Evidence inventory is available
- [ ] Candidate set is available
- [ ] No active blocker prevents classification

### Allowed Decisions

- Produce a classification result
- Produce a current understanding summary
- Produce confidence tied to evidence or explicit gaps

### Forbidden Decisions

- Assign confidence without evidence
- Invent assumptions without stating them
- Produce a final recommendation before the gate

### Definition of Done

- The output includes a clear classification result
- Confidence is tied to evidence, assumptions, or explicit missing inputs
- The output is readable enough to be used by the next gate

### Verification

- Verify that each confidence statement can be traced to a concrete evidence point or explicit gap
- Verify that the classification is understandable without extra explanation
- Verify that the output is suitable for the confirmation gate

### Exit Criteria

Proceed to Stage 4 only when classification and confidence are explicit and traceable.

---

## 11. Stage 4 — Confirmation Gate and Interactive Questions

### What is being built?

The first interactive gate that pauses the workflow when evidence is insufficient or when the current understanding is not yet stable.

### Why now?

This is the core implementation risk.

### Inputs

- Current understanding from Stage 3
- Evidence gaps from Stage 1

### Outputs

- A confirmation state with:
  - current understanding
  - confidence
  - proposed questions
  - a decision to proceed, adjust, or stop

### Definition of Ready

Before starting this stage, verify:

- [ ] Stage 3 is complete
- [ ] Current understanding is available
- [ ] Evidence gaps are available
- [ ] No active blocker prevents the gate

### Allowed Decisions

- Pause and ask targeted questions
- Proceed when the user resolves the gap
- Stop with an explicit reason when evidence is inadequate

### Forbidden Decisions

- Ask generic questions
- Continue without resolving the blocking gap
- Turn the gate into a decorative formality

### Definition of Done

- The system can pause and ask targeted questions
- Questions are tied to unresolved uncertainty or missing evidence
- The system does not proceed by guessing when evidence is insufficient
- The system can stop with a reason when the evidence is inadequate

### Verification

- Verify that each question reduces uncertainty
- Verify that the system halts when evidence is inadequate
- Verify that the system can proceed when the user resolves the gap

### Exit Criteria

The first slice is complete when the gate works as an actual decision checkpoint.

---

## 12. Pilot Success Criteria

The first pilot succeeds when the following are true:

- The workflow reaches a coherent current understanding
- Confidence is tied to named evidence or named gaps
- The system asks useful questions at the right moment
- The system pauses rather than guessing when evidence is insufficient
- The system can proceed after the user answers the blocking questions
- The output is useful to a human reviewer

The first pilot does not need to be perfect. It must be informative.

---

## 13. Escalation Policy

If any of the following happens, stop and create a blocker:

- Multiple solutions exist and the implementation cannot choose safely
- Architecture must change
- Definition of Done cannot be satisfied
- The ambiguity cannot be resolved from documentation
- The implementation would require a new architectural concept
- The implementation would require a new abstraction without clear evidence

When escalation is needed:

1. Stop implementation
2. Create BLOCKER.md
3. Record the blocker clearly
4. Wait for human decision

---

## 14. Repository Rules

Follow these repository rules during implementation:

- Never create documentation that duplicates existing documentation
- Prefer extending existing files over creating new ones
- Avoid introducing new folders unless required by the current stage
- Keep commits stage-focused
- Do not leave TODO comments without creating a deferred item
- Keep implementation changes local to the current stage
- Do not solve deferred items during the first implementation wave

---

## 15. Anti-Patterns

Do not do the following:

- Premature abstraction
- Generic helper classes without a clear need
- Future-proofing before the first pilot
- Hidden architecture changes
- Silent scope expansion
- TODO-driven development
- Refactoring outside the current stage

---

## 16. Deferred for Future Validation

The following are not blocked by the current implementation plan. They are deferred for future validation:

- The full decision brief contract
- The exact final front matter schema beyond the minimum required by implementation
- The detailed selection mechanism beyond the initial seed-based candidate flow
- Any upgrade to the knowledge selection path beyond the first pilot
- Any change to the frozen architecture baseline

Do not solve these during the first implementation wave.

---

## 17. Implementation Exit Criteria for v1

The first implementation wave is complete when:

- The first slice runs end to end
- The confirmation gate works as a real checkpoint
- The output is traceable to evidence and gaps
- The pilot produces a useful learning signal
- The implementation remains within the frozen architecture
