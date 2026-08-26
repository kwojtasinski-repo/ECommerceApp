# Shipment Dispatch and Outcome Flow

This companion describes the implemented Slice 2 lifecycle. The amendment is authoritative for the
enriched messages and parallel fan-out: [A1](../amendments/a1-shipment-integration-and-fanout.md).

## Dispatch

1. `CreateShipmentAsync` verifies the order through `OrderExistsQuery`, creates a `Shipment` with
	one or more `ShipmentLine` values, and persists it as `Pending`.
2. `MarkAsInTransitAsync` validates the tracking number and the `Pending` state, then sets
	`InTransit` and `ShippedAt`.
3. The aggregate update and `ShipmentDispatched(ShipmentId, OrderId, TrackingNumber, OccurredAt)`
	are committed in one Fulfillment transaction through the outbox.

## Delivery and failure

- `MarkAsDeliveredAsync` requires `InTransit`, sets `DeliveredAt`, and emits
  `ShipmentDelivered` with all shipment lines. Orders marks the order delivered; Inventory
  fulfills each corresponding stock hold.
- `MarkAsFailedAsync` allows `Pending` or `InTransit`, sets `Failed`, and emits `ShipmentFailed`
  with all shipment lines. Inventory releases each corresponding stock hold.
- Invalid state or missing shipment returns `InvalidStatus` or `NotFound` without publishing an
  event.

## Partial delivery (A1)

1. `MarkAsPartiallyDeliveredAsync` requires an in-transit shipment and at least one delivered
	product ID.
2. The aggregate becomes `PartiallyDelivered` and records `DeliveredAt`.
3. The service partitions shipment lines into `DeliveredItems` and `FailedItems`, then emits one
	`ShipmentPartiallyDelivered` outbox message.
4. Orders records the partial fulfilment. Inventory fulfills delivered items and releases failed
	items. Both consumers receive the event directly from Fulfillment.

## Replay and multiple shipments

Shipment handlers implement id-aware processing. The processed-message guard prevents replaying a
completed outbox message. Inventory stock operations are also terminal-aware, so a repeated delivery
does not fulfill or release the same hold twice. Multiple shipments for one order remain independent;
an individual shipment outcome does not imply that every shipment is complete.

## Relevant implementation surfaces

- `ECommerceApp.Domain/Sales/Fulfillment/Shipment.cs`
- `ECommerceApp.Application/Sales/Fulfillment/Services/ShipmentService.cs`
- `ECommerceApp.Application/Sales/Fulfillment/Messages/ShipmentDelivered.cs`
- `ECommerceApp.Application/Sales/Fulfillment/Messages/ShipmentPartiallyDelivered.cs`
- `ECommerceApp.Application/Inventory/Availability/Handlers/ShipmentDeliveredHandler.cs`
- `ECommerceApp.Application/Sales/Orders/Handlers/OrderShipmentPartiallyDeliveredHandler.cs`
