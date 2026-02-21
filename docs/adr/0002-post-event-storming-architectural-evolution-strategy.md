# ADR-0002: Post-Event-Storming Architectural Evolution Strategy

## Status
Proposed

## Date
2026-02-21

## Context

ECommerceApp is currently a monolithic ASP.NET Core MVC + Web API application using clean/onion
architecture (`Domain` → `Application` → `Infrastructure` → `Web` / `API`). The existing stack is:
- Single shared MSSQL database accessed via EF Core `Context` and `GenericRepository<T>`.
- Application services inheriting from `AbstractService` covering CRUD-oriented flows.
- Handler pattern (`CouponHandler`, `PaymentHandler`, `ItemHandler`) for cross-aggregate operations.
- No domain events, no message bus, no outbox pattern, no saga orchestration.
- `Payment` lifecycle tracked via `PaymentState` enum; `Order` lifecycle tracked via status fields on `Order`.

An event storming session was conducted to map domain events, bounded contexts (BCs), and lifecycle
flows across the following areas: Catalog, Orders, Payments, Refunds, Coupons, Customers, Currencies,
and Identity. The session revealed:

- **Implicit coupling** between `Order`, `Payment`, and `Refund` flows that is currently resolved
  through direct service calls (e.g., `PaymentHandler` calling `OrderService` internally).
- **State machine complexity** in `Payment` (`PaymentState`) and `Order` that will grow as business
  rules evolve — currently represented as plain enum checks scattered across `PaymentService` and
  `PaymentHandler`.
- **No audit trail or replayability** — current state is the only truth; past transitions are lost.
- **Risk of Big Ball of Mud** as more cross-domain flows (partial fulfillment, expiration, retries)
  are added without explicit event-driven boundaries.
- **BC autonomy not enforced** — all domain models share one `Context`, making it easy to leak
  cross-BC invariants accidentally.

This ADR records the strategic architectural direction agreed upon after the event storming. It is an
umbrella document. Each sub-decision listed here will be refined into a dedicated follow-up ADR when
its implementation is actually started.

## Decision

We will evolve ECommerceApp incrementally toward a DDD-aligned, event-driven, bounded-context-isolated
architecture. No timeline is committed — decisions are adopted as areas reach sufficient complexity to
justify the investment. The principles below are non-negotiable guardrails for all future work.

### 1. Selective Event Sourcing
Adopt Event Sourcing only where auditability and replayability have clear business value:
- **In scope**: `Order`, `Payment` (if lifecycle complexity increases beyond current `PaymentState`).
- **Out of scope**: `Coupon`, IAM (Identity), `Currency`, `ContactDetail`, `Tag`, `Brand`, `Type`.

Rationale: most domains are reference/lookup data where full ES would add cost with no benefit.

### 2. Saga Orchestration for lifecycle coordination
Introduce explicit Process Manager / Saga Orchestrator for flows that span multiple BCs:
- Order lifecycle (cart → order → payment → fulfillment).
- Payment retries and expiration handling.
- Refund flows triggered by order cancellation.
- Partial fulfillment scenarios.

Domain invariants stay inside aggregates. Sagas only coordinate, never enforce domain rules.

### 3. CQRS read model separation
Separate read models when:
- Read traffic starts impacting write performance.
- Analytical or dashboard queries grow in complexity.
- Projections require denormalized views across multiple aggregates.

Until that threshold is reached, the current `AbstractService`-based query methods are acceptable.

### 4. Inventory concurrency model
Introduce optimistic concurrency, reservation expiration, and idempotent reservation commands for
inventory-sensitive operations. A dedicated Reservation BC may be extracted if contention grows.

### 5. Distributed idempotency standard
Standardize idempotency across all commands and events:
- Idempotency key per command.
- Event deduplication at consumer level.
- Outbox pattern for reliable event publishing; Inbox pattern for safe consumption.

### 6. ML / recommendation isolation
Any future ML or profiling feature must:
- Live in a separate BC with no direct coupling to `Order` or `Payment`.
- Be advisory only — it must never control or block order/payment lifecycle.
- Integrate via events or a dedicated read API.

### 7. Payments domain as explicit state machine
Model `Payment` as an explicit state machine with named transitions, provider adapters behind an
Anti-Corruption Layer (ACL), and optionally Event Sourced state when audit requirements demand it.

### 8. Bounded context autonomy policy (non-negotiable)
All BCs must satisfy:
- No shared database across BC boundaries.
- No shared aggregates across BC boundaries.
- No cross-BC invariant enforcement.
- Integration only via domain events or well-defined API contracts.
- IAM / Identity never leaks into domain models (`ApplicationUser` must not appear in domain logic
  outside the Identity BC).

