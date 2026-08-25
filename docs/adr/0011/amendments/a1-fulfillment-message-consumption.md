## Design Amendment — Fulfillment Message Consumption (2025-06-27)

> **Status**: Implemented. See ADR-0017 section 13.3 and [the current Inventory wiring](../example-implementation/cross-bc-message-wiring.md).

### Rationale

Inventory originally learned about shipment outcomes indirectly via `OrderShipped` published by
Orders BC after `ShipmentDelivered`. That lacked failure and partial-delivery information and
could leave confirmed stock holds unreleased.

### Implemented changes

Fulfillment messages are consumed directly by Inventory:

| Handler | Message | Action |
|---|---|---|
| `ShipmentDeliveredHandler` | `ShipmentDelivered` | Find `StockHold` by orderId + productId and call `StockItem.Fulfill()` |
| `ShipmentFailedHandler` | `ShipmentFailed` | Find `StockHold` by orderId + productId and call `StockItem.Release()` |
| `ShipmentPartiallyDeliveredHandler` | `ShipmentPartiallyDelivered` | Fulfill delivered items and release failed items |

All three handlers publish `StockAvailabilityChanged` for affected products.

### OrderShippedHandler retirement

`OrderShippedHandler` is unregistered from
`Application/Inventory/Availability/Services/Extensions.cs`. Its class file may remain as a
rollback artifact during the parallel-change window, but it is not an active subscription.

### DI registration

The active registrations in `Extensions.cs` are:

```csharp
services.AddScoped<IMessageHandler<ShipmentDelivered>, ShipmentDeliveredHandler>();
services.AddScoped<IMessageHandler<ShipmentFailed>, ShipmentFailedHandler>();
services.AddScoped<IMessageHandler<ShipmentPartiallyDelivered>, ShipmentPartiallyDeliveredHandler>();
```

### Idempotency

`StockHold` status provides the natural guard. `Fulfilled` and `Released` are terminal states, so
replaying a shipment message for an already terminal `StockHold` is a no-op.

### Verification note

This amendment was originally recorded as planned work. The status is now updated because the
three Fulfillment handlers are registered in the current application code. The detailed current
subscription table and implementation sketch live in `cross-bc-message-wiring.md`.
