## Design Amendment — Fulfillment Message Consumption (2025-06-27)

> **Status**: Agreed — not yet implemented. See ADR-0017 §13.3 for the full parallel fan-out decision.

### Rationale

Inventory currently learns about shipment outcomes indirectly via `OrderShipped` (published by
Orders BC after `ShipmentDelivered`). This has two problems:

1. **`ShipmentFailed` and `ShipmentPartiallyDelivered` don't trigger stock release** — Inventory
   never sees these events. Failed shipment items remain in `StockHold.Confirmed` status
   indefinitely, causing a **stock leak**.
2. **`OrderShipped` lacks line-level detail** — it carries `Items[]` but these are order items,
   not shipment lines. For partial delivery, Inventory cannot distinguish delivered vs failed items.

### Changes

**3 new handlers** (consume Fulfillment messages directly):

| Handler | Message | Action |
|---|---|---|
| `ShipmentDeliveredHandler` | `ShipmentDelivered` | For each item: find `StockHold` by orderId + productId → `StockItem.Fulfill()` |
| `ShipmentFailedHandler` | `ShipmentFailed` | For each item: find `StockHold` by orderId + productId → `StockItem.Release()` |
| `ShipmentPartiallyDeliveredHandler` | `ShipmentPartiallyDelivered` | Delivered items → `Fulfill()`, Failed items → `Release()` |

All three publish `StockAvailabilityChanged` for each affected product.

**`OrderShippedHandler` retirement**:
- `OrderShippedHandler` is **unregistered** from `Application/Inventory/Availability/Services/Extensions.cs`
- The handler class file is kept during the parallel-change window for rollback safety
- Deleted at the atomic switch once Fulfillment handlers are verified green

**DI registration** (in `Extensions.cs`):
```csharp
// Remove:
// services.AddTransient<IMessageHandler<OrderShipped>, OrderShippedHandler>();

// Add:
services.AddTransient<IMessageHandler<ShipmentDelivered>, ShipmentDeliveredHandler>();
services.AddTransient<IMessageHandler<ShipmentFailed>, ShipmentFailedHandler>();
services.AddTransient<IMessageHandler<ShipmentPartiallyDelivered>, ShipmentPartiallyDeliveredHandler>();
```

**Architecture test update**: `App_Inventory` test must add `FulfillmentMessages` to its allowed
dependency list in `BoundedContextDependencyTests.cs`.

**Idempotency**: `StockHold` status provides natural guard — `Fulfilled` and `Released` are
terminal states. Replaying a `ShipmentDelivered` for an already-fulfilled hold is a no-op
(`StockItem.Fulfill()` guards `hold.Status == Confirmed`).
