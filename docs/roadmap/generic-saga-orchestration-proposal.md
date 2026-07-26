# Generic saga orchestration engine — requirements & implementation proposal

> **Status:** Proposed — captured for later resumption, not started. We are consciously living
> with the current state (Option A choreography compensation handlers, no Outbox) until this is
> picked up. Not a problem today; do not start implementation from this doc without re-confirming
> scope first, since requirements may drift before we return to it.
> **Origin:** conversation following `order-placement-compensation-followup.md` workstream review
> (2026-07-26). Supersedes the `OrderLifecycleSaga` framing of Option B in
> [`saga-pattern.md`](./saga-pattern.md#option-b--process-manager-per-saga-future-when-option-a-proves-insufficient)
> — that doc still describes a saga hardcoded to Orders; this doc captures why that's no longer
> the right shape and what should replace it.

---

## Why this doc exists (the decision that changed)

`saga-pattern.md` Option B proposed a single `OrderLifecycleSaga` process-manager entity, owned by
the Orders BC. While scoping effort for that, a second look at concrete near-term needs surfaced
**three more saga-shaped problems beyond order placement**:

1. **Refund flow** — already flagged as a saga candidate in `saga-pattern.md`'s risk table (🟢 Low
   today, but a real multi-step flow: `RefundApproved` → `ReturnStock` → `NotifyCustomer`).
2. **Cart/checkout corruption recovery** — the cart can end up in a broken/inconsistent state
   through paths other than `OrderPlacementFailed` (the one case workstream 1 just fixed); general
   cart recovery is a repeating shape, not a one-off.
3. **Inventory availability change → Checkout lock/reservation propagation** — when something goes
   wrong in the warehouse (stock adjustment, hold expiry/correction), Availability needs to change
   state and then push that change to Presale/Checkout so an existing reservation/lock shown to the
   user gets invalidated or updated. This is structurally different from the other two: it's not
   purely "failure → compensate", it's **mid-flow state change → notify a dependent step/BC**, which
   the current choreography-only model and even `saga-pattern.md`'s Option B sketch don't explicitly
   cover.

With four concrete cases (Order Placement + these three), a purpose-built `OrderLifecycleSaga`
would mean writing the same step-tracking/correlation/compensation plumbing three more times.
**Decision: build one generic saga/orchestration engine; each of the four flows is a saga
*definition* registered against it, not a bespoke implementation.**

This explicitly reverses the "do not refactor prematurely" caution in `saga-pattern.md` — that
caution was correct when only one saga candidate was known; it no longer applies with four.

---

## Requirements (agreed in conversation)

1. **No hardcoding to Orders, or to any single BC.** The engine must not know about
   `OrderId`/`PresaleUserId`/etc. — it operates on generic step/saga identifiers; BC-specific
   types stay inside each BC's saga *definition*, not inside the engine.
2. **Outbox is one generic mechanism, not a per-BC choice.** A single shared `messaging.Outbox`
   table + one generic poller/dispatcher serving every BC uniformly. No "Outbox flavor A for Sales,
   flavor B for Inventory" — this was already the direction recommended on cost grounds (7 BCs ×
   per-BC table vs. 1 shared table); the generic-concept requirement makes it non-negotiable, not
   just cheaper.
3. **The engine must support state-change propagation, not only failure-compensation.** The
   Inventory→Checkout lock-change case means a step definition needs to be able to say "this step's
   state changed, notify dependent step(s)" as a first-class transition, alongside the
   already-covered "this step failed, run compensations" transition.
4. **Existing Option A (`OrderPlacementFailed` compensation handlers, already shipped) is not
   necessarily retrofitted.** It already works and is low-risk. Whether it gets migrated onto the
   new engine or left as-is standalone choreography is an **open question** (see below), not a
   given.

---

## Known concrete saga candidates (justifying genericity)

| # | Flow | Shape | Risk (per `saga-pattern.md`, pre-existing) |
|---|---|---|---|
| 1 | **Order Placement** | PlaceOrder → Reserve Stock → Create Payment → Clean Cart; compensate via `OrderPlacementFailed` | 🔴 High — already has Option A choreography compensation (shipped) |
| 2 | **Refund** | RefundApproved → ReturnStock → NotifyCustomer | 🟢 Low today |
| 3 | **Cart/checkout corruption recovery** | General cart-recovery shape, of which `OrderPlacementFailed`→cart-restore (workstream 1) is one instance, not the only one | Not yet risk-assessed — needs the same "concrete trigger conditions" research done for workstream 1 before this is scoped |
| 4 | **Inventory availability change → Checkout lock propagation** | Availability step changes state (not necessarily a failure) → must notify Presale/Checkout to update/invalidate a reservation | Not yet risk-assessed; structurally different (propagation, not just compensation) |

Only #1 has a documented risk assessment today. **#2–#4 need their own "concrete trigger
conditions" write-ups** (mirroring what `order-placement-compensation-followup.md` did for cart
restore) before implementation of their saga *definitions* starts — this doc scopes the *engine*,
not the individual definitions.

