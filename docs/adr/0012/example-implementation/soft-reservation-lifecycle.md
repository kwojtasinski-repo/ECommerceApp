# Soft reservation lifecycle

`SoftReservation` is a Presale entity representing customer checkout intent. It is not an
Inventory reservation and never changes `StockItem` counters.

## Create and refresh

`HoldAsync` reads the current unit price through `ICatalogClient`, checks
`StockSnapshot.AvailableQuantity` against active holds, writes the entity to the database, then
updates `IMemoryCache`. The database is authoritative. Re-initiation removes the user's active
holds first, so prices and expiry are refreshed from a clean state.

The entity stores `ProductId`, `UserId`, `Quantity`, immutable `UnitPrice`, and `ExpiresAt`.
`SoftReservationStatus` distinguishes `Active` from `Committed` during confirmation.

## Expiry and removal

Creation schedules `SoftReservationExpiredJob` through `IDeferredJobScheduler`, using the
reservation ID as `EntityId`. The job loads the row by ID, reports a successful no-op when it is
missing or already committed, and otherwise deletes the row and evicts its cache key.
Manual removal and successful order handling delete the database row, evict the cache entry, and
cancel the deferred job.

## Rules

- Persist before updating the cache; never make memory cache the source of truth.
- `UnitPrice` is captured once per hold and is immutable for order placement.
- Committed holds are protected from expiry until the Orders result is known.
- Soft reservations belong to Presale; hard reservations belong to Inventory.

## Outside this document

- Confirmation sequencing is in `checkout-confirm-flow.md`.
- Advisory price comparison is in `price-change-detection.md`.
- TTL configuration and migration approval follow the repository's application and infrastructure
	policies.
