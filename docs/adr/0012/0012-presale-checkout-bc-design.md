# ADR-0012: Presale/Checkout BC - CartLine, SoftReservation, StockSnapshot, and ACL-Based Pre-Sale Design

## Status
Accepted

## Date
2026-03-06 (Slice 1), 2026-03-12 (Slice 2), 2026-03-14 (Slice 3)

## Context

The storefront needs a coherent pre-sale flow across Catalog and Inventory. Cart intent,
customer checkout intent, and hard stock commitment have different lifecycles and must not be
collapsed into one model. Price must be captured when checkout is initiated, while availability
must be observed without duplicating the Catalog product projection.

## Decision

Presale/Checkout is a bounded context with three delivered slices:

- Cart lines are flat `(UserId, ProductId)` rows, persisted to `presale.CartLines` and accelerated
  by a write-through memory cache. A cart is not an aggregate and `CartLine` has no price field.
- `SoftReservation` belongs to Presale. It captures the Catalog unit price at checkout initiation,
  is persisted in `presale.SoftReservations`, and expires through `IDeferredJobScheduler`.
  It never changes Inventory hard counters.
- `StockSnapshot` is a local read model updated by the `StockAvailabilityChanged` message from
  Inventory. Predicted availability is computed from the snapshot minus active soft holds.
- `ICatalogClient` and `IStockClient` are Presale ACLs. Infrastructure adapters connect them to
  the in-process Catalog and Inventory services; Presale does not inject another BC directly.
- `StorefrontController` is an API BFF that composes Catalog and Inventory for listing views. It
  is not part of the Presale domain model and no `StorefrontProducts` projection is introduced.
- `ICheckoutService` owns initiation and order placement; cart CRUD remains in `ICartService`.
  Confirmation commits soft reservations before calling the Orders ACL and reverts them if order
  placement fails. `SoftReservationExpiredJob` ignores committed reservations.
- Orders receives the locked per-line price through `PlaceOrderFromPresaleAsync`. Inventory owns
  the definitive hard reservation when the resulting `OrderPlaced` message is processed.
- Price-change detection is advisory. Confirmation compares the locked soft-reservation price
  with the current Catalog price but always places the order at the locked price.
- Checkout endpoints are authenticated: initiate, price-changes, and confirm are exposed under
  `/api/v2/checkout/`.
- Concurrent checkout accepts the documented EC-001 race; no distributed lock is introduced.

Implementation details are maintained in these companions:

- [Checkout confirmation flow](example-implementation/checkout-confirm-flow.md)
- [Soft reservation lifecycle](example-implementation/soft-reservation-lifecycle.md)
- [Price-change detection](example-implementation/price-change-detection.md)

## Consequences

Presale has an explicit boundary around customer intent and can later replace its ACL adapters
with HTTP clients. The BFF avoids a duplicated product projection, while `StockSnapshot` accepts
eventual consistency for availability. Database persistence survives process restarts, and the
memory cache remains an optimisation rather than the source of truth. The definitive Inventory
reservation still occurs at order placement, so the pre-check cannot guarantee stock by itself.

## Alternatives considered

- A denormalized `StorefrontProduct` projection was rejected because it duplicates Catalog data,
  adds handlers and migrations, and introduces propagation lag.
- Soft holds in Inventory were rejected because they represent customer intent, not stock
  commitment.
- A price on `CartLine` was rejected because the cart represents browsing intent; price is locked
  at checkout initiation.
- Memory-only soft reservations were rejected because process restart would lose active holds.
- Polling Inventory for availability was rejected in favor of event-driven `StockSnapshot` updates.
- Catalog messages without a consumer in Presale were rejected as dead contracts.

## References

- [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](../0002/0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0006 - Typed IDs and value objects](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0009 - Supporting TimeManagement BC](../0009/0009-supporting-timemanagement-bc-design.md)
- [ADR-0010 - In-Memory Message Broker](../0010/0010-in-memory-message-broker-for-cross-bc-communication.md)
- [ADR-0011 - Inventory/Availability BC](../0011/0011-inventory-availability-bc-design.md)
- [ADR-0014 - Sales/Orders BC](../0014/0014-sales-orders-bc-design.md)
