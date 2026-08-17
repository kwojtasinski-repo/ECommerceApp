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
3. **Inventory availability change → Checkout reservation propagation** — when something goes
   wrong in the warehouse (stock adjustment, hold expiry/correction), Availability needs to change
   state and then push that change to Presale/Checkout so an existing reservation shown to the
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
   Inventory→Checkout reservation-change case means a step definition needs to be able to say "this step's
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
| 4 | **Inventory availability change → Checkout reservation propagation** | Availability step changes state (not necessarily a failure) → must notify Presale/Checkout to update/invalidate a reservation | Not yet risk-assessed; structurally different (propagation, not just compensation) |

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

> **Status update (2026-07-29): retrofit complete, validated PASS.** All 29 call sites across the 12
> files listed below now go through `IOutboxWriter`/`CrossContextTransactionScope`; `messaging.Outbox`,
> the generic poller, and the dispatcher are live in production. Full-solution grep for
> `_messageBroker.PublishAsync(`/`_broker.PublishAsync(` returns zero hits outside `ModuleClient.cs`/
> `InMemoryMessageBroker.cs`. See `.github/plans/03-phase-outbox-retrofit-callsites-validation.md`
> history (deleted after PASS per convention; this note is the permanent record) and
> [`order-placement-compensation-followup.md`](./order-placement-compensation-followup.md) for the
> `OrderPlacementFailed` compensation-semantics decision made during the retrofit.
> `IMessageBroker`/`AsyncMessageDispatcher`/`BackgroundMessageDispatcher`/`MessageChannel` were
> deliberately left in place, unmodified — they're dead-code cleanup candidates for a future, separate,
> low-risk phase, not addressed here. **Next up: Phase 4 (Inbox / consumer-side idempotency, below).**

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
- Retrofit the existing `PublishAsync` call sites so the write goes into `messaging.Outbox` inside
  the same transaction as the aggregate write, not as a separate `_messageBroker.PublishAsync` call
  afterward. **Recounted precisely (2026-07-27, grep of `ECommerceApp.Application` for
  `PublishAsync(`) — 29 call sites across 12 files**, not the ~15 originally estimated:
  `OrderService.cs` (5), `StockService.cs` (6), `ProductService.cs` (3), `CouponService.cs` (2),
  `RefundService.cs` (2), `ShipmentService.cs` (4), `PaymentService.cs` (1),
  `PaymentWindowExpiredJob.cs` (1), `StockAdjustmentJob.cs` (1), and the 3 Inventory leaf-republishers
  (`ShipmentDeliveredHandler.cs`, `ShipmentFailedHandler.cs`, `ShipmentPartiallyDeliveredHandler.cs`,
  1 each). Re-grep at Phase 0 start in case new call sites landed since this count.

### Inbox / consumer-side idempotency (new — required once Outbox introduces at-least-once delivery)

