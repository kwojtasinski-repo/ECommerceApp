# ADR-0014: Sales/Orders BC — Order and OrderItem Aggregate Design

**Status**: Accepted — Amended (§16–§19 implemented)
**BC**: Sales/Orders
**Last amended**: 2025-06-27

## What this decision covers
Design of `Order` and `OrderItem` aggregates, TypedIds, result-based error handling,
`OrderCustomer` snapshot, `OrderProductSnapshot`, `OrderEvent` audit log, and cross-BC integration.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0014-sales-orders-bc-design.md | Core design: aggregates §1–§15, Consequences, Alternatives | Understanding Orders BC structure |
| amendments/a1-order-status-lifecycle.md | OrderStatus enum, state transitions, timestamp derivation (overrides §1) | Working with order lifecycle |
| amendments/a2-event-payload-records.md | Event payload record shapes, PaymentId/RefundId moved to payloads | Adding or reading OrderEvents |
| amendments/a3-integration-flow-decisions.md | CartLine cleanup, PaymentConfirmed.Items gap, currency decision | Cross-BC integration questions |
| amendments/a4-operator-notifications.md | OrderRequiresAttention, ShipmentFailurePayload, PartialFulfilmentPayload | Notification/operator message handlers |
| checklist.md | Domain aggregate + infrastructure + application conformance rules | Code review |
| migration-plan.md | 32-step implementation guide (completed) | Historical reference |
| example-implementation/order-aggregate-usage.md | Order.Create(), MarkAsPaid(), Cancel(), AssignCoupon() usage | Implementing order operations |
| example-implementation/place-order-flow.md | PlaceOrderAsync full sequence (Presale→Orders→Payments) | End-to-end order placement |
| example-implementation/result-handling-pattern.md | How to handle PlaceOrderResult / OrderOperationResult in controllers | Writing order controllers |
| example-implementation/order-product-snapshot.md | SnapshotOrderItemsJob + OrderPlacedSnapshotHandler pattern | Working with product snapshots |

## Key rules
- Amendments §16–§19 **override** earlier sections — always check amendments before main ADR
- Order state changes ONLY via domain methods — `order.IsPaid = true` is a compile error after switch
- Switch complete — legacy `OrderService`, `OrderItemService`, `OrderRepository` do not exist

## Related ADRs
- ADR-0015 (Payments) — OrderPlaced triggers PaymentInitialized
- ADR-0026 (Saga) — OrderPlacementFailed compensation
- ADR-0012 (Presale) — PlaceOrderFromPresaleAsync caller
- ADR-0016 (Coupons) — AssignCoupon / RemoveCoupon on Order
