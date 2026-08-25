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
`OrderPlaced.ExpiresAt` is the single source of truth for both `StockHold.ExpiresAt` and the
`PaymentWindowTimeout` scheduled-job fire time.

```text
StockService.ReserveAsync(dto):
  1. Load ProductSnapshot and require CanBeReserved.
  2. For physical products, load StockItem, call Reserve(qty), and persist with RowVersion.
	3. Add StockHold.Create(productId, orderId, qty, dto.ExpiresAt).
  4. Schedule PaymentWindowTimeoutJob at dto.ExpiresAt.
```

Digital products still receive a StockHold row for order tracing, but do not touch the
`StockItem` counter.

## Timeout and idempotency

`PaymentWindowTimeoutJob` receives an `EntityId` encoded as `orderId:productId:quantity`.
Malformed or missing identifiers report a failed job. A missing hold or a hold that
is no longer `Guaranteed` is a successful no-op.

```text
PaymentWindowTimeoutJob.ExecuteAsync(context):
  1. Validate EntityId and parse orderId, productId, quantity.
	2. Load StockHold by (orderId, productId).
  3. If absent or not Guaranteed, report success as a no-op.
  4. Release quantity from StockItem.
	5. Mark StockHold as Released and update it; retain the row for audit history.
  6. Report success.
```

## Rules

- Reuse `OrderPlaced.ExpiresAt`; do not calculate a second timeout independently.
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
	await scheduler.ScheduleAsync(
		PaymentWindowTimeoutJob.JobTaskName,
		$"{dto.OrderId}:{dto.ProductId}:{dto.Quantity}",
		dto.ExpiresAt,
		ct);
}
```

The sketch is illustrative. Transaction boundaries, repository implementations, and message
dispatch remain governed by the surrounding ADRs and the application service implementation.

## Outside this document

- Checkout soft-hold UX and TTL policy: ADR-0012.
- Payment authorization and order lifecycle: ADR-0014 and ADR-0015.
- Generic deferred-job infrastructure: ADR-0009.
- Database mappings and migrations: ADR-0013 and the Infrastructure project.
