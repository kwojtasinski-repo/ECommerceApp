# Saga / Orchestrator Pattern — Design Analysis

> **Status**: In Progress — Option A (choreography + compensation) skeleton implemented. ADR-0026 Accepted.
> Option B (Process Manager) deferred pending Outbox pattern + F4 handler chain refactoring.
> Linked from [`README.md` F3](./README.md#future-architectural-considerations).

---

## What is already there (implicit choreography sagas)

The codebase already runs choreography-based sagas — they just aren't labelled as such.
Every multi-BC event chain is one:

| Flow | Event chain |
|---|---|
| **Order Placement** | `OrderPlaced` → [Inventory.`OrderPlacedHandler`, Payments.`OrderPlacedHandler`, Presale.`OrderPlacedHandler`] |
| **Payment Confirmed** | `PaymentConfirmed` → [Orders.`OrderPaymentConfirmedHandler`, Inventory.`PaymentConfirmedHandler`] |
| **Payment Expired** | `PaymentExpired` → [Orders.`OrderPaymentExpiredHandler`] → `OrderCancelled` → [Inventory.`OrderCancelledHandler`, Coupons.`CouponsOrderCancelledHandler`] |
| **Refund Approved** | `RefundApproved` → [Inventory.`InventoryRefundApprovedHandler`, Communication.`RefundApprovedEmailHandler`] |
| **Shipment Delivered** | `ShipmentDelivered` → [Orders.`OrderShipmentDeliveredHandler`, Inventory.`ShipmentDeliveredHandler`] |
| **Shipment Failed** | `ShipmentFailed` → [Orders.`OrderShipmentFailedHandler`, Inventory.`ShipmentFailedHandler`] → `OrderRequiresAttention` → [Communication] |

`OrderStatus` on `Order` + `sales.OrderEvents` (ADR-0014 §16/§13) effectively **is** an order saga state machine already.
`StockHold.Status` (ADR-0011) is the Inventory leg of the same saga.

---

## Current gaps

### Gap 1 — No compensation on partial failure

If `Payments.OrderPlacedHandler` (creates the payment record) succeeds but
`Inventory.OrderPlacedHandler` (reserves stock) fails in the same `OrderPlaced` fan-out,
there is no rollback signal back to Payments. The `SoftReservationExpiredJob` acts as a
sweeper but it is a timeout-based safety net, not a compensating transaction.

The same gap exists on the reverse path: if `OrderCancelledHandler` in Inventory throws
after stock is already partially released for a multi-item order, there is no retry or
partial-compensation record.

### Gap 2 — No saga state persistence

You can answer "what status is this order in?" via `Order.Status`, but you cannot answer
"which saga steps have completed for order #42?" without joining across multiple BC tables.
Failed or partially-executed event chains leave no structured breadcrumb — only application
logs.

### Gap 3 — Implicit handler chains (F4 overlap)

`OrderPaymentExpiredHandler` handles `PaymentExpired` → cancels order → publishes
`OrderCancelled`. The second event triggers its own set of handlers across Inventory and
Coupons. These implicit chains are correct when all handlers succeed but produce ambiguous
failure semantics when any handler throws mid-chain.

---

## Design options

### Option A — Stay choreography, add explicit compensation paths *(recommended)*

**Add `OrderPlacementFailed(int orderId, string reason)` integration message:**

- Published by `OrderService.PlaceOrderAsync` (or a new `OrderPlacementSagaHandler`) when
  any downstream step throws after `OrderPlaced` has already been published.
- `Payments.OrderPlacementFailedHandler` — voids the pending payment.
- `Inventory.OrderPlacementFailedHandler` — releases any partial reservations.
- `Presale.OrderPlacementFailedHandler` — restores the cart.

**Why Option A first:**
- Uses the existing `IMessageBroker` + `IMessageHandler<T>` infrastructure — no new
  framework or persistence table needed.
- Compensation handlers are small, focused, and independently testable.
- Consistent with the choreography pattern already used across all BCs.
- Can be delivered incrementally (one compensation handler per BC).

**Known limitation:** still no saga state record — if a compensation handler itself fails,
the system is in a partially-compensated state with no recovery path beyond manual
intervention. Acceptable for the current traffic/complexity level.

### Option B — Process Manager per saga *(future, when Option A proves insufficient)*

A `OrderLifecycleSaga` entity (persisted in `sales.OrderSagas`, owned by the Orders BC)
that:
1. Subscribes to all `OrderPlaced`, `PaymentConfirmed`, `PaymentExpired`, `ShipmentDelivered`
   events.
2. Records which saga steps have acknowledged completion (`SagaStep` enum with bitfield or
   separate rows).
3. Issues compensating commands (`CancelPayment`, `ReleaseStockHolds`) when a step fails or
   times out.
4. Drives the next step only after the previous step has confirmed completion (optional —
   depends on whether strict ordering is needed).

**Why Option B is deferred:**
- Requires a new `sales.OrderSagas` table and `OrderSagaConfiguration`.
- Requires a `SagaCorrelationId` (likely `int orderId`) on every message.
- The current in-memory `ModuleClient` has no retry or at-least-once delivery guarantee —
  Option B's value is limited until a durable message transport (e.g. outbox pattern or
  external queue) is in place.
