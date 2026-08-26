# Price-change detection

Price warnings compare a locked `SoftReservation.UnitPrice` with the current Catalog price before
confirmation. The comparison is advisory; order placement still uses the locked price.

## Query

`ISoftReservationService.GetPriceChangesAsync` loads active holds for the user and calls
`ICatalogClient.GetUnitPriceAsync` for each product. It returns only changed lines as
`SoftReservationPriceChangeVm(ProductId, LockedPrice, CurrentPrice)`. An empty list means no
change was detected or there are no active holds.

The consuming checkout controller requests this view model before rendering the confirmation
screen. It displays a warning per returned line, but does not rewrite the reservation or cart.

## Rules

- Compare against the live Catalog value only for warning purposes.
- Always pass `SoftReservation.UnitPrice` into `PlaceOrderFromPresaleAsync`.
- Keep the warning read-only and do not turn it into an implicit re-pricing operation.
- Missing or unavailable Catalog prices are handled by the service's existing result policy; they
	must not silently become a new transaction price.

## Outside this document

- Reservation creation and price capture are in `soft-reservation-lifecycle.md`.
- Order placement sequencing is in `checkout-confirm-flow.md`.
- UI wording and localization belong to the consuming Web/API surface.
