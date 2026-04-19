# ADR-0017: Sales/Fulfillment BC

**Status**: Accepted — Amended
**BC**: Sales/Fulfillment
**Last amended**: 2025-06-27

## What this decision covers
Design of `Refund` aggregate (Slice 1: request/approve/reject), `Shipment` entity (Slice 2:
dispatch/deliver/fail), cross-BC coordination, and Payments BC extension for refund processing.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0017-sales-fulfillment-bc-design.md | Core design: §1–§12 Refund + Shipment aggregates | Understanding Fulfillment BC |
| amendments/a1-shipment-integration-and-fanout.md | §13.1–13.5: PartiallyDelivered status, enriched messages, parallel fan-out, idempotency | Working with shipment integration |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/refund-lifecycle-flow.md | RequestRefund → ApproveRefund → RefundApproved message flow | Implementing refund operations |
| example-implementation/shipment-dispatch-flow.md | Shipment dispatch → deliver/fail → fan-out to Orders + Inventory | Working with shipment lifecycle |

## Key rules
- Amendment §13.3: fan-out is parallel — Fulfillment publishes to BOTH Orders and Inventory on shipment events
- `IPaymentService.ProcessRefundAsync` is an extension added to Payments BC — defined in ADR-0015
- Switch complete for both Slice 1 and Slice 2

## Related ADRs
- ADR-0015 (Payments) — ProcessRefundAsync
- ADR-0011 (Inventory) — subscribes to RefundApproved, ShipmentDispatched
- ADR-0014 (Orders) — subscribes to shipment events
