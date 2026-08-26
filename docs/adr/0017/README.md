# ADR-0017: Sales/Fulfillment BC

**Status**: Accepted — Amended
**BC**: Sales/Fulfillment
**Last amended**: 2025-06-27

## What this decision covers
Design of the `Refund` and `Shipment` aggregates, cross-BC coordination, and the Payments BC
extension for refund processing. The main ADR is intentionally concise; implementation detail is
split into the companion documents below.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0017-sales-fulfillment-bc-design.md | Context, decisions, consequences, alternatives, and references | Understanding Fulfillment BC |
| amendments/a1-shipment-integration-and-fanout.md | Shipment integration amendment: partial delivery, enriched messages, parallel fan-out, idempotency | Working with shipment integration |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/refund-lifecycle-flow.md | RequestRefund → ApproveRefund → RefundApproved message flow | Implementing refund operations |
| example-implementation/shipment-dispatch-flow.md | Shipment dispatch → deliver/fail → fan-out to Orders + Inventory | Working with shipment lifecycle |

## Key rules
- Amendment §13.3 takes precedence for shipment integration: fan-out is parallel — Fulfillment publishes to BOTH Orders and Inventory
- `IPaymentService.ProcessRefundAsync` is an extension added to Payments BC — defined in ADR-0015
- Slice 1 and Slice 2 are implemented in the current Fulfillment code; the legacy Payments-owned refund contract remains transitional until atomic cleanup is verified

## Related ADRs
- ADR-0015 (Payments) — ProcessRefundAsync
- ADR-0011 (Inventory) — subscribes to RefundApproved, ShipmentDispatched
- ADR-0014 (Orders) — subscribes to shipment events