Today's synchronous, in-process broker delivers each message exactly once per process lifetime —
there is no redelivery path, so no handler in this codebase currently needs to tolerate being
called twice for the same logical event. **The Outbox poller changes that.** If the poller
dispatches a message, the process crashes before the `Outbox` row is marked `Dispatched`, and it
restarts, the same row is picked up again — every registered handler for that message type runs a
second time. Handlers that mutate state without checking prior occurrence (`StockService`
decrementing quantity, `PaymentService` creating a payment record, Coupons incrementing usage
count) would double-apply. This is not a hypothetical — it is the direct consequence of moving from
synchronous in-process delivery to a poll/retry dispatcher, and Phase 0 is not complete without
addressing it (see the existing but under-specified validation line 193-194 below: "at-least-once +
idempotent handling, or an explicit dedup key" — this subsection is that specification).

**Key design constraint, and why it's cheaper than it first looks:** Outbox is single-writer — one
`SaveChangesAsync` on the publishing BC's own `DbContext` can atomically include the Outbox insert.
Inbox/dedup is multi-*consumer* — each handler lives in a different BC and writes to that BC's own
`DbContext`. Naively this looks like it needs a distributed transaction to keep a shared dedup table
atomic with each consumer's own write. **Verified this is not the case here**: every BC's `DbContext`
(`OrdersDbContext`, `AvailabilityDbContext`, `PaymentsDbContext`, etc.) points at the same
`ConnectionStrings:DefaultConnection` — one physical SQL Server database, split by schema, not
separate databases (confirmed across all `Extensions.cs` registrations, e.g.
`ECommerceApp.Infrastructure/Sales/Orders/Extensions.cs:20`,
`ECommerceApp.Infrastructure/Inventory/Availability/Extensions.cs:21`). Two `DbContext` instances on
the same physical connection can share one local ADO.NET transaction
(`context1.Database.BeginTransaction()` → `context2.Database.UseTransaction(tx.GetDbTransaction())`)
with no MSDTC/2PC involved — this is a standard, supported EF Core technique, not something novel
being introduced for this feature.

**Recommended shape:** one shared `messaging.Inbox` table (not per-BC-schema — same reasoning as
requirement 2 for Outbox: one shared mechanism beats N per-BC forks). Columns: `MessageId` (the
`messaging.Outbox.Id` of the delivered row), `HandlerType` (string — one BC can register multiple
handlers for the same message type, each needs its own dedup slot), `ProcessedAt`. Unique constraint
on `(MessageId, HandlerType)`.
- The handler's own logic wraps its existing work **and** the `messaging.Inbox` insert in one shared
  local transaction spanning the handler's own `DbContext` and `MessagingDbContext` (the same
  connection-sharing technique used for the Outbox write in the retrofit — Phase 0 builds this
  helper once, both Outbox-write and Inbox-check reuse it), then a single `SaveChangesAsync` per
  context inside that transaction. Insert-or-skip via a try/catch on the unique-constraint violation
  (`DbUpdateException`), matching how this codebase already treats unique-constraint races elsewhere
  (verify against `efcore.instructions.md` conventions before implementing — do not invent a new
  race-handling idiom).
- **Cheaper alternative, worth ruling in/out explicitly rather than defaulting to the above:** for
  handlers that are already naturally idempotent by construction (e.g. a handler that only
  overwrites a value to an absolute target rather than incrementing/decrementing it), no dedup
  table is needed at all — the redelivery is harmless. Audit the 29 retrofitted call sites'
  *consumers* (not the publishers) once Phase 0 starts: some may already qualify, shrinking the
  number of BCs that actually need a `ProcessedMessages` table.

**Open question — not decided, resolve before Phase 0 implementation starts:** which consumer
handlers are naturally idempotent (skip the dedup insert, cheaper) vs. genuinely need a
`messaging.Inbox` row. Do not default every handler into writing one "to be safe" — that's needless
write volume for a problem some handlers don't have; equally, do not skip the audit and assume none
need it.