---

## Design shape (draft — refine at implementation time)

### Schema convention (confirmed from existing code)

Every BC's `DbContext` calls `HasDefaultSchema(...)` — e.g. `profile`, `iam`, `time_management`
(`TimeManagementDbContext.cs:20`, migrations under `ECommerceApp.Infrastructure/Supporting/TimeManagement/Migrations/`
use schema `time_management` for tables `DeferredJobQueue`, `JobExecutions`, `ScheduledJobs`).
Following that convention:

- **Outbox** → new schema `messaging` (matches its home, `ECommerceApp.Application/Messaging` /
  `ECommerceApp.Infrastructure/Messaging`), table `messaging.Outbox`. One schema, one table, no
  per-BC duplication (requirement 2).
- **Saga engine** → new schema, e.g. `sagas` (cross-cutting infra, not owned by any single BC —
  same reasoning as why `messaging` isn't owned by Sales or Inventory). Tables:
  `sagas.SagaInstances`, `sagas.SagaSteps`.

### Outbox (prerequisite — workstream 2, unchanged scope from prior estimate)

- `messaging.Outbox`: `Id`, `MessageType` (assembly-qualified or a registered short type key —
  needs a serialization strategy decision), `Payload` (JSON), `CreatedAt`, `Status`
  (`Pending`/`Dispatched`/`Failed`), `DispatchedAt`, retry count.
- Generic poller/dispatcher reusing the `DeferredJobPollerService` shape
  (`ECommerceApp.Infrastructure/Supporting/TimeManagement/DeferredJobPollerService.cs:40-79`): same
  poll-loop / lock-window / zombie-recovery pattern, applied to `messaging.Outbox` instead of
  `DeferredJobQueue`. New part (nothing to reuse): deserializing `Payload` back into a concrete
  `IMessage` and calling `IModuleClient.PublishAsync` — today's `JobDispatcherService` resolves an
  `IScheduledTask` by string name, which is a simpler problem than polymorphic message
  deserialization.
- Retrofit the ~15 existing `PublishAsync` call sites (`OrderService`, `PaymentService`,
  `CouponService`, `RefundService`, `ShipmentService`, `StockService`, `StockAdjustmentJob`,
  `ProductService`, plus the 3 Inventory Shipment* leaf-republishers) so the write goes into
  `messaging.Outbox` inside the same `SaveChangesAsync` as the aggregate write, not as a separate
  `_messageBroker.PublishAsync` call afterward.

### Saga engine core (new scope vs. prior `OrderLifecycleSaga` estimate)

- `SagaInstance` (generic): `Id`, `SagaType` (string key identifying which definition), `Status`
  (`Running`/`Completed`/`Compensating`/`Failed`), `CorrelationId` (generic — e.g. the triggering
  order id, refund id, etc., stored as an opaque string/int, not a typed `OrderId`), `CreatedAt`,
  `CompletedAt`.
- `SagaStep` (generic): `Id`, `SagaInstanceId`, `StepName`, `Status`
  (`Pending`/`Completed`/`Failed`/`Compensated`), `OccurredAt`, `Payload` (JSON, step-specific
  data).
- `ISagaDefinition` (or similar): each BC registers one per saga type (`OrderPlacementSagaDefinition`,
  `RefundSagaDefinition`, `CartRecoverySagaDefinition`, `AvailabilityLockChangeSagaDefinition`) via
  DI, declaring its steps and, per step, either a compensating action or a "notify dependent step"
  action — covering requirement 3 (propagation, not just compensation).
- The engine subscribes to the relevant domain events per registered definition and drives
  `SagaInstance`/`SagaStep` transitions generically; it never references a BC-specific message type
  directly — that mapping lives in each `ISagaDefinition`.

**Open question — not decided, needs explicit resolution before Phase 2 (see below):** does the
existing, already-shipped Option A `OrderPlacementFailed` choreography get retrofitted into an
`OrderPlacementSagaDefinition` on the new engine, or does it stay as standalone handlers and the
engine is only used for the three *new* sagas? Retrofitting is more consistent but touches
already-working, tested code for no functional gain — a real regression-risk-vs-consistency
tradeoff to make explicitly, not by default.

---

## Proposed implementation phases (draft, for later — re-confirm before starting)

| Phase | Scope |
|---|---|
| **0** | Outbox pattern: `messaging.Outbox` schema/table/migration, generic poller + dispatcher, retrofit ~15 call sites, crash/restart + at-least-once integration tests |
| **1** | Saga engine core: `SagaInstance`/`SagaStep` domain + EF config/migration (`sagas` schema), `ISagaDefinition` abstraction + registration mechanism, generic transition/compensation/notify-dependent-step execution |
| **2** | Decide + implement: retrofit Option A into `OrderPlacementSagaDefinition`, or leave standalone (explicit decision required first) |
| **3** | `RefundSagaDefinition` — first genuinely new saga on the engine; validates the abstraction with a second real case |
| **4** | Concrete-trigger-conditions research (cart corruption recovery) → `CartRecoverySagaDefinition` |
| **5** | Concrete-trigger-conditions research (availability lock propagation) → `AvailabilityLockChangeSagaDefinition` — first user of the "notify dependent step" transition type, validates requirement 3 end-to-end |

Phases 3–5 should each be materially cheaper than Phase 1 once the engine exists — that's the
entire justification for building it generically. If any of them turns out *not* to be cheaper,
that's a signal the abstraction in Phase 1 was wrong and needs revisiting before continuing.

---

## Effort estimate (rough — revise once Phase 1 design is final)

- Phase 0 (Outbox, shared table): ~1 week — unchanged from prior estimate.
- Phase 1 (engine core): ~2–3 weeks — bigger than the previous purpose-built `OrderLifecycleSaga`
  estimate (2–4 weeks total) because it now includes a real abstraction layer (definition registry,
  generic transition engine) instead of one fixed entity.
- Phase 2 (Option A retrofit decision + possible migration): 2–5 days if retrofitted, ~0 if not.
- Phases 3–5 (one saga definition each, engine already exists): a few days each, *if* the
  abstraction holds. Phase 5 carries the most abstraction risk (new transition type).

Total for engine + all four concrete sagas: roughly **5–8 weeks**, weighted heavily toward Phase 1
getting the abstraction right — worth a design spike/prototype on Phase 1 alone before committing
to the full estimate.

---

## Validation criteria (check these before calling this proposal "ready to implement", and again
## after each phase — do not just trust phase-completion claims, re-verify per the pattern used for
## workstream 1)

**Before starting any implementation:**
- [ ] Phase 2's open question (retrofit Option A or not) has an explicit answer, recorded here, not
      decided implicitly during coding.
- [ ] Concrete-trigger-conditions write-ups exist for saga candidates #3 and #4 (cart corruption,
      availability lock propagation) — mirroring `order-placement-compensation-followup.md`'s
      "why it's not a rare edge case" section for workstream 1. Do not scope Phase 4/5 off vibes.