### 9. Refactoring guardrails
The following are permanent constraints — never bypass:
- Do not collapse BC boundaries for implementation convenience.
- Do not convert behavioral domains (Orders, Payments) into CRUD services.
- `Payment` must never control the `Order` lifecycle.
- `Availability` / inventory must never be a passive side effect — it must be an explicit domain participant.

### 10. Signals for architectural review
Trigger a dedicated architectural review if any of the following are observed:
- Synchronous cross-BC calls increasing in number or latency.
- Retry storms or timeout cascades appearing in logs.
- Saga coordination logic spreading into services or controllers.
- Race conditions increasing around inventory or payment state.
- Accidental coupling growing (shared DTOs, shared DB queries across BC boundaries).
- Lifecycle reasoning (what state is an order in and why) becoming difficult to trace.

## Consequences

### Positive
- Explicit domain event flows make cross-BC dependencies visible and auditable.
- Saga Orchestration removes implicit coupling between `PaymentHandler` and `OrderService`.
- BC autonomy policy prevents future accidental coupling from compounding.
- Selective ES for `Order` / `Payment` provides full audit trail and replayability without
  imposing ES on simpler domains.
- CQRS separation allows read and write paths to scale and evolve independently when needed.
- Guardrails and review signals give the team a shared language for architectural drift detection.

### Negative
- Higher infrastructure complexity when event bus, outbox/inbox, and saga engine are introduced.
- Increased learning curve for contributors unfamiliar with ES / Saga patterns.
- Incremental adoption requires discipline — partial implementations can be worse than none.
- More ADRs and architectural ceremony required as each sub-decision is implemented.

### Risks & mitigations
- **Premature adoption**: mitigated by the explicit "selective" scope and the requirement that each
  sub-decision has a dedicated ADR before implementation starts.
- **Incomplete BC isolation**: mitigated by the BC autonomy policy (section 8) enforced in code review
  and the shared `Context` being a visible signal of remaining coupling.
- **Saga sprawl**: mitigated by the guardrail that sagas only coordinate — domain invariants stay in
  aggregates; saga logic in controllers or services triggers an architectural review.
- **Over-engineering reference domains**: mitigated by explicit "out of scope" lists (section 1) and
  the principle of not optimizing prematurely.

## Alternatives considered

- **Keep CRUD-centric monolith indefinitely** — rejected because the event storming session revealed
  that `Order` → `Payment` → `Refund` lifecycle complexity already exceeds what CRUD services can
  express cleanly without hidden coupling (visible in `PaymentHandler` calling `OrderService`).
- **Full Event Sourcing for all BCs** — rejected because domains like `Coupon`, `Tag`, `Brand`,
  `Currency` are reference/lookup data; ES would add operational cost and complexity with no business
  value.
- **Microservices split now** — rejected because the team and codebase are not at a scale where the
  operational overhead of distributed services is justified; BC boundaries are established logically
  first, physical split is deferred.
- **Full CQRS from the start** — rejected because current read traffic does not justify a separate
  read store; the threshold-based adoption in section 3 defers this until it is actually needed.

## Migration plan

No migration is triggered by this ADR itself — it records strategic direction only.

Each sub-decision listed in this ADR will be implemented incrementally:
- A dedicated follow-up ADR must be created and accepted before any sub-decision is implemented.
- Existing code (`AbstractService`, `GenericRepository<T>`, `PaymentHandler`, `CouponHandler`,
  `ItemHandler`, `ExceptionMiddleware`) remains unchanged until a specific follow-up ADR supersedes it.
- The shared `Context` in `ECommerceApp.Infrastructure` is the primary indicator of remaining
  BC coupling and will be decomposed gradually as BC boundaries are formalized.

## References

- Related ADRs:
  - [ADR-0001 — ECommerceApp Project Overview and Technology Stack](./0001-project-overview-and-technology-stack.md)
  - Planned: ADR-0003 — Selective Event Sourcing for Orders and Payments
  - Planned: ADR-0004 — Saga Orchestration for Order and Payment lifecycle
  - Planned: ADR-0005 — CQRS read model separation
  - Planned: ADR-0006 — Inventory concurrency model with optimistic locking
  - Planned: ADR-0007 — Distributed idempotency standard with Outbox + Inbox
  - Planned: ADR-0008 — Payments domain as explicit state machine
  - Planned: ADR-0009 — Bounded context autonomy enforcement policy
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/web-api-instructions.md`](../../.github/instructions/web-api-instructions.md)
  - [`.github/instructions/migration-policy.md`](../../.github/instructions/migration-policy.md)
- Issues / PRs: <!-- link to PR when raised -->
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
