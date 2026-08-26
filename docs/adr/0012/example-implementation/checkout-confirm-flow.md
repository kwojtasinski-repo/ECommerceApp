# Checkout confirmation flow

This companion records the implementation sequence for ADR-0012. Boundary decisions remain in
the ADR.

## Initiation

`ICheckoutService.InitiateAsync` loads the user's `CartLine` rows, removes existing active
`SoftReservation` rows, and calls `ISoftReservationService.HoldAsync` for each line. Each hold
gets its price from `ICatalogClient` and its availability gate from `StockSnapshot` minus active
holds. The result reports reserved and unavailable product IDs.

## Confirmation

1. Load active holds with `GetAllForUserAsync`; no holds returns a result failure.
2. Mark the loaded holds `Committed` before invoking Orders. This prevents the expiry job from
	deleting a hold during confirmation.
3. Build `PlaceOrderLineDto` values from the locked `SoftReservation.UnitPrice` and call
	`IOrderClient.PlaceOrderAsync`.
4. Revert committed holds to `Active` when Orders fails; return the result failure.
5. On success, `OrderPlaced` handling removes only the ordered cart lines and committed holds.

The Orders BC then publishes `OrderPlaced`; Inventory performs the definitive hard reservation.

## Rules

- `ICheckoutService` coordinates checkout; `ICartService` owns cart CRUD.
- Never replace the locked `SoftReservation.UnitPrice` with a live Catalog price during placement.
- Do not treat the availability pre-check as the definitive stock reservation.
- Keep the EC-001 concurrent-checkout behavior explicit; no distributed lock is required.

## Outside this document

- Soft-hold creation and expiry details are in `soft-reservation-lifecycle.md`.
- Price warnings are in `price-change-detection.md`.
- Orders and Inventory own their respective order-placement and hard-stock decisions.
