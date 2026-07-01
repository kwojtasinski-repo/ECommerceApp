# IMPLEMENTATION_PLAYBOOK

## Purpose

This playbook explains how to use the existing implementation documentation in practice.

It is not an architecture document.
It is not a design document.
It is not a replacement for the Blueprint, the Rules, or the Task Template.

Its purpose is operational:

- explain how implementation work should be performed
- explain how the documents work together
- reduce ambiguity during execution
- help humans and multiple LLMs work in a consistent way

Use this document together with the following immutable documents:

- `docs/architect-consultant/Implementation-Blueprint-v1.md`
- `docs/architect-consultant/IMPLEMENTATION_RULES.md`
- `docs/architect-consultant/IMPLEMENTATION_STATE.md`
- `.github/templates/TASK_TEMPLATE.md` (canonical operational copy)

---

## Repository Documentation Map

Use the documents in the following way.

| Document | Purpose | Answers the question |
|---|---|---|
| Architecture documents | Describe the frozen architecture and the intended system shape | What is the system supposed to be? |
| Implementation Blueprint | Describe the implementation sequence and stage order | What do we build, and in what order? |
| Implementation Rules | Define mandatory implementation behavior | How must implementation proceed? |
| Task Template | Standardize execution of each implementation task | How should a single task be executed? |
| Implementation Playbook | Explain how to run implementation work in practice | How do we use the documents together? |

Do not use the playbook to redesign the architecture.
Do not use the playbook to expand scope.
Do not use the playbook to replace the Rules.

---

## Recommended Workflow

Use the following workflow for every implementation task.

1. Read `docs/architect-consultant/IMPLEMENTATION_RULES.md`
2. Read the relevant stage in `docs/architect-consultant/Implementation-Blueprint-v1.md`
3. Fill `.github/templates/TASK_TEMPLATE.md`
4. Implement only the current stage objective
5. Verify Definition of Ready
6. Verify Definition of Done
7. Create a blocker if required
8. Record remaining work and risks
9. Move to the next stage only when the current stage is complete

Do not skip steps.
Do not improvise a new process.
Do not change the architecture during implementation.

---

## Working with Different LLMs

Different models may perform different parts of the work, but all of them must follow the same rules.

### Stronger models

Use stronger models for:

- planning the next implementation step
- reviewing completed work
- analyzing blockers
- handling complex implementation phases
- validating stage completion

### Mid-tier models

Use mid-tier models for:

- implementing a defined stage
- refactoring within the current stage
- writing or updating tests
- handling structured implementation tasks

### Smaller models

Use smaller models for:

- isolated tasks with clear boundaries
- small refactors
- documentation updates
- test additions
- simple data or parsing work

### Common rule for all models

Every model must follow the same implementation contract:

- read the Rules
- read the current Blueprint stage
- use the Task Template
- verify Definition of Done
- report blockers immediately

Do not allow weaker models to invent architecture changes.

---

## Human Responsibilities

Some decisions always require a human.

Humans must decide:

- whether a blocker is real
- whether architecture must change
- whether Definition of Done is truly satisfied
- whether the pilot outcome is acceptable
- whether the next stage should begin
- whether implementation should continue after a blocker

Humans must not delegate these decisions to models.

---

## Blocker Workflow

A blocker is raised when implementation cannot continue safely within the frozen architecture.

### When to raise a blocker

Raise a blocker when:

- architecture must change
- the frozen pipeline must change
- responsibilities must be merged
- a new architectural concept is required
- a new abstraction is required without evidence from the current stage
- Definition of Done cannot be satisfied
- ambiguity cannot be resolved from available documentation

### Blocker procedure

1. Stop implementation
2. Record the blocker clearly
3. Create `BLOCKER.md`
4. Describe the impact
5. Describe why the current stage cannot continue
6. Wait for human decision

Do not continue silently after a blocker.

---

## Pilot Workflow

The first implementation wave is a foundation pilot.

### Pilot objective

Validate whether the first slice produces a meaningful result beyond a generic prompt.

### Pilot execution

1. Prepare the input case
2. Run Stage 0
3. Run Stage 1
4. Run Stage 2
5. Run Stage 3
6. Run Stage 4
7. Evaluate the pilot against the success criteria
8. Record findings
9. Decide whether to continue to the next implementation wave

### Pilot evaluation

Evaluate the pilot using the criteria in the Blueprint.

Focus on:

- evidence quality
- confidence quality
- question quality
- pause behavior when evidence is insufficient
- usefulness to a human reviewer

Do not judge the pilot by feature count.

---

## Code Review Checklist

Use this checklist for implementation review.

- [ ] The work matches the current stage objective
- [ ] The work stayed within the frozen architecture
- [ ] Definition of Ready was satisfied before implementation started
- [ ] Definition of Done was verified before completion
- [ ] The output is traceable to evidence or explicit gaps
- [ ] No forbidden abstractions were introduced
- [ ] No scope expansion occurred
- [ ] Risks and remaining work were documented
- [ ] Blockers were raised immediately when needed
- [ ] The implementation remains understandable for the next stage

---

## Implementation Checklist

Before considering work complete, verify the following:

- [ ] The current stage objective is understood
- [ ] The correct stage was selected
- [ ] Definition of Ready was checked
- [ ] The implementation stayed within scope
- [ ] The work satisfies Definition of Done
- [ ] Verification was performed
- [ ] Errors or gaps were documented
- [ ] Remaining work was recorded
- [ ] Risks were recorded
- [ ] The next recommended stage is clear

If any item is missing, the work is not complete.

---

## Daily Workflow

Use the following workflow during normal implementation days.

1. Review the current stage objective
2. Review the current stage Definition of Ready
3. Review the current stage Definition of Done
4. Fill or update the Task Template
5. Implement the planned work
6. Verify the result
7. Record blocker, risk, or remaining work if needed
8. Prepare the next task for the following stage

Keep daily work stage-focused.
Do not mix multiple unverified stages in one day.

---

## Repository Evolution

The repository should evolve in a controlled sequence.

### After Pilot 1

- Validate the first slice
- Capture the actual findings
- Keep the architecture unchanged unless a blocker requires a human decision
- Decide whether the next implementation wave should proceed

### After Pilot 2

- Validate repeated behavior on a second real case
- Compare outcomes against the same stage expectations
- Refine the implementation process, not the architecture

### At v1

- The first implementation wave is considered complete when the pilot and initial stage work are stable
- The team can proceed to the next wave with evidence instead of assumptions
- Any future changes must be justified by implementation evidence, not speculation

---

## Final Operating Principle

Implementation work must remain disciplined.

The goal is not to build everything at once.
The goal is to build the next correct stage, verify it, and move forward with evidence.
