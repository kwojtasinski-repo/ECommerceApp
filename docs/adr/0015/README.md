# ADR-0015: Sales/Payments BC

**Status**: Accepted
**BC**: Sales/Payments
**Last amended**: —

## What this decision covers
Design and current implementation of the `Payment` aggregate, payment-window expiry, payment
messages, Orders integration, and the Payments schema.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0015-sales-payments-bc-design.md | Context, decisions, consequences, alternatives, and references | Understanding Payments BC |
| checklist.md | Conformance rules for Payment aggregate and handlers | Code review |
| example-implementation/payment-state-machine.md | Payment status transitions: Pending→Confirmed→Cancelled | Working with payment lifecycle |
| example-implementation/payment-window-expiry-flow.md | PaymentWindowExpiredJob timing, Order.Cancel() trigger | Debugging payment timeouts |
| example-implementation/order-cancel-flow.md | Order.Cancel() domain method + OrderCancelled message publishing | Implementing cancellation |

## Key rules
- `Payment` state transitions are aggregate methods with explicit status guards
- `PaymentWindowExpiredJob` is owned by Payments BC, registered in TimeManagement
- Payments uses an outbox-backed, processed-message-guarded placement handler

## Related ADRs
- ADR-0014 (Orders) — order payment and cancellation state
- ADR-0026 (Saga) — `Payment.Cancel()` compensation on `OrderPlacementFailed`
- ADR-0009 (TimeManagement) — PaymentWindowExpiredJob scheduling
