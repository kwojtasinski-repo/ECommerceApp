# Knowledge Library

## Purpose

The Knowledge Library is the Architect Consultant's validated memory.

It stores reusable architectural knowledge, decision guidance, verified pilot evidence,
and reference archetypes.

It is not a second RAG.
It is not a documentation dump.
It is not a speculative design space.

Only validated knowledge enters this library.

---

## Role in the Consultant Workflow

The Knowledge Library supports:

- `archetype-scanner` for fast candidate discovery
- `coordinator` for orchestration and decision support
- future validation flows for evidence-based learning

The library must support:

- quick lookup
- incremental growth
- evidence-backed entries
- weak-model-friendly consumption

---

## Library Structure

### 1. Business Archetypes

Business concepts that frequently appear in enterprise systems.

Examples:

- Accounting
- Pricing
- Party
- Inventory
- Workflow
- Order
- Shipment
- Booking
- Payment
- Subscription
- Asset
- Case
- Document
- Audit

These describe business semantics, not implementation techniques.

---

### 2. Architectural Patterns

Reusable implementation strategies.

Examples:

- Saga
- CQRS
- Outbox
- Event Sourcing
- Process Manager
- Transaction Script
- Modular Monolith
- Event-Driven Integration

These describe how the system is organized.

---

### 3. DDD Patterns

Domain-Driven Design building blocks.

Examples:

- Aggregate
- Repository
- Domain Service
- Factory
- Specification
- Value Object
- Domain Event
- Entity

These describe model structure and domain behavior.

---

### 4. Integration Patterns

Patterns for cross-boundary communication.

Examples:

- ACL
- Open Host
- Published Language
- Conformist
- Message Broker
- Compensation Handler
- Integration Event

These describe how bounded contexts interact.

---

### 5. Decision Strategies

How to think when choosing between architectural options.

Examples:

- When to introduce Saga
- When to split Aggregate
- When to create a new BC
- When not to use CQRS
- When to keep a Transaction
- When to publish an Event
- When NOT to publish an Event

These are not patterns.
These are decision heuristics for architects.

---

### 6. Heuristics

Short rules that guide reasoning.

Examples:

- Aggregate Size
- BC Splitting
- Event Naming
- Boundary Ownership
- Consistency Boundary
- Ask Before Assume

Example format:

```md
## Aggregate Size

Question:
Is the aggregate enforcing a consistency boundary, or simply grouping data?

When to prefer:
- one consistency boundary
- one transactional invariant

When to reject:
- grouping only for convenience
```

These are checklist-style thinking aids.

---

### 7. Smells

Signals that something is likely wrong.

Examples:

- God Aggregate
- Chatty Events
- Shared Database
- Anemic Domain
- Implicit Orchestration
- Hidden Cross-BC Coupling

These are warning signs, not solutions.

---

### 8. ADR References

Validated architectural decisions from the repository.

Examples:

- ADR-0011 Inventory / Availability
- ADR-0014 Sales / Orders
- ADR-0015 Sales / Payments
- ADR-0016 Coupons
- ADR-0026 Order Lifecycle Saga

These are the highest-trust repository facts.

---

### 9. Evidence Library

Validated observations from pilots and real projects.

Examples:

- `pilot-order-placement-001`
- `pilot-saga-gap-001`
- `pilot-archetype-party-001`

These are not general principles.
They are recorded evidence from actual cases.

---

## Seed Strategy

Each category starts with 2–5 validated seed entries.

Additional entries are added only when they are:

- reused across multiple projects
- validated during implementation
- supported by evidence
- repeatedly useful in pilots

The library grows from usage, not speculation.

---

## Entry Rules

Every entry must have:

- `id`
- `title`
- `type`
- `tags`
- `maturity`
- `confidence`
- `problem-types`
- `signals`
- `anti-signals`
- `related`
- `source`
- `status`

Example:

```md
id: arch-pattern-saga
title: Saga
type: architectural-pattern
maturity: proven
confidence: high
problem-types:
  - cross-bc coordination
  - partial failure compensation
signals:
  - multiple steps across bounded contexts
  - need for compensation
anti-signals:
  - single transactional boundary is enough
source:
  - ADR-0026
status: validated
```

---

## Confidence Rules

- Do not estimate confidence numerically.
- Use qualitative confidence only.
- Confidence must be grounded in:
  - validated source material
  - confirmed pilot behavior
  - explicit assumptions
- Missing evidence lowers confidence.
- Low confidence should trigger questions, not guessing.

---

## Promotion Rules

A concept becomes a library entry only when it is:

- observed in real use
- validated by a pilot or ADR
- useful beyond a single case
- not just a one-off idea

Do not promote:

- speculative ideas
- future features
- untested abstractions

---

## Maintenance Rules

- Do not duplicate repository docs.
- Do not invent missing knowledge.
- Do not expand categories without evidence.
- Do not add a new class of knowledge until multiple real examples require it.
- Keep entries short and searchable.
- Prefer references to original sources over rewritten explanations.

---

## What This Library Is Not

This library is not:

- the architecture itself
- a full domain encyclopedia
- a prompt dump
- a replacement for ADRs
- a speculative brainstorming space

---

## Minimal Starting Set

Suggested initial seed set:

### Business Archetypes

- Accounting
- Pricing
- Party
- Inventory
- Workflow

### Architectural Patterns

- Saga
- CQRS
- Outbox

### DDD Patterns

- Aggregate
- Repository
- Domain Service

### Integration Patterns

- ACL
- Published Language
- Compensation Handler

### Decision Strategies

- When to introduce Saga
- When to split Aggregate
- When not to use CQRS
- When to publish an Event

### Heuristics

- Aggregate Size
- BC Splitting
- Event Naming

### Smells

- God Aggregate
- Chatty Events
- Shared Database

### ADR References

- ADR-0011
- ADR-0014
- ADR-0015
- ADR-0016
- ADR-0026

### Evidence Library

- pilot-order-placement-001
- pilot-party-archetype-001
