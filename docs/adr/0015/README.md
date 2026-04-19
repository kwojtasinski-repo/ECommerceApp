# ADR-0015: Sales/Payments BC

**Status**: Accepted
**BC**: Sales/Payments
**Last amended**: — (### 12. Migration plan is inside Decision section — extracted separately)

## What this decision covers
Design of the `Payment` aggregate state machine, `OrderPlacedHandler`, `PaymentWindowExpiredJob`,
`Order.Cancel()` extension, and the payments DB schema.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0015-sales-payments-bc-design.md | Core design: §1–§11 Payment aggregate, handlers, job, Order extensions | Understanding Payments BC |
| checklist.md | Conformance rules for Payment aggregate and handlers | Code review |
| example-implementation/payment-state-machine.md | Payment status transitions: Pending→Confirmed→Cancelled | Working with payment lifecycle |
| example-implementation/payment-window-expiry-flow.md | PaymentWindowExpiredJob timing, Order.Cancel() trigger | Debugging payment timeouts |
| example-implementation/order-cancel-flow.md | Order.Cancel() domain method + OrderCancelled message publishing | Implementing cancellation |

## Key rules
- `Payment` state transitions use result types — never throw for expected outcomes
- `PaymentWindowExpiredJob` is owned by Payments BC, registered in TimeManagement
- Switch complete — legacy `PaymentService` and `PaymentHandler` do not exist

## Related ADRs
- ADR-0014 (Orders) — Order.Cancel() added for Payments BC
- ADR-0026 (Saga) — Payment.Cancel() compensation on OrderPlacementFailed
- ADR-0009 (TimeManagement) — PaymentWindowExpiredJob scheduling
