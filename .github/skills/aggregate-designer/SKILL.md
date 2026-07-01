---
name: aggregate-designer
description: Guided skill for designing aggregate boundaries (consistency units). Uses fit checks, command extraction, conflict analysis, sequencing probes, and locking strategy. Use when the user asks about aggregates, consistency boundaries, locking, concurrent contention, or when commands may compete for the same state.
argument-hint: "[domain description or list of commands/requirements]"
---

# Aggregate Designer

## Enforcement Rules

- ALWAYS run the fit check before designing anything.
- DO NOT continue if the problem is not a contention / consistency-boundary problem.
- ALWAYS ask one question at a time when the next step depends on user input.
- DO NOT guess missing commands, conflicts, or invariants.
- ALWAYS separate commands, facts/events, and queries.
- ALWAYS surface self-conflicts and time-range conflicts explicitly.
- DO NOT propose implementation details unless the user asks for them.
- ALWAYS stop if the answer requires a new architectural concept.

---

## Purpose

Design a consistency unit that protects invariants under concurrency.

An aggregate is a locking unit.
It is not an OOP pattern.
Its job is to lock what must be locked and leave everything else free.

---

## When to Use

- The domain has commands that change shared state
- Multiple actors may touch the same data concurrently
- The main question is "what must not happen at the same time?"
- The user wants a consistency boundary, not a CRUD model

## When NOT to Use

- The problem is read-only
- The problem is simple CRUD
- The rules are only input validation
- The system only records outcomes decided elsewhere

---

## Workflow

### Phase 0: Input

Get the domain context.

1. If an argument was provided, use it.
2. If not, inspect the conversation for a domain description.
3. If still missing, ask the user for a rough description of:
   - operations that change state
   - rules that must never be broken
   - who or what triggers the operations

Do not continue until you have enough context to test fit.

### Phase 1: Fit Check

Ask:

> Can the data used to decide whether an operation is allowed be changed by another concurrent request at the same time?

If the answer is clearly no, stop and tell the user this is probably not an aggregate problem.

If the answer is uncertain, ask:

- Can multiple actors trigger these operations simultaneously on the same data?
- Yes / No / Unsure

If it still does not fit, stop.

### Phase 2: Extract Commands

List all commands that change state.

For each item, classify it as one of:

- Command
- Fact / Event
- Query

Do not continue until the command list is confirmed.

### Phase 3: Conflict Analysis

Build a pairwise conflict matrix for commands.

Ask whether each conflict is real and whether any command is missing.

Always surface:

- self-conflict
- parameter-dependent conflict
- time-range conflict

### Phase 4: Business Process Sequencing Probe

For each important conflict, ask whether the business process already separates the operations in time.

Use a simple Q/A format:

```md
Q: Can these operations happen at the same time?
A: Yes / No / Unsure
Result: Real conflict / Theoretical conflict

Q: Does the business process already separate them in time?
A: Yes / No / Unsure
Result: Keep aggregate / Maybe simpler constraint

Q: Is there a real concurrent write risk?
A: Yes / No / Unsure
Result: Aggregate needed / Aggregate not needed
```

If the user wants a visual summary, offer it at the end. Only generate a table if the user says yes.

If a conflict is only theoretical, record that.

### Phase 5: Boundary Decision

Decide:

- one aggregate
- multiple aggregates
- database constraint instead of aggregate
- no aggregate needed

Prefer the simplest option that protects the invariant.

---

## Output

### Aggregate Model

- Aggregate name
- Commands it guards
- Invariants it protects
- Locking strategy
- Known trade-offs

### Conflict Table

| Command A | Command B | Conflict? | Reason |
|---|---|---|---|

### Open Decisions

- Anything still uncertain
- Anything the user must confirm

### Recommendation

- Final boundary choice
- Why this choice is the safest simple option

---

## Weak-Model Rules

- Use short questions.
- Use short answers.
- Use explicit yes/no/unsure choices when possible.
- Repeat the fit check if the domain becomes unclear.
- Never move ahead on assumed invariants.
- Never turn the skill into a generic architecture essay.