**Resolved (Phase 4, 2026-08-02) — PASS.** Full audit of all 48 registered consumer handlers across
every Phase-3-retrofitted message type: **26 need dedup, 22 are naturally idempotent** (a small
correction was needed mid-phase — a Refund flow added after the first audit pass added 8 previously
unaudited handlers; all 8 were classified and folded in). 2 of the 26 (`OrderCouponAppliedHandler`,
`OrderPriceAdjustedHandler` — audit-trail-duplication only, no financial impact) plus
`Inventory.Availability.Handlers.OrderShippedHandler` (turned out to be dead/unregistered code,
superseded by Fulfillment's shipment handlers per ADR-0017 §13.3) were explicitly excluded from
wiring — the first 2 as placeholders slated for a future event-subscription rewrite, the last because
it can never be invoked. The remaining 23 are wired and each has a passing `DuplicateDeliveryTests`
integration test that redelivers the same Outbox row twice and asserts the side effect changed
exactly once.

**Mechanism** (option (a) from this section — per-handler, not dispatcher-level): a new additive
`IIdAwareMessageHandler<TMessage>` interface; `IModuleClient.PublishAsync`/`OutboxDispatcher` carry the
originating `OutboxMessage.Id` through to handlers that opt in; each dedup-needing handler injects
`IProcessedMessageGuard` and wraps its existing logic. Two guard overloads ended up necessary, not
foreseen when this doc was written: one anchored on a BC's own `IOutboxTransaction` (matches this
section's original design), and a second, transaction-less one for handlers with no BC `DbContext` of
their own (Supporting/Communication's 12 email/notification handlers — pure external side effects).

**Known residual weakness, not fixed by this phase, flagged for whoever picks up Inventory next**:
`StockService.FulfillAsync`'s only guard checks the product's *aggregate* `ReservedQuantity` across
all orders, not the specific hold being fulfilled. Inbox dedup correctly prevents *redelivery* of the
same message from double-fulfilling; it does not fix that guard for a genuinely concurrent second
order on the same product hitting the same aggregate headroom. Tracked here, not silently treated as
resolved.

### Outbox (and Inbox) cleanup job — reuse the existing TimeManagement recurring-job pattern

Once `messaging.Outbox` rows reach `Dispatched`, they're audit trail, not live data — same relationship
`DeferredJobQueue` has to `JobExecutions` today. Left unbounded, `Outbox` (and any per-BC
`ProcessedMessages` tables from the Inbox subsection above) grow forever. This needs the same kind
of recurring purge `RefreshTokenCleanupTask` already does for expired refresh tokens — no new
infrastructure pattern, just another instance of the existing one.

- **New `IScheduledTask`**: `OutboxCleanupTask` (`ECommerceApp.Application/Messaging/Services/`,
  mirroring `RefreshTokenCleanupTask`'s shape exactly —
  `ECommerceApp.Application/Identity/IAM/Services/RefreshTokenCleanupTask.cs:9-32`): constructor-
  injected repository, `TaskName => "OutboxCleanup"`, `ExecuteAsync` wraps the delete in
  try/catch → `context.ReportSuccess`/`ReportFailure`, never throws out of the task.
- **Retention threshold is appsettings-configurable, not hardcoded**, mirroring `PresaleOptions`
  (`ECommerceApp.Application/Presale/Checkout/PresaleOptions.cs:6-20` — `TimeSpan` properties bound
  from config with an `IValidateOptions<T>` guard): new `MessagingOptions.OutboxRetention` (`TimeSpan`,
  default 14 days per your ask — "co 2 tygodnie" — but make it a config value, not a literal
  `TimeSpan.FromDays(14)` in the task itself), section name `"Messaging"`, with a validator rejecting
  zero/negative values the same way `PresaleOptionsValidator` does.
- **Only delete `Dispatched` rows older than the retention window.** Never delete `Pending` or
  `Failed` rows regardless of age — a `Failed` row sitting past retention is a signal something is
  stuck, not garbage; surfacing that (e.g. an alert/metric) is a separate concern from cleanup, not
  something this task should silently paper over by deleting the evidence.
- **Recurring schedule**, not a one-off: registered as a `ScheduledJob` the same way the existing
  `CurrencyRateSync` job is (`ScheduledJob.Create(jobName, cronExpression, timeZoneId, maxRetries)`,
  wired through `JobManagementService.Create` —
  `ECommerceApp.Application/Supporting/TimeManagement/Services/JobManagementService.cs:126`). Daily
  cadence (e.g. `"0 3 * * *"`) is a separate knob from the retention `TimeSpan` above — don't conflate
  "how often it runs" with "how old something must be to delete"; both should be independently
  configurable.
- **DI registration**: `services.AddScoped<IScheduledTask, OutboxCleanupTask>()` alongside
  `MessagingOptions` binding, in whichever `Extensions.cs` owns Messaging DI registration today —
  same one-line pattern as IAM's `Extensions.cs:12`.
- If the Inbox subsection above lands per-BC `ProcessedMessages` tables, each such BC needs its own
  equivalent cleanup task (or the existing task iterates a registered list of cleanup targets) — scope
  this once the Inbox open question is resolved, don't guess the shape now.

**Test plan (mirrors `RefreshTokenCleanupTaskTests.cs`,
`ECommerceApp.UnitTests/Identity/IAM/RefreshTokenCleanupTaskTests.cs`):** delete-older-than-retention
happy path; rows exactly at the boundary are not deleted (off-by-one check); `Pending`/`Failed` rows
are never touched regardless of age; repository throwing is caught and reported as failure, not
propagated.

### Saga engine core (new scope vs. prior `OrderLifecycleSaga` estimate)

- `SagaInstance` (generic): `Id`, `SagaType` (string key identifying which definition), `Status`
  (`Running`/`Completed`/`Compensating`/`Failed`), `CorrelationId` (generic — e.g. the triggering
  order id, refund id, etc., stored as an opaque string/int, not a typed `OrderId`), `CreatedAt`,
  `CompletedAt`.
- `SagaStep` (generic): `Id`, `SagaInstanceId`, `StepName`, `Status`
  (`Pending`/`Completed`/`Failed`/`Compensated`), `OccurredAt`, `Payload` (JSON, step-specific
  data).
- `ISagaDefinition` (or similar): each BC registers one per saga type (`OrderPlacementSagaDefinition`,
  `RefundSagaDefinition`, `CartRecoverySagaDefinition`, `AvailabilityReservationChangeSagaDefinition`) via
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
| **0** | **Done (2026-08-02), validated PASS.** Outbox pattern: `messaging.Outbox` schema/table/migration, generic poller + dispatcher, retrofit 29 call sites, crash/restart + at-least-once integration tests; consumer-side Inbox/idempotency audit + shared `messaging.Inbox` table where needed; `OutboxCleanupTask`/`InboxCleanupTask` recurring purge jobs (appsettings-configurable retention + enable/disable + dynamic cron). All 5 phases (schema, poller/dispatcher, retrofit, Inbox, cleanup) complete — see `order-placement-compensation-followup.md` workstream 2. |
| **1** | Saga engine core: `SagaInstance`/`SagaStep` domain + EF config/migration (`sagas` schema), `ISagaDefinition` abstraction + registration mechanism, generic transition/compensation/notify-dependent-step execution |
| **2** | Decide + implement: retrofit Option A into `OrderPlacementSagaDefinition`, or leave standalone (explicit decision required first) |
| **3** | `RefundSagaDefinition` — first genuinely new saga on the engine; validates the abstraction with a second real case |
| **4** | Concrete-trigger-conditions research (cart corruption recovery) → `CartRecoverySagaDefinition` |
| **5** | Concrete-trigger-conditions research (availability reservation propagation) → `AvailabilityReservationChangeSagaDefinition` — first user of the "notify dependent step" transition type, validates requirement 3 end-to-end |

Phases 3–5 should each be materially cheaper than Phase 1 once the engine exists — that's the
entire justification for building it generically. If any of them turns out *not* to be cheaper,
that's a signal the abstraction in Phase 1 was wrong and needs revisiting before continuing.

---

## Effort estimate (rough — revise once Phase 1 design is final)

- Phase 0 (Outbox, shared table + Inbox audit/dedup + cleanup job): estimated ~1.5–2 weeks, delivered
  as 5 incremental phases across roughly 2026-07-26 to 2026-08-02 (schema/poller/dispatcher, 6 retrofit
  slices, Inbox audit + 23 handlers wired, cleanup jobs) — actual effort tracked closely with the
  estimate. Two corrections surfaced mid-flight, both resolved and recorded in the phase files before
  they were deleted: the Inbox audit initially missed 8 handlers from a Refund flow added after the
  first audit pass (folded in), and `OutboxPollerService`'s poll interval was briefly wired through a
  disconnected `IOptions<MessagingOptions>` that would have silently ignored appsettings/env overrides
  in production — caught during independent validation, fixed to match the rest of Messaging's
  established manually-bound-singleton pattern before merge.
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
      availability reservation propagation) — mirroring `order-placement-compensation-followup.md`'s
      "why it's not a rare edge case" section for workstream 1. Do not scope Phase 4/5 off vibes.
- [ ] `messaging` and `sagas` schema names are confirmed non-conflicting with any in-flight ADR
      (check `docs/adr/` for anything already claiming those names).
- [ ] Serialization strategy for Outbox `Payload` (assembly-qualified type name vs. registered short
      key) is decided — affects both Outbox and every `SagaStep.Payload`, so decide once, reuse.

**After Phase 0 (Outbox) — deterministic checks — all confirmed 2026-08-02:**
- [x] `dotnet build` + full unit/integration suite green (same three commands used for workstream 1
      validation). Re-verified independently: build 0 errors, UnitTests 1072/1072, IntegrationTests
      245/245 (run 3 times in a row after the parallel-collections change below, zero flakiness).
- [x] Kill-the-process-mid-dispatch integration test proves a message survives restart and is
      delivered exactly the intended number of times from the consumer's perspective (at-least-once
      + idempotent handling, or an explicit dedup key) — not just "poller runs", but a real crash
      simulation. (Phase 3's crash-recovery tests.)
- [x] Every one of the 29 original `PublishAsync` call sites was updated — grep for direct
      `_messageBroker.PublishAsync(` calls outside the new dispatcher; any hit outside the
      dispatcher itself is a missed retrofit, flag it. (Phase 3, all 6 slices.)
- [x] No BC-specific `Outbox` table or schema was added anywhere — confirms requirement 2 held
      (single shared table, not a per-BC fork slipping back in under time pressure).
- [x] The Inbox audit was actually performed and recorded (which consumer handlers got a
      `ProcessedMessages` table vs. were judged naturally idempotent, and why) — not skipped, not
      "we'll add dedup later if it becomes a problem." 48 handlers audited (26 need dedup, 22
      naturally idempotent) — see the "Resolved (Phase 4)" note above.
- [x] A duplicate-delivery integration test exists per handler that got a `ProcessedMessages` table:
      deliver the same Outbox row twice, assert the handler's side effect (stock decrement, payment
      record, coupon usage count, etc.) happened exactly once. All 23 in-scope handlers covered.