- [ ] `messaging` and `sagas` schema names are confirmed non-conflicting with any in-flight ADR
      (check `docs/adr/` for anything already claiming those names).
- [ ] Serialization strategy for Outbox `Payload` (assembly-qualified type name vs. registered short
      key) is decided — affects both Outbox and every `SagaStep.Payload`, so decide once, reuse.

**After Phase 0 (Outbox) — deterministic checks:**
- [ ] `dotnet build` + full unit/integration suite green (same three commands used for workstream 1
      validation).
- [ ] Kill-the-process-mid-dispatch integration test proves a message survives restart and is
      delivered exactly the intended number of times from the consumer's perspective (at-least-once
      + idempotent handling, or an explicit dedup key) — not just "poller runs", but a real crash
      simulation.
- [ ] Every one of the ~15 original `PublishAsync` call sites was updated — grep for direct
      `_messageBroker.PublishAsync(` calls outside the new dispatcher; any hit outside the
      dispatcher itself is a missed retrofit, flag it.
- [ ] No BC-specific `Outbox` table or schema was added anywhere — confirms requirement 2 held
      (single shared table, not a per-BC fork slipping back in under time pressure).

**After Phase 1 (engine core):**
- [ ] Grep the engine's own code (`SagaInstance`, `SagaStep`, the transition/execution logic) for
      any BC-specific type name (`OrderId`, `PresaleUserId`, `CartRestoreItem`, etc.) — a hit here
      means requirement 1 (no hardcoding) was violated; the engine must only reference its own
      generic identifiers.
- [ ] Confirm both transition kinds are actually exercised by a test each: at least one
      failure→compensate path and one state-change→notify-dependent-step path, even with a
      throwaway/test saga definition if #4's real definition isn't built yet — don't let requirement
      3 go unverified until Phase 5.

**After each of Phase 2–5 (per saga definition):**
- [ ] Confirm the phase's actual effort against the "a few days each" estimate above. If it isn't
      materially cheaper than Phase 1, stop and re-evaluate the abstraction before adding the next
      definition on top of it.
- [ ] Spec-conformance check specific to that definition (mirroring Step 3 of workstream 1's
      validation checklist) — re-derive the checklist from that saga's own concrete-trigger-conditions
      doc, don't reuse workstream 1's checklist verbatim since the domain differs.

---

## References

- [`saga-pattern.md`](./saga-pattern.md) — original analysis; Option B section here is effectively
  superseded by this doc for the *shape* of the solution (generic engine vs. `OrderLifecycleSaga`),
  but its gap analysis (Gap 1/2/3) and Option A material are still accurate and unchanged.
- [`order-placement-compensation-followup.md`](./order-placement-compensation-followup.md) —
  workstream 1 (cart restore, done) and workstream 2 (Outbox, next up) still apply; this doc's
  Phase 0 *is* workstream 2, just specified in more detail.
- [`README.md` F3](./README.md#future-architectural-considerations) — link this doc alongside
  `saga-pattern.md` once scope here stabilizes.
