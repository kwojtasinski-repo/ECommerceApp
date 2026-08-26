# ADR-0017: Sales/Fulfillment BC

## Status
Accepted and amended. Slice 1 and Slice 2 are implemented in the current Fulfillment code.

## Date
2026-03-12

## Context

Refunds and shipment lifecycle behavior belong to the Sales/Fulfillment bounded context. The
legacy design had two coupling problems: refund processing mutated Orders synchronously, and the
Payments-owned `RefundApproved` contract represented a Fulfillment event. Shipment integration
also needs item-level outcomes because one order may be fulfilled by multiple shipments.

This ADR covers the Fulfillment context in two slices:

- Slice 1: the `Refund` aggregate, request/approve/reject workflow, refund messages, and reactions
  in Orders, Payments, Inventory, and Communication.
- Slice 2: the `Shipment` aggregate, dispatch and delivery lifecycle, partial delivery, and direct
  fan-out of shipment outcomes to Orders and Inventory.

The amendment is authoritative for shipment integration details. Where this ADR and the amendment
differ, the amendment wins: [A1 - Shipment Integration and Parallel Fan-Out](./amendments/a1-shipment-integration-and-fanout.md).

## Architectural decisions

### 1. Bounded-context ownership and aggregates

Sales/Fulfillment owns `Refund` and `Shipment` aggregates. `RefundItem` and `ShipmentLine` are
owned entities. Cross-context references such as `OrderId` and `ProductId` are plain IDs with no
navigation properties or cross-context foreign keys. `RefundId` and `ShipmentId` use the shared
`TypedId<int>` pattern.

`Refund` has the states `Requested`, `Approved`, and `Rejected`. Its service verifies the order,
rejects duplicate active refunds, persists the aggregate, and emits approval or rejection events.
`Shipment` has `Pending`, `InTransit`, `Delivered`, `Failed`, and, under A1, `PartiallyDelivered`.
Its normal transitions are `Pending -> InTransit -> Delivered | Failed | PartiallyDelivered`.

### 2. Message ownership and integration

Fulfillment owns and publishes `RefundApproved` and `RefundRejected`. `RefundApproved` contains the
refund ID, order ID, and the complete list of refunded items. Payments processes one refund by
refund ID; Inventory processes each item; Orders records the refund; Communication handles customer
notifications. The old Payments-owned `RefundApproved` contract remains in the repository during
the transition and must not be described as removed until the atomic cleanup is actually complete.

Fulfillment publishes shipment messages through the transactional outbox. `ShipmentDispatched`,
`ShipmentDelivered`, `ShipmentFailed`, and A1's `ShipmentPartiallyDelivered` carry the line-level
data required by consumers. Shipment outcomes fan out directly to both Orders and Inventory; Orders
is not a relay for Inventory. Message handlers use outbox-message identity guards so replayed
messages do not repeat terminal stock operations.

### 3. Persistence and boundaries

Fulfillment uses `FulfillmentDbContext` and the `fulfillment` schema. Refund items and shipment
lines are persisted with `OwnsMany`. The only foreign keys are from owned rows to their aggregate
rows. There are no foreign keys from Fulfillment to Orders or Catalog.

### 4. Service and coordination rules

`RefundService` and `ShipmentService` are internal sealed application services. They coordinate with
other contexts through module queries and message contracts, not direct aggregate navigation. State
changes and outgoing messages are committed in one Fulfillment transaction through the outbox.

### 5. Amendment precedence

A1 amends the original shipment design with `PartiallyDelivered`, enriched shipment messages,
parallel Fulfillment-to-Orders-and-Inventory fan-out, retirement of the indirect Inventory
`OrderShipped` subscription, and multi-shipment idempotency. A1 is intentionally kept as a separate
amendment so its later decision remains visible and takes precedence over the original Slice 2
examples.

## Consequences

Positive:

- Fulfillment owns its refund and shipment lifecycles without synchronous cross-BC mutation.
- Refund approvals support multiple items while Payments is invoked once per refund.
- Shipment consumers receive enough line-level data to fulfill or release the correct stock.
- Transactional outbox delivery and processed-message guards make state changes recoverable and
  replay-aware.
- The schema preserves bounded-context boundaries while allowing owned-entity persistence.

Costs and risks:

- Plain cross-context IDs can become stale if another context hard-deletes referenced data.
- Refund status does not by itself replace every Order status transition; consumers must use their
  own message handlers and domain rules.
- The legacy Payments-owned refund contract remains transitional surface area until its atomic
  cleanup step is verified.
- Multiple shipment and partial-delivery consumers require idempotent handling and careful item
  partitioning.

## Alternatives considered

- Directly call Orders or Payments from `RefundService`: rejected because synchronous cross-BC calls
  couple transactions and leave partial-failure paths harder to recover.
- Keep `RefundApproved` owned by Payments: rejected because the publishing lifecycle belongs to
  Fulfillment.
- Publish one approval message per refund item: rejected because Payments must transition once for
  the aggregate refund, not once per line.
- Relay shipment events through Orders before Inventory: rejected by A1 because Orders would become
  an unnecessary relay and line-level detail would be lost.
- Implement shipment without a partial-delivery outcome: rejected because partial fulfillment needs
  separate delivered and failed item sets.

## References

- [ADR-0010 - In-Memory Message Broker](../0010/README.md)
- [ADR-0011 - Inventory/Availability BC](../0011/README.md)
- [ADR-0013 - Per-BC DbContext Interfaces](../0013/README.md)
- [ADR-0014 - Sales/Orders BC](../0014/README.md)
- [ADR-0015 - Sales/Payments BC](../0015/README.md)
- [ADR-0016 - Sales/Coupons BC](../0016/README.md)
- [Bounded-context map](../../architecture/bounded-context-map.md)

## Companion documents

- [Refund lifecycle flow](./example-implementation/refund-lifecycle-flow.md)
- [Shipment dispatch and outcome flow](./example-implementation/shipment-dispatch-flow.md)
- [Conformance checklist](./checklist.md)
- [Migration plan](./migration-plan.md)
- [A1 - Shipment Integration and Parallel Fan-Out](./amendments/a1-shipment-integration-and-fanout.md)
