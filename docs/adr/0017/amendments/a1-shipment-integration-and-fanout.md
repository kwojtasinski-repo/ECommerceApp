## §13 Design Amendments — Shipment Integration and Parallel Fan-Out (2025-06-27)

> **Status**: Agreed — not yet implemented. Amends §11.1, §11.3, §11.5, §11.6.

### §13.1 `ShipmentStatus.PartiallyDelivered` (amends §11.1)

A fifth status value is added to support partial delivery scenarios:

```csharp
public enum ShipmentStatus { Pending, InTransit, Delivered, Failed, PartiallyDelivered }
```

State transitions:
```
Pending → InTransit → Delivered | Failed | PartiallyDelivered
```

`Shipment.MarkAsPartiallyDelivered(IReadOnlyList<int> deliveredProductIds)`:
- Guards `Status == InTransit`
- Sets `Status = PartiallyDelivered`, `DeliveredAt = DateTime.UtcNow`
- `deliveredProductIds` identifies which `ShipmentLine` products were successfully delivered;
  the remainder are failed. This line-level detail is what makes partial delivery work —
  there is no separate "partial delivery" concept.

### §13.2 Enriched Shipment Messages (amends §11.5)

All shipment messages are enriched with line-level item data so downstream consumers
(Orders BC, Inventory BC) can act on specific products without querying back:

```csharp
public record ShipmentDelivered(
    int ShipmentId, int OrderId,
    IReadOnlyList<ShipmentLineItem> Items,
    DateTime OccurredAt) : IMessage;

public record ShipmentFailed(
    int ShipmentId, int OrderId,
    IReadOnlyList<ShipmentLineItem> Items,
    DateTime OccurredAt) : IMessage;

public record ShipmentPartiallyDelivered(
    int ShipmentId, int OrderId,
    IReadOnlyList<ShipmentLineItem> DeliveredItems,
    IReadOnlyList<ShipmentLineItem> FailedItems,
    DateTime OccurredAt) : IMessage;

public record ShipmentLineItem(int ProductId, int Quantity);
```

`ShipmentDispatched` is unchanged (already has `TrackingNumber`; items are implicit from the
shipment creation).

### §13.3 Parallel Fan-Out — Fulfillment → Orders + Inventory (amends §11.6)

**Decision**: Fulfillment publishes shipment events directly to **both** Orders and Inventory
simultaneously via `IMessageBroker`. This replaces the previously implied chain pattern
(Fulfillment → Orders → Inventory via `OrderShipped`).

**Rationale**: The Fulfillment ↔ Inventory relationship in the BC map is a partnership — both
BCs have legitimate reasons to consume shipment events directly. Chaining through Orders would:
1. Create an unnecessary coupling (Orders becomes a relay for Inventory)
2. Lose line-level detail (Orders only publishes `OrderShipped` without item breakdown)
3. Add latency and a single point of failure

**Inventory handlers (new)**:

```csharp
// Application/Inventory/Availability/Handlers/
internal sealed class ShipmentDeliveredHandler : IMessageHandler<ShipmentDelivered>
{
    // For each item: find StockHold by orderId + productId → call StockItem.Fulfill()
    // Publishes StockAvailabilityChanged for each affected product
}

internal sealed class ShipmentFailedHandler : IMessageHandler<ShipmentFailed>
{
    // For each item: find StockHold by orderId + productId → call StockItem.Release()
    // Publishes StockAvailabilityChanged for each affected product
}

internal sealed class ShipmentPartiallyDeliveredHandler : IMessageHandler<ShipmentPartiallyDelivered>
{
    // For delivered items: Fulfill() — stock leaves the system
    // For failed items: Release() — stock returns to available pool
    // Publishes StockAvailabilityChanged for each affected product
}
```

**`OrderShippedHandler` retirement**:
- The existing `OrderShippedHandler` in Inventory (subscribed to `OrderShipped` from Orders BC)
  is **unregistered** from DI. Inventory no longer gets stock information indirectly via Orders.
- `OrderShipped` message **continues to be published** by Orders BC for audit trail and future
  consumers (e.g., Communication BC). It is not deleted.

**Architecture test update**: `App_Inventory` test must add `FulfillmentMessages` to its allowed
dependency list in `BoundedContextDependencyTests.cs`.

### §13.4 `ShipmentService` Amendments (amends §11.3)

`ShipmentService` methods are updated to enrich messages:

- `MarkAsDeliveredAsync`: loads shipment with lines, publishes `ShipmentDelivered` with
  `Items` populated from `shipment.Lines`.
- `MarkAsFailedAsync`: loads shipment with lines, publishes `ShipmentFailed` with `Items`
  populated from `shipment.Lines`.
- `MarkAsPartiallyDeliveredAsync` (new): calls `shipment.MarkAsPartiallyDelivered(deliveredProductIds)`,
  splits lines into delivered/failed based on `deliveredProductIds`, publishes
  `ShipmentPartiallyDelivered` with both lists.

### §13.5 Multi-Shipment Idempotency

One order can have multiple shipments (partial fulfillment). `ShipmentDelivered` for shipment 1
should not mark the entire order as fulfilled if shipment 2 is still in transit.
`StockHold` status provides natural idempotency — `Fulfilled` is a terminal state, so replaying
`ShipmentDelivered` for an already-fulfilled hold is a no-op (guard in `StockItem.Fulfill()`).
