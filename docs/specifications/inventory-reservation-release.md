# Flow: Inventory Reservation and Release

> Domain: Inventory/Availability
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0011/0011-inventory-availability-bc-design.md](docs/adr/0011/0011-inventory-availability-bc-design.md)

---

## Purpose

Describe the current hard-reservation lifecycle in inventory, including confirm/release/fulfill/withdraw paths implemented in handlers and stock service.

---

## Scope (current code only)

### Included

- Reservation creation on `OrderPlaced`.
- Confirmation on `PaymentConfirmed`.
- Release on `PaymentExpired`, `OrderCancelled`, `OrderPlacementFailed`, and shipment-failure paths.
- Fulfillment on shipment-delivered paths.
- Manual withdrawal via stock service.

### Excluded

- Future event taxonomy not present in code.
- Soft-reservation UI behavior.

---

## Implemented status model

`StockHoldStatus` values in code:

- `Guaranteed`
- `Confirmed`
- `Released`
- `Fulfilled`
- `Withdrawn`

Source: `ECommerceApp.Domain/Inventory/Availability/StockHoldStatus.cs`.

---

## Entry and transition points in current implementation

### Reserve (create hold)

- Message: `OrderPlaced`
- Handler: `Inventory.Availability.Handlers.OrderPlacedHandler`
- Calls `StockService.ReserveAsync(...)`
- Creates `StockHold` with `Guaranteed` status.

### Confirm hold

- Message: `PaymentConfirmed`
- Handler: `Inventory.Availability.Handlers.PaymentConfirmedHandler`
- Calls `StockService.ConfirmHoldsByOrderAsync(...)`
- Hold transitions to `Confirmed`.

### Release hold

Release triggers currently implemented:

- `PaymentExpired` -> `ReleaseAllHoldsForOrderAsync(...)`
- `OrderCancelled` -> `ReleaseAsync(...)` per item
- `OrderPlacementFailed` -> `ReleaseAsync(...)` per item
- `ShipmentFailed` -> `ReleaseAsync(...)` per failed item
- `ShipmentPartiallyDelivered` -> `ReleaseAsync(...)` for failed items

`ReleaseAsync(...)` marks hold released and returns stock to available quantity for guaranteed holds.

### Fulfill hold

- `ShipmentDelivered` -> `FulfillAsync(...)`
- `ShipmentPartiallyDelivered` -> `FulfillAsync(...)` for delivered items

`FulfillAsync(...)` consumes reserved quantity and marks hold fulfilled.

### Withdraw hold

- `WithdrawHoldAsync(...)` available in stock service.
- Marks hold withdrawn and releases quantity.

Sources:

- `ECommerceApp.Application/Inventory/Availability/Services/StockService.cs`
- `ECommerceApp.Application/Inventory/Availability/Handlers/*.cs`

---

## Implemented rules

- Reserve path creates hard hold and timeout job.
- Confirm path upgrades holds to confirmed state.
- Release paths are idempotent-safe when hold is missing (`ReleaseAsync` returns `false`).
- Fulfill and release are separate outcomes.
- Shipment partial delivery splits logic: fulfilled items consumed, failed items released.
- Operator withdrawal is explicit and independent from message handlers.

---

## Notes

- This specification reflects current service/handler behavior only.
- No additional synthetic inventory events are documented.
