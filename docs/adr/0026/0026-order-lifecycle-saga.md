# ADR-0026: Implement Order Placement Saga via Choreography + Compensation (Option A)

## Status
Accepted

## Date
2026-04-14

## Context

The codebase already runs implicit choreography-based sagas. The highest-risk fan-out is the
`OrderPlaced` event, which triggers three handlers sequentially in the in-memory broker:

1. `Payments.OrderPlacedHandler` — creates a `Payment` record and schedules `PaymentWindowExpiredJob`
2. `Inventory.OrderPlacedHandler` — reserves stock holds per order item
3. `Presale.OrderPlacedHandler` — clears the cart and removes soft reservations

If any handler in the fan-out throws after a preceding handler has already committed its side
effects, the system is left in a partially-executed state with no automatic rollback path.
Specifically:

- If Inventory throws after Payments has committed, a `Payment` record exists with no matching
  stock hold — an orphaned pending payment.
- If Presale throws after both Payments and Inventory have committed, stock holds and the payment
  record exist but the cart was not cleared.

There is no saga state record — failure leaves only application logs as breadcrumbs.

This decision records how we address the highest-risk gap (partial failure after `OrderPlaced`
fan-out) in a way consistent with the existing choreography infrastructure.

See also: [`docs/roadmap/saga-pattern.md`](../roadmap/saga-pattern.md) for the full analysis,
gap inventory, and sequencing plan.

## Decision

We will implement **Option A — Choreography + Compensation**: introduce an `OrderPlacementFailed`
integration message and three compensation handlers, one per affected bounded context.

**Message:** `OrderPlacementFailed(int OrderId, string Reason, IReadOnlyList<OrderPlacedItem> Items, string UserId)`

**Trigger:** `OrderService.PlaceOrderAsync` wraps the `PublishAsync(OrderPlaced)` call in a
`try-catch`. On any exception it publishes `OrderPlacementFailed` with the failure reason and
returns `PlaceOrderResult.PlacementFailed(orderId)`.

**Compensation handlers:**

| BC | Handler | Action |
|---|---|---|
| Payments | `Payments.OrderPlacementFailedHandler` | Finds payment by `OrderId`; calls `Payment.Cancel()`; cancels the `PaymentWindowExpiredJob` timer via `IDeferredJobScheduler`. No-op if payment not found (idempotent). |
| Inventory | `Inventory.OrderPlacementFailedHandler` | Calls `IStockService.ReleaseAsync(orderId, productId, qty)` for each item in the message. No-op if no hold exists (idempotent). |
| Presale | `Presale.OrderPlacementFailedHandler` | Logs a warning. Cart restoration is deferred pending `ICartService.RestoreAsync` (tracked as TODO in handler). |

**Domain change:** `PaymentStatus.Cancelled` added for payments voided by compensation.
`Payment.Cancel()` guards that only `Pending` payments can be cancelled.

## Consequences

### Positive
- Closes the highest-risk partial-failure path (orphaned `Payment` + stock hold) without any new
  infrastructure or persistence table.
- Uses the existing `IMessageBroker` + `IMessageHandler<T>` infrastructure — consistent with all
  other BC event handling.
- Compensation handlers are independently testable.
- Delivery is incremental: each compensation handler is a small, focused unit.

### Negative
- No saga state record — if a compensation handler itself fails, the system is in a
  partially-compensated state with no automatic recovery beyond manual intervention or logs.
- Presale compensation is partial: cart items cleared by `OrderPlacedHandler` cannot be
  automatically restored until `ICartService.RestoreAsync` is implemented.
- The in-memory broker has no retry or at-least-once delivery guarantee — if the process crashes
  between `OrderPlaced` publishing and `OrderPlacementFailed` publishing, compensation is lost.

### Risks & mitigations
- **Compensation handler failure**: log + monitor; acceptable at current traffic level. Mitigated
  long-term by Outbox pattern (see roadmap).
- **Presale cart loss**: user must re-add items manually. Mitigated by a clear error message at
  the UI layer. Full restoration tracked as a TODO in `Presale.OrderPlacementFailedHandler`.
- **Double compensation**: all handlers are idempotent — calling `ReleaseAsync` or `Cancel()` on
  a resource that was never created is a safe no-op.

## Alternatives considered

- **Option B — Process Manager / OrderLifecycleSaga**: deferred. Requires a new `sales.OrderSagas`
  table, a `SagaCorrelationId` on every message, and an Outbox pattern for reliable delivery.
  Evaluate after F4 (handler chain refactoring) and Outbox are complete. See
  [`docs/roadmap/saga-pattern.md`](../roadmap/saga-pattern.md) §Option B.

- **Status quo (timeout-based safety net only)**: the existing `SoftReservationExpiredJob` sweeper
  handles some cases but is a timeout, not a compensating transaction. Orphaned Payment records
  are not addressed until the `PaymentWindowExpiredJob` fires (up to 3 days later).

---

## Amendment — F4 handler chain refactoring (2026-04-XX)

### `OrderCancelled` — reserved for manual-cancel path only

As part of the F4 decoupling work, `OrderCancelled` is **no longer published on the `PaymentExpired`
path**. The following change was made:

| Before | After |
|---|---|
| `OrderPaymentExpiredHandler` cancelled the order → published `OrderCancelled` → Inventory + Coupons handled `OrderCancelled` | `PaymentExpired` fan-out dispatches directly to `Inventory.PaymentExpiredHandler` + `Coupons.CouponsPaymentExpiredHandler` alongside `Orders.OrderPaymentExpiredHandler` |

**`OrderCancelled` is now reserved for the manual order cancellation path.** No handler or service
currently publishes it — it is retained as a message contract for future use when a manual-cancel
action is implemented (e.g., admin or customer cancels a Placed order before payment).

**`IStockService.ReleaseAllHoldsForOrderAsync(orderId)`** was added to support
`Inventory.PaymentExpiredHandler` — it retrieves all stock holds for the order via
`IStockHoldRepository.GetByOrderIdAsync` and releases each one, without requiring the caller
to know the item list.

### Current `PaymentExpired` flat fan-out topology

```
PaymentExpired ──┬── Orders.OrderPaymentExpiredHandler    (expires the order — no downstream publish)
                 ├── Inventory.PaymentExpiredHandler       (releases all stock holds via ReleaseAllHoldsForOrderAsync)
                 └── Coupons.CouponsPaymentExpiredHandler  (restores coupon usage — mirrors OrderCancelled path)
```

Integration tests: `PaymentExpiredFanOutTests` (4 tests) verify the flat fan-out end-to-end.
