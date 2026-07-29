## Plan: Inbox / consumer-side idempotency — `messaging.Inbox` dedup for at-least-once redelivery

### Scope
- **BC(s)**: whichever consumer handlers the audit (Step 1 below) determines need dedup — likely
  Inventory (`StockService`-driven handlers), Payments, Coupons, at minimum; audit decides the full list
- **Governing doc**: [`generic-saga-orchestration-proposal.md`](../../docs/roadmap/generic-saga-orchestration-proposal.md)
  §"Inbox / consumer-side idempotency"
- **Risk**: medium — wrong here means silent double-processing (double stock decrement, duplicate
  payment record) under a redelivery, which is a real data-correctness bug, not just noise. But it's
  additive (a new table + new inserts alongside existing handler logic), not a rewire of an existing
  path — lower blast radius than Phase 3.
- **Behavioral change**: yes, for audited-as-needed handlers — they gain a dedup check before their
  existing logic runs.

### Prerequisite
Phases 1–3 complete and validated PASS. Outbox is live; redelivery is now a real (if rare —
crash-window-sized) possibility for every retrofitted message type.

### Why this phase is scoped separately from Phase 3
Phase 3 made delivery durable (**at-least-once**). This phase is what actually closes the loop to
**effectively-once** processing for handlers that need it. Doing this as its own phase, after the
retrofit is proven, means the audit (Step 1) can be done against real, working message flows instead
of guessing ahead of time which handlers matter.

### Step 1 — the audit (do this first, before writing any dedup code)
For each of the message types retrofitted in Phase 3, examine every registered
`IMessageHandler<T>` and classify it:
- **Naturally idempotent** (redelivery is harmless) — e.g. a handler that unconditionally sets a value
  to an absolute target rather than incrementing/decrementing it, or that's already guarded by a
  domain invariant that makes a second call a no-op. Document *why* per handler, don't just assert it.
- **Needs dedup** — any handler that increments/decrements a counter or quantity (`StockService`
  reacting to a message that decrements `AvailableQuantity`), creates a new row per invocation without
  a natural uniqueness constraint (a payment-creation handler), or has any other side effect that isn't
  safe to run twice.

Record the classification in a table in this file (or a linked doc) before writing any code — this is
the deliverable that answers the open question `generic-saga-orchestration-proposal.md` flags
("which consumer handlers are naturally idempotent... vs. genuinely need a `messaging.Inbox` row").
**Do not default every handler into having dedup "to be safe"** (needless write volume for handlers
that don't need it) and **do not skip auditing any handler** (an unaudited handler is an unverified
assumption, not a decision).

#### Audit result (2026-07-29, all 40 registered handler classes across the 20 Phase-3 message types)

Every `IMessageHandler<T>` implementation for a Phase-3-retrofitted message type was read (including the
domain/aggregate method it delegates to, not just the handler shell) and classified below. 3 registered
message types have no handler yet (`FulfillmentRefundApproved`, `FulfillmentRefundRejected`,
`StockReconciliationRequired`) — nothing to audit there.

**Catalog**

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `ProductCacheInvalidationHandler` | ProductUpdated/Published/Unpublished | Naturally idempotent | Pure `IMemoryCache.Remove(key)` eviction. |

**Presale/Checkout**

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `ProductDetailsCacheInvalidationHandler` | ProductUpdated/Published/Unpublished | Naturally idempotent | Same cache-eviction pattern. |
| `StockAvailabilityChangedHandler` | StockAvailabilityChanged | Naturally idempotent | Upsert-by-`ProductId` sets absolute `AvailableQuantity`. |
| `OrderPlacedHandler` (Presale) | OrderPlaced | Naturally idempotent | Deletes cart/soft-reservations by key — no-op if already gone. |
| `OrderPlacementFailedHandler` (Presale) | OrderPlacementFailed | Naturally idempotent | `CartService.RestoreAsync` upserts to an absolute quantity per product, not additive. |

