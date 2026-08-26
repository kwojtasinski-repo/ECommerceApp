# ADR-0014: Sales/Orders BC - Order and OrderItem Aggregate Design

## Status
Accepted - amended by §§16-19. Amendments take precedence over this document where they
conflict with the original design.

## Date
2026-03-09

## Context

The legacy `Order` and `OrderItem` models were anemic entities in the shared model. They
exposed mutable state, held `ApplicationUser` navigations, and reached through foreign bounded
contexts to calculate prices. The legacy `OrderService` also combined order lifecycle, cart
operations, payment, coupon, customer, and product concerns.

The Sales/Orders bounded context needs an independently persisted model with explicit aggregate
invariants, historical customer and product data, asynchronous application services, and stable
cross-BC contracts. The existing integration messages `OrderPlaced`, `OrderCancelled`, and
`OrderShipped` must remain compatible with their consumers.

## Decision

Sales/Orders is implemented as a parallel bounded context under `Domain/Sales/Orders`,
`Application/Sales/Orders`, and `Infrastructure/Sales/Orders`. The existing legacy model remains
outside this design until an explicitly coordinated switch.

### Aggregate boundary

`Order` is the aggregate root and owns its `OrderItem` children and append-only `OrderEvent`
history. Domain state is changed through methods, not public setters. `OrderItem` has no product,
user, coupon, or refund navigation: it stores typed IDs and the captured line price. Refunds are
whole-order operations.

The aggregate keeps `CustomerId` and `CurrencyId` as raw cross-BC references. `OrderCustomer` is
an immutable placement-time snapshot, and `OrderProductSnapshot` is an asynchronous placement-time
snapshot of product display data. These snapshots preserve historical order presentation without
coupling the aggregate to live AccountProfile or Catalog models.

### Typed identity and value objects

The design uses `OrderId`, `OrderItemId`, `OrderProductId`, `OrderUserId`, and `OrderEventId` to
prevent accidental mixing of identifiers. `OrderNumber` is a validated, generated value object.
`OrderItem.UnitCost` uses the local `UnitCost` value object, which permits zero for free or
promotional items while rejecting negative values. The shared `Price` invariant is unchanged.

### Lifecycle and audit

`OrderStatus` is the authoritative lifecycle column. Domain transitions update the status and
append an `OrderEvent` in the same unit of work. Payment and refund reference IDs live in event
payloads rather than scalar order columns, allowing the history to represent more than one
payment and keeping cross-BC references out of the aggregate schema.

The event table is separate from the order table so events remain individually queryable and
append-oriented. The status column remains the fast-read projection for lifecycle queries. The
exact status rules, derived timestamps, payload records, and enriched shipment events are defined
by amendments §§16-19.

### Application and persistence boundaries

`IOrderService` owns order lifecycle and queries; `IOrderItemService` owns cart-item operations.
Expected business outcomes use `PlaceOrderResult` and `OrderOperationResult`; exceptions remain
for invariant violations and unexpected failures.

Orders owns an `OrdersDbContext` in the `sales` schema, with repositories defined in the domain
folder and infrastructure implementations using the Orders context only. The customer and
product resolvers are ACLs implemented by infrastructure adapters. Customer resolution occurs at
placement; product display snapshots are resolved by the `OrderPlaced` handler, with the scheduled
sweeper as a recovery path.

Order placement persists the aggregate and assigned cart items in an Orders unit of work, then
enqueues the `OrderPlaced` integration message through the outbox. Delivery similarly persists the
aggregate transition and enqueues `OrderShipped`. Presale cleanup, payment confirmation, and
inventory release remain message-driven responsibilities of their owning bounded contexts.

### Implementation references

The code-oriented usage sequences are maintained separately:

- [Order aggregate usage](example-implementation/order-aggregate-usage.md)
- [Place-order flow](example-implementation/place-order-flow.md)
- [Result handling](example-implementation/result-handling-pattern.md)
- [Product snapshot flow](example-implementation/order-product-snapshot.md)

## Consequences

Positive consequences include explicit aggregate invariants, no Law of Demeter navigation chain
for pricing, typed identifiers, historical customer and product display data, an auditable event
stream, and focused asynchronous services. `OrderStatus` supports efficient lifecycle queries
while event payloads retain contextual history. The separate `sales` schema permits the new BC to
be tested independently before the switch.

The design also introduces additional tables, value-object conversions, result contracts, ACL
adapters, and eventual consistency for product snapshots. The atomic switch remains a coordinated
operation across Orders, Payments, Coupons, Inventory, controllers, registrations, and tests.
Event history requires indexing and a later operational archival policy at high volume.

## Alternatives considered

- Keep exception-based outcomes: rejected because expected business failures have explicit result
  contracts and the Inventory BC already establishes this style.
- Use a generic `Result<T,E>`: rejected in favor of small named results for this bounded context.
- Extend the legacy generic repository: rejected because it is tied to `BaseEntity` and the shared
  context rather than this BC's typed IDs and DbContext.
- Use `Guid` identifiers: rejected because the current integration and MSSQL identity relationships
  use integer IDs.
- Keep one service for orders and cart items: rejected because the responsibilities have different
  lifecycles and the legacy service was already too broad.
- Keep live customer/product navigations: rejected because they violate BC isolation and would
  corrupt historical presentation when source data changes.
- Keep separate payment/delivery/cancellation booleans: rejected in favor of one `OrderStatus`
  column with event-derived timestamps.
- Store payment and refund references as order columns: rejected because event payloads preserve
  context and support multiple payment confirmations.
- Store events as JSON on `Order`: rejected because a separate table keeps rows queryable and
  append-oriented without rewriting a growing JSON document.
- Use the shared `Price` value object for `UnitCost`: rejected because zero is valid for a free
  promotional line; `UnitCost` localizes the relaxed invariant.
- Capture product display data when adding to cart: rejected because the order contract forms at
  placement and unused carts should not trigger Catalog reads.

## Amendments

- [§16 - OrderStatus lifecycle](amendments/a1-order-status-lifecycle.md)
- [§17 - Event payload records](amendments/a2-event-payload-records.md)
- [§18 - Integration flow decisions](amendments/a3-integration-flow-decisions.md)
- [§19 - Operator notifications](amendments/a4-operator-notifications.md)

## References

- [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](../0002/0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0003 - Feature-Folder Organization for New Bounded Context Code](../0003/0003-feature-folder-organization-for-new-bounded-context-code.md)
- [ADR-0006 - Strongly-Typed IDs and Self-Validating Value Objects](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0011 - Inventory/Availability BC Design](../0011/0011-inventory-availability-bc-design.md)
- [ADR-0012 - Presale/Checkout BC Design](../0012/0012-presale-checkout-bc-design.md)
- [ADR-0013 - Per-BC DbContext Interfaces](../0013/0013-per-bc-dbcontext-interfaces.md)
- [Bounded Context Map](../../architecture/bounded-context-map.md)
- [ADR-0014 router](README.md)

## Reviewers

- @team/architecture
