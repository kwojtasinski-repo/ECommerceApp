# Cross-BC Message Wiring

This document is the implementation-oriented companion to ADR-0011 §9. It records the current
Inventory subscriptions and the one-way integration boundary. The table is reconciled against
`Application/Inventory/Availability/Services/Extensions.cs` and ADR-0017 §13.3.

## Boundary rule

All cross-BC triggers use `IMessageBroker`. Inventory subscribes to publisher-owned contracts;
Sales, Payments, Fulfillment, and Catalog do not call Inventory services directly.

## Current inbound subscriptions

| Message | Publisher | Handler | Action |
|---|---|---|---|
| `OrderPlaced` | Sales/Orders | `OrderPlacedHandler` | Create `StockHold`, reserve stock, and schedule timeout |
| `OrderCancelled` | Sales/Orders | `OrderCancelledHandler` | Release the `StockHold` and complete terminal transition |
| `PaymentConfirmed` | Sales/Payments | `PaymentConfirmedHandler` | Confirm the `StockHold` |
| `PaymentExpired` | Sales/Payments | `PaymentExpiredHandler` | Release the `StockHold` through expiry path |
| `OrderPlacementFailed` | Sales/Orders | `OrderPlacementFailedHandler` | Compensate the stock hold |
| `RefundApproved` | Sales/Payments | `RefundApprovedHandler` | Return physical stock |
| `ShipmentDelivered` | Sales/Fulfillment | `ShipmentDeliveredHandler` | Find `StockHold` by orderId + productId and fulfill delivered lines |
| `ShipmentFailed` | Sales/Fulfillment | `ShipmentFailedHandler` | Find `StockHold` by orderId + productId and release failed lines |
| `ShipmentPartiallyDelivered` | Sales/Fulfillment | `ShipmentPartiallyDeliveredHandler` | Fulfill delivered and release failed lines |
| `ProductPublished` | Catalog | `ProductPublishedHandler` | Upsert an orderable ProductSnapshot |
| `ProductUnpublished` | Catalog | `ProductUnpublishedHandler` | Suspend ProductSnapshot |
| `ProductDiscontinued` | Catalog | `ProductDiscontinuedHandler` | Mark ProductSnapshot discontinued |

## Shipment handler migration

`OrderShippedHandler` is not an active Inventory subscription. It was replaced by the three
Fulfillment handlers because shipment outcomes contain line-level information needed for partial
delivery. The old class may remain as a rollback artifact, but it must stay unregistered.

```csharp
services
	.AddScoped<IMessageHandler<ShipmentDelivered>, ShipmentDeliveredHandler>()
	.AddScoped<IMessageHandler<ShipmentFailed>, ShipmentFailedHandler>()
	.AddScoped<IMessageHandler<ShipmentPartiallyDelivered>, ShipmentPartiallyDeliveredHandler>();
```

## Outbound message

Inventory publishes `StockAvailabilityChanged` after `Reserve`, `Release`, `Fulfill`, `Return`,
and `Adjust` (`Application/Inventory/Availability/Messages/StockAvailabilityChanged.cs`). The
payload is exactly `ProductId`, `AvailableQuantity`, and `OccurredAt` — there is no `IsOutOfStock`
field on this message. Internal stock domain events remain Inventory-internal audit events.

## Example implementation sketch

```csharp
internal sealed class ShipmentPartiallyDeliveredHandler
	: IMessageHandler<ShipmentPartiallyDelivered>
{
	public async Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct)
	{
		foreach (var delivered in message.DeliveredItems)
		{
			await stockService.FulfillAsync(
				message.OrderId, delivered.ProductId, delivered.Quantity, ct);
		}

		foreach (var failed in message.FailedItems)
		{
			await stockService.ReleaseAsync(
				message.OrderId, failed.ProductId, failed.Quantity, ct);
		}
	}
}
```

Each handler must preserve terminal-state idempotency. Replaying a shipment message must not
fulfill or release a stock hold that has already reached its terminal state.

## Rules

- Register handlers in `AddAvailabilityServices`.
- Keep message contracts in their publisher BC.
- Use `StockHold`, the real domain entity; do not introduce an alternative entity type.
- Do not re-register `OrderShippedHandler`.
- Keep partial-delivery handling line-aware.
- Update architecture tests when a new publisher dependency is introduced.

## Outside this document

- Fulfillment shipment state machine: ADR-0017.
- Message broker delivery and retry semantics: ADR-0010.
- Stock-hold invariants and timeout behavior: `two-phase-reservation-flow.md`.