**Inventory/Availability**

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `ProductPublishedHandler` | ProductPublished | Naturally idempotent | Upsert sets `Orderable` status absolute. |
| `ProductUnpublishedHandler` | ProductUnpublished | Naturally idempotent | Upsert sets `Suspended` status absolute. |
| `PaymentConfirmedHandler` | PaymentConfirmed | Naturally idempotent | `StockHold.Confirm()` guard blocks `Released/Fulfilled/Withdrawn`; re-confirming a `Confirmed` hold is a same-value no-op, no quantity touched. |
| `PaymentExpiredHandler` | PaymentExpired | Naturally idempotent | `ReleaseAsync`'s `stockHold.IsGuaranteed` check flips false after the first release — redelivery skips the quantity decrement. |
| `OrderPlacementFailedHandler` (Inventory) | OrderPlacementFailed | Naturally idempotent | Same `IsGuaranteed`-gated `ReleaseAsync` path. |
| `OrderCancelledHandler` | OrderCancelled | Naturally idempotent | Same `IsGuaranteed`-gated `ReleaseAsync` path. |
| `ShipmentFailedHandler` | ShipmentFailed | Naturally idempotent (edge case flagged) | Core `ReleaseAsync` path safe; but the "hold not found" branch re-enqueues `StockReconciliationRequired` with no guard — a redelivery hitting that branch double-publishes the reconciliation message. Low severity (ops alert, not a financial/quantity bug) but not zero. |
| `ShipmentDeliveredHandler` | ShipmentDelivered | **Needs dedup** | `FulfillAsync`'s only guard (`quantity > stock.ReservedQuantity.Value`) checks the product's aggregate reserved quantity across *all* orders, not this specific hold — under concurrent orders on the same product, redelivery can pass the guard again and double-decrement. `stockHold.MarkAsFulfilled()` has no already-fulfilled guard either. |
| `ShipmentPartiallyDeliveredHandler` | ShipmentPartiallyDelivered | **Needs dedup** | Delivered-items loop calls `FulfillAsync` (same weakness as above); failed-items loop's `ReleaseAsync` is safe, but the `Fulfill` half makes the whole handler unsafe. |
| `OrderShippedHandler` | OrderShipped | **Needs dedup** | Same `FulfillAsync` aggregate-guard weakness. |
| `OrderPlacedHandler` (Inventory) | OrderPlaced | **Needs dedup** | `ReserveAsync` unconditionally decrements `AvailableQuantity` and creates a brand-new `StockHold` row every call — no existing-hold check, no unique constraint. The plan's textbook case. |

**Sales/Orders**

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `OrderPaymentConfirmedHandler` | PaymentConfirmed | Naturally idempotent | Guards `Status != Placed → return` before `ConfirmPayment()`; already-`PaymentConfirmed` short-circuits. |
| `OrderPaymentExpiredHandler` | PaymentExpired | Naturally idempotent | Same `Status != Placed → return` guard before `ExpirePayment()`. |
| `OrderCouponRemovedHandler` | CouponRemovedFromOrder | Naturally idempotent | `RemoveCouponAsync` returns `CouponNotAssigned` if `CouponUsedId is null` — full no-op on redelivery, no event appended either. |
| `OrderShipmentDeliveredHandler` | ShipmentDelivered | Naturally idempotent | `MarkAsDeliveredAsync` explicitly returns `AlreadyDelivered` if `Status == Fulfilled` — blocks re-execution and re-publishing `OrderShipped` on redelivery. |
| `OrderPlacedSnapshotHandler` | OrderPlaced | Naturally idempotent | Updates existing `OrderItem` rows in place (`SetSnapshot`), no inserts — overwrite to the same resolved values. |
| `OrderCouponAppliedHandler` | CouponApplied | **Needs dedup** (lower severity) | `AssignCoupon` overwrites `CouponUsedId`/`DiscountPercent` to the same value on redelivery (financially consistent) but unconditionally appends another `CouponApplied` `OrderEvent` row — audit-trail duplication, not a monetary bug, but no natural-key guard exists. |
| `OrderPriceAdjustedHandler` | OrderPriceAdjusted | **Needs dedup** (lower severity) | `AdjustPrice` sets `Cost` absolute (safe) but always appends a duplicate `PriceAdjusted` event — same audit-trail nuance as above. |
| `OrderShipmentDispatchedHandler` | ShipmentDispatched | **Needs dedup** | `RecordShipmentDispatched`'s guard (`Status != PaymentConfirmed → return`) never changes `Status`, so it passes identically on every redelivery and appends a duplicate event each time. |
| `OrderShipmentFailedHandler` | ShipmentFailed | **Needs dedup** | `RecordShipmentFailure` has no guard at all — always appends a new event. |
| `OrderShipmentPartiallyDeliveredHandler` | ShipmentPartiallyDelivered | **Needs dedup** | `MarkAsPartiallyFulfilled`'s guard allows `{PaymentConfirmed, PartiallyFulfilled}` — since the method transitions *into* `PartiallyFulfilled` and that's itself an allowed starting state, redelivery of the same shipment passes again and appends a duplicate event. |

