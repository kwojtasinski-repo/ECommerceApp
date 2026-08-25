# Stock Adjustment Algorithm

This document is the implementation-oriented companion to ADR-0011 §8a. `Adjust` is the only
human-driven `StockItem` write and is deliberately deferred so admin corrections do not compete
with order reservation, release, or fulfillment writes for the same `RowVersion`.

## Design

`PendingStockAdjustment` stores one latest setpoint per product. Every `AdjustAsync` call:

1. upserts the requested quantity and replaces the `Version` GUID,
2. cancels a pending job for the product,
3. schedules one fresh `StockAdjustmentJob` immediately,
4. returns to the administrator with the adjustment queued.

This is last-write-wins coalescing. Rapid submissions do not create a queue of stale values.

## Example implementation sketch

```csharp
public sealed class PendingStockAdjustment
{
		public StockProductId ProductId { get; private set; }
		public StockQuantity NewQuantity { get; private set; }
		public Guid Version { get; private set; }
		public DateTime SubmittedAt { get; private set; }

		private PendingStockAdjustment() { }

		public static PendingStockAdjustment Create(
				StockProductId productId, StockQuantity newQuantity)
				=> new()
				{
						ProductId = productId,
						NewQuantity = newQuantity,
						Version = Guid.NewGuid(),
						SubmittedAt = DateTime.UtcNow
				};
}
```

## Job execution

The job captures the pending row's `Version` before changing stock. It loads fresh stock state,
calls `StockItem.Adjust`, and retries only application-level `DbUpdateConcurrencyException` up to
five times with exponential backoff. This is independent from the deferred-job infrastructure's
default retry count of three.

```text
StockAdjustmentJob.ExecuteAsync(context):
	1. Validate EntityId as productId.
	2. Load pending adjustment; missing row is a successful no-op.
	3. Capture pending.Version.
	4. Up to 5 attempts: load fresh StockItem, call Adjust, persist, and retry conflicts.
	5. DeleteIfVersionMatches(productId, capturedVersion).
	6. Report success.
```

The domain guard is evaluated at execution time. If current reservations are greater than the
requested quantity, `Adjust` rejects the operation rather than violating the invariant.

## Race behavior

If an administrator submits a new setpoint while the first job is already running, cancellation
cannot remove the running job. The running job applies its captured value, then its version-match
delete affects zero rows because the newer setpoint has a different `Version`. The newer pending
job remains and applies the latest value.

```text
Job 1 captures (qty=4, version=A)
Admin submits (qty=9, version=B)
Job 1 applies 4 and DeleteIfVersionMatches(A) deletes 0 rows
Job 2 reads version=B and applies 9
Final stock quantity = 9
```

## Rules

- There is at most one pending setpoint row per product.
- A pending row is deleted only with a matching version.
- `Adjust` is never performed inline in the admin request.
- `StockItem.Adjust` is absolute replacement, not additive correction.
- The current `ReservedQuantity` guard is evaluated by the background job.
- Infrastructure retry and application concurrency retry are separate mechanisms.

## Outside this document

- SQL mapping and schema conventions: Infrastructure configuration and ADR-0013.
- Generic scheduler polling and dead-letter behavior: ADR-0009.
- Reservation and fulfillment writes: `two-phase-reservation-flow.md` and ADR-0011.
