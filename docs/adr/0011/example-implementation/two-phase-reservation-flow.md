# Two-Phase Reservation Flow

This document is the implementation-oriented companion to ADR-0011 §4. It describes the
separation between the checkout preview hold and the inventory guarantee that starts when an
order is created.

## Scope

Inventory owns only the persisted `StockHold`. The short-lived UX hold belongs to
Presale/Checkout (ADR-0012) and is not loaded, copied, or reconciled by Inventory.

## Phase 1 — Soft reservation

The checkout flow may use an in-memory TTL hold while the shopper is selecting items. This hold
is an availability hint for UX, not a durable inventory reservation, and it must not decrement
`StockItem.ReservedQuantity`.

## Phase 2 — Guaranteed reservation

The `OrderPlaced` message is the boundary at which Inventory creates a durable `StockHold`.
`OrderPlaced.ExpiresAt` is stored as `StockHold.ExpiresAt` for record-keeping, but Inventory does
**not** schedule its own expiry job. The payment window is owned end-to-end by Sales/Payments.

```text
StockService.ReserveAsync(dto):
  1. Load ProductSnapshot and require CanBeReserved.
  2. For physical products, load StockItem, call Reserve(qty), and persist via a transaction.
  3. Add StockHold.Create(productId, orderId, qty, dto.ExpiresAt).
```

Digital products still receive a StockHold row for order tracing, but do not touch the
`StockItem` counter.

## Timeout and idempotency

There is no `IScheduledTask` in Inventory for reservation expiry — only `StockAdjustmentJob`
(§8a) is registered there. The actual timeout is `PaymentWindowExpiredJob`
(`Application/Sales/Payments/Handlers/PaymentWindowExpiredJob.cs`, ADR-0015), scheduled per
`Payment` and keyed by `paymentId`, not by product or quantity. On expiry it marks the `Payment`
as `Expired` and publishes `PaymentExpired(PaymentId, OrderId, OccurredAt, CorrelationId)`.

Inventory's `PaymentExpiredHandler` (`Application/Inventory/Availability/Handlers/`) subscribes to
that message and releases every hold for the order in one call, not just the single hold that
expired first:

```text
PaymentExpiredHandler.HandleAsync(message):
  1. Call StockService.ReleaseAllHoldsForOrderAsync(message.OrderId).
     -> loads every StockHold for the order and calls ReleaseAsync(orderId, productId, qty)
        for each one.

StockService.ReleaseAsync(orderId, productId, quantity):
  1. Load StockHold by (orderId, productId); missing hold is a no-op (returns false).
  2. If the hold is Guaranteed, release the quantity from StockItem inside a transaction.
     A hold that is no longer Guaranteed skips the counter release — this is what makes a
     replayed PaymentExpired safe.
  3. Mark the StockHold as Released and persist it regardless of the branch above.
```

`OrderCancelled` follows the same `ReleaseAsync` path through `OrderCancelledHandler` for the
manual-cancel case, so both expiry and cancellation share one idempotent release routine.

## Rules

- Do not schedule a reservation-expiry job inside Inventory; the payment window belongs to
  Sales/Payments (`PaymentWindowExpiredJob`, ADR-0015).
- Soft holds never enter the Inventory database.
- Hard holds are persisted and terminal rows are retained for audit.
- `StockItem` never owns a `StockHold` collection; holds are queried separately.
- `RowVersion` protects the counter update against concurrent order operations.

## Example implementation sketch

```csharp
public async Task ReserveAsync(ReserveStockDto dto, CancellationToken ct)
{
	var snapshot = await snapshots.GetByProductIdAsync(dto.ProductId, ct)
		?? throw new BusinessException("Product snapshot not found.");

	if (!snapshot.CanBeReserved)
	{
		throw new BusinessException("Product cannot be reserved.");
	}

	if (!snapshot.IsDigital)
	{
		var stock = await stockItems.GetByProductIdAsync(dto.ProductId, ct)
			?? throw new BusinessException("Stock item not found.");
		stock.Reserve(dto.Quantity);
		await stockItems.UpdateAsync(stock, ct);
	}

	await stockHolds.AddAsync(
		StockHold.Create(dto.ProductId, dto.OrderId, dto.Quantity, dto.ExpiresAt), ct);
}
```

The sketch is illustrative. Transaction boundaries, repository implementations, and message
dispatch remain governed by the surrounding ADRs and the application service implementation.

## Outside this document

- Checkout soft-hold UX and TTL policy: ADR-0012.
- Payment authorization and order lifecycle: ADR-0014 and ADR-0015.
- Generic deferred-job infrastructure: ADR-0009.
- Database mappings and migrations: ADR-0013 and the Infrastructure project.
