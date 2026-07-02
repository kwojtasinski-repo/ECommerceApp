---
name: problem-classifier
description: Classify business problems into CRUD, Transformation & Presentation, Integration, or Resource Contention. Use when the user asks which modeling class fits best.
argument-hint: "[business requirement, UI mockup, or feature description]"
---

# Modeling Problem Class Classifier

## Enforcement Rules

- ALWAYS classify only when the user asks for a modeling class or problem type.
- NEVER turn this into an archetype mapper.
- ALWAYS scan for all four classes before answering.
- ALWAYS ask targeted questions when the class is ambiguous.
- NEVER guess when concurrency, integration, or state gating is unclear.

Given a business requirement, identify the best modeling problem class, ask targeted clarifying questions, and recommend an implementation approach aligned with that class.

## The 4 Problem Classes

### Class 1: CRUD

Use when data is stored and retrieved exactly as entered, with no shared-state contention.

### Class 2: Transformation & Presentation

Use when the operation reads data and transforms it for display or consumption without changing state.

### Class 3: Integration

Use when the main challenge is sequencing, contracts, and failure handling across systems or bounded contexts.

### Class 4: Resource Contention

Use when the operation must protect an answer to "can I do X?" against concurrent state changes.

---

## Workflow

### Step 0: Input acquisition

- Use the provided requirement or UI mockup
- If nothing is provided, ask for a concrete business scenario

### Step 1: Pre-check scan

Scan the text or mockup for signals from all four classes.

### Step 2: Targeted clarifying questions

Ask the most discriminating questions first.

### Step 3: Classify

Choose a primary class and, if needed, a secondary class.

### Step 4: Explain

State the triggers, the risk of choosing the wrong class, and the recommended implementation shape.

---

## Output Format

```markdown
# Problem Class Classification

## Primary class
[CRUD / Transformation & Presentation / Integration / Resource Contention]

## Secondary class
[optional]

## Why this fits
[short evidence-based explanation]

## Questions to resolve
- [question]

## Recommended implementation direction
[brief guidance]
```

---

## Hard Signals

- Save/edit/delete with no side effects → CRUD
- Read-only derived output → Transformation & Presentation
- Send/receive/sync with another system → Integration
- Concurrent availability, booking, locking, or assignment → Resource Contention