- [x] `OutboxCleanupTask` exists, is registered, and its retention is read from `MessagingOptions`
      (config), not a hardcoded literal — confirm by changing the config value in a test and
      observing the cutoff shift. `MessagingScheduledJobReconcilerIntegrationTests` proves the dynamic
      part end-to-end (asserts the `ScheduledJobs.Schedule` DB column actually changes after a second
      reconcile with different config, not just "no exception").
- [x] `OutboxCleanupTask` never deletes `Pending` or `Failed` rows regardless of age — test asserts
      this explicitly, not just "some old rows were deleted." Confirmed by reading
      `OutboxRepository.DeleteDispatchedOlderThanAsync`'s query directly: `WHERE Status == Dispatched
      AND DispatchedAt < cutoff` — no other status is ever touched.

**Test-suite speedup, done alongside Phase 5 (not scoped by this doc, flagged here since it touches
the same integration-test infrastructure this checklist relies on):** `ECommerceApp.IntegrationTests`
now runs with `xunit.runner.json`'s `parallelizeTestCollections: true` (previously `false`) plus a
configurable `OutboxPollerService` poll interval (test override: 100ms vs. production's unchanged
10s default) — full suite dropped from ~95s to ~17-21s. Verified safe: ran the full suite 3
consecutive times post-change with zero flakiness. One latent race was found and fixed during that
verification, not by the change's own author: `BcWebApplicationFactory`'s IAM-database seeding (added
during Phase 4's own test-infra fix) had no try/catch around it, unlike the equivalent seeding call in
`CustomWebApplicationFactory` — under real parallel test-class construction, two hosts can now race to
seed the same fixed-Id test users in the one shared `"InMemoryIamDatabase"` name, and the loser would
have crashed its own host startup. Now caught and logged, matching the established pattern, instead of
left as a hope-it-doesn't-happen gap.

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