- May conflict with F4 (handler chain refactoring) — both address the same implicit-chain
  problem from different angles. Evaluate F4 first.

---

## Infrastructure prerequisite: Outbox Pattern

Both options benefit from — and Option B essentially requires — an **Outbox Pattern** to
guarantee that messages published during a `SaveChangesAsync` are eventually delivered even
if the process crashes after the DB commit but before `PublishAsync` returns.

Without the outbox:
- `OrderPlaced` can be published but the DB write can fail, or vice versa.
- No saga compensation can be reliable if the trigger event is lost.

**Outbox scope:** one table per BC (`sales.Outbox`, `payments.Outbox`, etc.) with a
`DeferredJobPollerService`-style background poller. Alternatively, a shared
`messaging.Outbox` table if we keep the in-memory broker long-term.

This is a separate ADR concern but must be sequenced **before** Option B is started.

---

## Recommended sequencing

```
Now (optional):
  └─► Option A — add OrderPlacementFailed compensation handlers
        Adds safety for the highest-risk failure path (placement fan-out)
        No new infrastructure required

Before Option B:
  └─► F4 — refactor implicit handler chains into explicit orchestration per BC
  └─► Outbox pattern — guarantee at-least-once delivery for critical messages
  └─► ADR-0026 — decision: choreography-only vs. mixed orchestration

Option B (if needed):
  └─► OrderLifecycleSaga entity + DB table
  └─► SagaCorrelationId on all relevant messages
  └─► Compensation command messages (CancelPayment, ReleaseStockHolds, etc.)
```

---

## Primary saga candidates (in order of risk)

| Saga | Steps | Compensation needed | Risk today |
|---|---|---|---|
| **Order Placement** | PlaceOrder → Reserve Stock → Create Payment → Clean Cart | ReleaseStockHolds, VoidPayment, RestoreCart | 🔴 High — fan-out; partial failure leaves orphaned Payment record |
| **Payment → Fulfillment** | PaymentConfirmed → ConfirmHolds → MarkOrderPaid | ReleaseHolds (if ConfirmHolds fails) | 🟡 Medium — two steps, one BC each |
| **Shipment → Fulfillment** | ShipmentDelivered → FulfillHolds → MarkOrderFulfilled | Rare — shipment events are authoritative | 🟢 Low |
| **Refund** | RefundApproved → ReturnStock → NotifyCustomer | ReturnStock idempotent; notification failure is non-critical | 🟢 Low |

---

## Files to create at implementation time

**Option A:**
```
Application/Sales/Orders/Messages/OrderPlacementFailed.cs
Application/Sales/Payments/Handlers/OrderPlacementFailedHandler.cs
Application/Inventory/Availability/Handlers/OrderPlacementFailedHandler.cs
Application/Presale/Checkout/Handlers/OrderPlacementFailedHandler.cs
UnitTests/Sales/Orders/OrderPlacementFailedHandlerTests.cs   (×3)
```

**Option B (additional):**
```
Domain/Sales/Orders/OrderSaga.cs
Domain/Sales/Orders/OrderSagaStep.cs
Infrastructure/Sales/Orders/Configurations/OrderSagaConfiguration.cs
Application/Sales/Orders/Handlers/OrderLifecycleSagaManager.cs
docs/adr/0026/0026-order-lifecycle-saga.md
```

---

## References

- [ADR-0002 — Parallel Change strategy](../adr/0002/README.md)
- [ADR-0014 §13 — OrderEvent audit log](../adr/0014/0014-sales-orders-bc-design.md) — existing saga state in Orders BC
- [ADR-0014 §18 — Integration flow gaps](../adr/0014/0014-sales-orders-bc-design.md) — Gap 2 (CartLine/SoftReservation leak) directly related
- [Roadmap F4 — handler chain refactoring](./README.md) — prerequisite for Option B
- [`bounded-context-map.md`](../architecture/bounded-context-map.md) — full BC event map