**Sales/Payments**

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `OrderPlacedHandler` (Payments) | OrderPlaced | **Needs dedup** | `Payment.Create` + `AddAsync` unconditionally creates a new `Payment` row per call — no existing-payment-for-order check. The plan's own "payment-creation handler" example. |
| `OrderPlacementFailedHandler` (Payments) | OrderPlacementFailed | **Needs dedup** | `Payment.Cancel()` throws `DomainException` if `Status != Pending` — redelivery finds the payment already `Cancelled` and **throws** rather than no-op-ing. Unsafe to run twice (crashes redelivery, doesn't just waste a write). |

**Sales/Coupons**

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `CouponsPaymentExpiredHandler` | PaymentExpired | Naturally idempotent | Deletes the `CouponUsed` row after releasing the coupon; redelivery's `FindAllByOrderIdAsync` returns empty (row already gone) and returns early before touching `Coupon.Release()`/`MarkAsReversed()` (both would otherwise throw on a second call). The deletion is the guard. Verified directly (spot-checked). |
| `CouponsOrderCancelledHandler` | OrderCancelled | Naturally idempotent | Identical delete-as-guard logic to `CouponsPaymentExpiredHandler`. |

**Supporting/Communication** — all irreversible external side effects with zero redelivery guard, except the two log-only handlers:

| Handler | Message type(s) | Classification | Reasoning |
|---|---|---|---|
| `ShipmentFailedNotificationHandler` | ShipmentFailed | Naturally idempotent | `ILogger` only — a duplicate log line is harmless. |
| `ShipmentPartiallyDeliveredNotificationHandler` | ShipmentPartiallyDelivered | Naturally idempotent | Logging only, same as above. |
| `PaymentConfirmedEmailHandler` | PaymentConfirmed | **Needs dedup** | Unconditional email send. |
| `PaymentConfirmedNotificationHandler` | PaymentConfirmed | **Needs dedup** | Unconditional notification creation. |
| `PaymentExpiredEmailHandler` | PaymentExpired | **Needs dedup** | Unconditional email send. |
| `PaymentExpiredNotificationHandler` | PaymentExpired | **Needs dedup** | Unconditional notification creation. |
| `OrderPlacedEmailHandler` | OrderPlaced | **Needs dedup** | Unconditional email send. |
| `OrderPlacedNotificationHandler` | OrderPlaced | **Needs dedup** | Unconditional notification creation. |
| `OrderCancelledEmailHandler` | OrderCancelled | **Needs dedup** | Unconditional email send. |
| `OrderCancelledNotificationHandler` | OrderCancelled | **Needs dedup** | Unconditional notification creation. |

**Summary: 40 handlers audited — 19 need dedup, 21 are naturally idempotent.** Of the 19: 4 are
Inventory stock-quantity bugs (the `FulfillAsync` aggregate-guard weakness, shared root cause across 3
handlers, plus `OrderPlacedHandler`'s new-hold-per-call), 2 are Sales/Orders audit-trail-duplication
(lower severity, no financial/quantity impact), 3 are Sales/Orders event-duplication with no guard at
all, 2 are Sales/Payments (one real double-write risk, one throws-on-redelivery), and 8 are Communication
send-once handlers (uniform fix shape: wrap in the dedup guard, no domain nuance).

**Method note**: this audit was produced by a subagent doing a full read-and-classify pass over all 40
handlers, then independently spot-checked by re-reading a sample (`CouponsPaymentExpiredHandler`, the
`StockService.FulfillAsync`/`Order` domain methods already read earlier in the session) — findings held
up under spot-check. Treat as a strong first pass; a second pair of eyes on the 2-3 flagged-ambiguous
rows before wiring code is still worthwhile, per the plan's own validation checklist.

### PAUSED HERE (2026-07-29) — resume point for the next session

Work stops after the audit above. **Nothing past this point has started** — no design decision made, no
`ProcessedMessage`/`IProcessedMessageGuard` code written, no handler touched. The next session should
pick up at Step 2 below ("resolve the message-id question"), not re-derive the audit.

**Agreed division of labor for when this resumes** (decided in conversation, not yet executed):

| Who | What | Why |
|---|---|---|
| Claude (this session or a fresh one) | Step 2 design decision (how a handler learns its `OutboxMessage.Id` — option (a) dispatcher-level vs (b) per-handler); the new infra (`ProcessedMessage`, `IProcessedMessageGuard`/`ProcessedMessageGuard`, EF config, migration); the 2 Sales/Payments handlers; the 4 Inventory handlers | These need judgment, not transcription. The 4 Inventory handlers in particular share a root cause worth a real decision: `StockService.FulfillAsync`'s guard checks the product's *aggregate* reserved quantity, not this specific hold — wrapping all 4 call sites in an Inbox dedup check only prevents *redelivery* of the *same* message from re-triggering the bug; it does not fix the underlying guard weakness, which could still misfire from an unrelated concurrent order. Decide explicitly whether to fix `FulfillAsync`'s guard itself (tightening it to check the specific hold, not just the aggregate) as part of this phase, or to accept Inbox-dedup as a narrower fix and track the guard weakness separately — do not silently paper over it. | One reference handler should be fully wired + tested end-to-end first, to serve as the literal template for the gpt-5-mini prompts below. |
| gpt-5-mini (once the above exists) | Two prompt batches, mirroring the granularity and style of the existing `gpt5mini-phase3-slice*-prompt.md` files in the repo root (self-contained, "run everything yourself, don't stop to ask permission", literal code blocks to paste, explicit do-not-touch lists, mandatory `dotnet build`/`dotnet test` validation with pasted real output): (1) the 8 Communication handlers (`PaymentConfirmedEmailHandler`/`...NotificationHandler`, `PaymentExpiredEmailHandler`/`...NotificationHandler`, `OrderPlacedEmailHandler`/`...NotificationHandler`, `OrderCancelledEmailHandler`/`...NotificationHandler`) in **one** prompt, since the fix shape is identical across all 8 — writing 8 separate prompts would be pure ceremony; (2) the 3 Sales/Orders handlers with no guard at all (`OrderShipmentDispatchedHandler`, `OrderShipmentFailedHandler`, `OrderShipmentPartiallyDeliveredHandler`) as a second prompt. The 2 lower-severity audit-trail-duplication handlers (`OrderCouponAppliedHandler`, `OrderPriceAdjustedHandler`) can ride along with batch (2) or be deferred — not urgent. | Every gpt-5-mini slice in Phase 3 needed an independent review pass afterward — once caught a real bug (a missing `await using`/transaction disposal that leaked a DB connection) and once caught a handler that stopped mid-task instead of finishing unattended. Budget a review pass per batch here too; do not skip it just because the fix shape looks trivial. |

**Concrete next actions, in order**: (1) make the Step 2 design decision explicitly (see above) and record
it here before writing any code; (2) build the new infra + wire it into one reference handler (recommend
`OrderPlacedHandler` in Sales/Payments — highest-stakes of the "needs dedup" set, and a clean single-row-create
case that makes a good template); (3) write the two gpt-5-mini prompt batches per the table above; (4) run
each batch, review independently, fix, merge; (5) full validation pass per the checklist file before
declaring Phase 4 done.

### Verified facts this plan relies on
- All BCs share one physical database (`ConnectionStrings:DefaultConnection`, confirmed in Phase 1) —
  this is what makes **one shared `messaging.Inbox` table** viable (not per-BC-schema), using the same
  `CrossContextTransactionScope` technique Phase 1 built and Phase 3 already proved works in production
  traffic.
- `MessageTypeRegistry` and `OutboxMessage.Id` (the `long` primary key from Phase 1) give a stable,
  unique `MessageId` to dedup against — the same `Id` a redelivered row still carries (redelivery
  re-dispatches the *same* `Outbox` row, it doesn't create a new one), so `(OutboxMessage.Id,
  HandlerType)` is a correct, stable dedup key across the redelivery.

### Files to add
| File | Purpose |
|---|---|
| `ECommerceApp.Domain/Messaging/ProcessedMessage.cs` | Minimal entity: `MessageId` (`long`, the `OutboxMessage.Id`), `HandlerType` (`string`, e.g. `typeof(TheHandler).FullName`), `ProcessedAt` (`DateTime`). Composite key `(MessageId, HandlerType)` — no separate surrogate `Id` needed, this is a pure dedup marker, not a rich aggregate (mirrors `JobExecution`'s "append-only record, no FK ceremony" treatment more than `ScheduledJob`'s). Static factory `Create(long messageId, string handlerType)` only — no state transitions, it either exists or doesn't. |
| `ECommerceApp.Application/Messaging/IProcessedMessageGuard.cs` | `Task<bool> TryMarkProcessedAsync(long messageId, string handlerType, CrossContextTransactionScope scope, CancellationToken ct = default)` — returns `true` if this is the first time (safe to proceed with the handler's real work), `false` if a row already exists for this `(messageId, handlerType)` (skip — already processed). |
| `ECommerceApp.Infrastructure/Messaging/Configurations/ProcessedMessageConfiguration.cs` | `IEntityTypeConfiguration<ProcessedMessage>`, `ToTable("Inbox")` (schema `messaging`, same `MessagingDbContext` from Phase 1 — add `DbSet<ProcessedMessage> Inbox` to it), composite key via `HasKey(x => new { x.MessageId, x.HandlerType })`. |
| `ECommerceApp.Infrastructure/Messaging/ProcessedMessageGuard.cs` | Implements `IProcessedMessageGuard`: attempts `scope.CreateSecondaryContext<MessagingDbContext>().Inbox.Add(ProcessedMessage.Create(...))` + that context's `SaveChangesAsync`; catches the unique-constraint `DbUpdateException` specifically (verify the exact exception shape/inner-exception check convention already used elsewhere in this codebase for constraint races — reuse it, don't invent a new one) and returns `false` on that specific conflict; returns `true` on success. Any *other* exception propagates (a real DB error is not the same thing as "already processed"). |
| `ECommerceApp.Infrastructure/Messaging/Migrations/` | New migration adding the `Inbox` table to `MessagingDbContext` (`dotnet ef migrations add AddInboxTable --context MessagingDbContext ...`). |

### Files to modify
- Each handler classified "needs dedup" in Step 1's audit: inject `IProcessedMessageGuard`, wrap the
  handler's existing logic:
  ```csharp
  public async Task HandleAsync(TMessage message, CancellationToken ct = default)
  {
      await using var scope = new CrossContextTransactionScope(_myDbContext);
      if (!await _guard.TryMarkProcessedAsync(outboxMessageId, GetType().FullName, scope, ct))
      {
          return; // already processed this exact delivery
      }
      // ... existing handler logic, using `scope` for its own DbContext work so the
      // dedup marker and the handler's actual side effect commit together ...
      await scope.CommitAsync();
  }
  ```
  **Open question to resolve per-handler, not assumed uniform**: how does a handler get the
  `OutboxMessage.Id` it's currently processing? `IMessageHandler<T>.HandleAsync(TMessage message, ...)`
  today only receives the deserialized message, not the Outbox row's `Id`. This likely needs a small
  Phase 2 amendment (out of scope to redo Phase 2, but flag as a **dependency this phase adds
  backward**): either (a) `IModuleClient.PublishAsync` gains an overload/parameter carrying the
  originating `OutboxMessage.Id` for handlers that want it, or (b) dedup happens one layer up, in
  `OutboxDispatcher` itself, checking `(OutboxMessage.Id, "all-handlers-for-this-row")` **before**
  calling `IModuleClient.PublishAsync` at all — simpler, but coarser (dedups per-row, not per-handler,
  so if 3 handlers are registered for one message type and only 1 needs dedup, this approach still
  gates all 3 identically). **Decide this explicitly before writing handler code** — recommendation:
  option (b) if the audit shows dedup-needing and dedup-safe handlers are never mixed for the same
  message type; option (a) if they are. Record the actual decision here once made.

### Files NOT to touch
- Handlers classified "naturally idempotent" in the audit — no dedup wrapping added, but the *reason*
  must be recorded in the audit table (Step 1), not just silently skipped.
- Phase 1–3 infrastructure (`OutboxMessage`, `OutboxPollerService`, `OutboxDispatcher`,
  `CrossContextTransactionScope`) — only touched if the audit's open question above resolves to option
  (b) (dispatcher-level dedup), in which case `OutboxDispatcher` gets a small, explicit addition — not
  a rewrite.

### Tests required
- Unit: `ECommerceApp.UnitTests/Messaging/ProcessedMessageGuardTests.cs` —
  `TryMarkProcessedAsync_FirstCall_ReturnsTrueAndPersists`,
  `TryMarkProcessedAsync_DuplicateCall_ReturnsFalseNoSecondRow`,
  `TryMarkProcessedAsync_OtherDbException_Propagates` (distinguish a real failure from "already
  processed").
- Integration, **per handler that got dedup** — the single most important test class per handler:
  `HandlerNameDuplicateDeliveryTests.cs` — deliver the *same* `Outbox` row twice (simulate redelivery
  directly, don't rely on timing/crash races), assert the handler's real side effect (stock quantity,
  payment row count, coupon usage count) changed **exactly once**, not twice.
- Integration: confirm a message type with **no** dedup-needing handler still delivers normally and
  isn't accidentally gated by an Inbox check that shouldn't apply to it.

### Steps (atomic, ordered)
1. Complete and record the audit (Step 1) — get this reviewed/confirmed before writing code, since it
   determines the entire remaining scope of this phase.
2. Resolve the "how does a handler know its Outbox message id" open question explicitly.
3. Add `ProcessedMessage`, `IProcessedMessageGuard`/`ProcessedMessageGuard`, EF config, migration.
4. Wire dedup into each audited-as-needed handler (or into `OutboxDispatcher`, per the resolved
   approach), one handler/BC at a time, testing each before moving to the next.
5. Full build + full test suite.

### Verification commands @verifier will run
- `dotnet build ECommerceApp.sln --configuration Release --nologo`
- `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
- `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
- ArchUnitNET — confirm `messaging.Inbox`/`ProcessedMessage` access stays inside the Messaging layer
  and its consumers, no cross-BC leakage of the dedup mechanism's internals

### Risks / open questions
- **The "how does a handler know its Outbox message id" question must be resolved before any handler
  code is written** — this is the load-bearing design decision of this entire phase; do not let
  individual handlers each invent their own answer.
- **Risk**: if the audit under-scopes (misses a handler that actually needs dedup), the gap is silent
  until a real redelivery happens in production — recommend a conservative bias in ambiguous cases
  (add dedup) over an aggressive one (skip it), since the cost of an unnecessary `Inbox` row is far
  lower than the cost of a silent double-charge or double stock decrement.
- **Risk**: composite-key `(MessageId, HandlerType)` on `Inbox` will grow unbounded without cleanup —
  this is exactly what Phase 5 exists to address; do not add ad hoc cleanup logic here, wait for Phase 5.

### Rollback plan
- Revert the audited handlers' dedup wrapping (per-handler, independent). Revert
  `ProcessedMessage`/`IProcessedMessageGuard`/migration as a unit if the whole phase needs undoing. No
  impact on Phases 1–3's already-live Outbox delivery — Inbox is purely additive on top of it.
