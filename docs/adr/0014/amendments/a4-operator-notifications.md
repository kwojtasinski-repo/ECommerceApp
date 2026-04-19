## §19 Design Amendment — Operator Notifications and Event Payload Enrichment (2025-06-27)

> **Status**: Agreed — not yet implemented. Amends §2, §13 (event payloads).

### §19.1 `OrderRequiresAttention` Message

A new integration message signals operators when an order enters a problematic state.
No consumer exists yet — the Communication BC (ADR-0018) is blocked. This follows the
build-first parallel change strategy: the event infrastructure is ready for when the
consumer is built.

```csharp
// Application/Sales/Orders/Messages/
public record OrderRequiresAttention(
    int OrderId,
    string Reason,
    DateTime OccurredAt) : IMessage;
```

`Reason` is a free-form string describing the problem. Examples:
- `"Shipment failed — all items undelivered"`
- `"Shipment partially delivered — some items failed"`

**Publish points** (Orders handlers):
- `OrderShipmentFailedHandler` → publishes `OrderRequiresAttention` after recording the failure
- `OrderShipmentPartiallyDeliveredHandler` → publishes `OrderRequiresAttention` after recording
  partial delivery

### §19.2 `ShipmentFailurePayload` (new event payload)

`Order.RecordShipmentFailure()` currently stores no payload. This amendment enriches it
with shipment and item details for operator visibility:

```csharp
// Domain/Sales/Orders/Events/Payloads/
public record ShipmentFailurePayload(
    int ShipmentId,
    IReadOnlyList<FailedShipmentItem> FailedItems);

public record FailedShipmentItem(int ProductId, int Quantity);
```

`RecordShipmentFailure(int shipmentId, IReadOnlyList<FailedShipmentItem> failedItems)` —
updated signature; appends `OrderEventType.ShipmentFailed` with serialized
`ShipmentFailurePayload`.

### §19.3 `PartialFulfilmentPayload` Enrichment (amends existing payload)

The existing `PartialFulfilmentPayload` only tracks delivered items. It is enriched to also
track failed items and the shipment reference:

```csharp
// Domain/Sales/Orders/Events/Payloads/
public record PartialFulfilmentPayload(
    int ShipmentId,                                    // NEW
    IReadOnlyList<FulfilledItem> DeliveredItems,       // renamed from Items
    IReadOnlyList<FulfilledItem> FailedItems);         // NEW

public record FulfilledItem(int ItemId, int Quantity); // unchanged
```

`Order.MarkAsPartiallyFulfilled(int shipmentId, ...)` — updated to accept both lists.
