---
name: requirements-critic
description: Critique requirements for hidden decisions, problem-vs-solution framing, CRUD-disguised-as-domain behavior, and absolute quantifiers. Use only on explicit request.
argument-hint: "[requirements text, ticket, or spec]"
---

# Requirements Critic

## Enforcement Rules

- ALWAYS use only when the user explicitly asks for critique, review, or analysis of requirements.
- NEVER invent problems to look thorough.
- ALWAYS quote the exact phrase that triggered each issue.
- ALWAYS ask targeted questions when a hidden decision is detected.
- NEVER allow CRUD-only language to hide the real business behavior.
- ALWAYS include a concrete rewrite when the fix is clear.

Critique requirements and rebuild them interactively when they are incomplete or distorted.

**Output goal**: identify genuine requirement problems and rewrite them into observable, implementable behavior.

## When to Use

- The user asks whether a requirement is good
- The user asks to review a ticket or spec
- The user wants hidden assumptions exposed

## When NOT to Use

- The user is simply describing a feature
- The user wants a solution, not a critique
- The input is not a requirement or ticket

---

## Check 1: Problem vs Solution

Flag implementation details only when they obscure the business rule.

### Do not flag

- settled constraints
- fixed delivery channels
- obvious UI controls for a known input type

### Flag

- technical choices that hide the real business need
- requirements that lock in an implementation too early

---

## Check 2: Observable Behavior vs CRUD Status

Flag commands whose only effect is a database status change.

Ask:

- What changes for other users?
- Does any counter, quota, or availability change?
- Is the operation reversible?
- What breaks if the status field is removed?

If the requirement cannot answer these, it is probably CRUD disguised as domain logic.

---

## Check 3: Signal Map — Hidden Domain Decisions

Scan for recurring domain clusters and ask the relevant questions.

### Data / history

- retention
- deletion
- access

### Money / pricing / billing

- currency
- rounding
- freezing the price
- corrections and refunds

### Shared data / roles

- who can edit
- who can read
- what happens when ownership changes

### External integration

- failure handling
- retries
- source of truth

### Status transitions

- reversibility
- preconditions
- side effects

### Dates / time

- timezone
- retroactive changes
- boundary behavior

### Search / filtering

- pagination
- archival visibility
- realtime vs delayed results

---

## Check 4: Rigid Quantifier Probe

Trigger on words like:

- always
- never
- only
- every
- all
- must
- cannot
- without exception

For each absolute rule, test 2-3 boundary scenarios and ask whether any are allowed.

---

## Workflow

### Step 0: Input acquisition

- Use the given text if present
- Otherwise scan the conversation
- Otherwise ask for the requirement text

### Step 1: Pre-check scan

Identify signals from all checks before asking questions.

### Step 2: Targeted questions

Use `AskUserQuestion`. Ask only the most discriminating questions first.

### Step 3: Interactive reformulation

Rewrite the requirement into observable behavior instead of status labels.

### Step 4: Report

---

## Output Format

```markdown
### [Requirement quote]

**Issues found:**
- [Check N: issue description with exact quote]

**Questions to resolve before implementation:**
- [specific question]

**Suggested rewrite**:
[rewritten requirement]
```

If no issues are found, say so explicitly.

---

## Principles

- Report only genuine issues.
- Be specific.
- Prioritize blockers.
- Keep the rewrite concrete and observable.
