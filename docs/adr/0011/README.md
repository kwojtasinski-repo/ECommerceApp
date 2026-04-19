# ADR-0011: Inventory/Availability BC — StockItem Aggregate Design

**Status**: Accepted — Amended
**BC**: Inventory/Availability
**Last amended**: 2025-06-27

## What this decision covers
Design of `StockItem` counter aggregate, `Reservation` entity, two-phase reservation,
deferred `StockAdjustmentJob` with coalescing, and cross-BC message subscriptions.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0011-inventory-availability-bc-design.md | Core design: StockItem, Reservation, two-phase reserve, §8a summary, cross-BC wiring | Understanding Inventory BC structure |
| amendments/a1-fulfillment-message-consumption.md | Adds RefundApproved + ShipmentDispatched handler wiring | Working with Fulfillment→Inventory integration |
| checklist.md | Conformance rules for StockItem and Reservation | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/stock-adjustment-algorithm.md | Full §8a: deferred write, command coalescing, version-match delete, Flow A + Flow B | Implementing or debugging StockAdjustmentJob |
| example-implementation/two-phase-reservation-flow.md | Reserve → Confirm → Fulfill lifecycle with timeout | Working with reservation state machine |
| example-implementation/cross-bc-message-wiring.md | All inbound/outbound message handlers and their actions | Adding a new message subscription |

## Key rules
- `StockItem` never loads `Reservation` as a collection — always query separately
- `Adjust` is always deferred through `StockAdjustmentJob` — never inline
- Amendment A1 adds two new inbound handlers — check it before modifying message subscriptions

## Related ADRs
- ADR-0010 (message broker) — all cross-BC triggers
- ADR-0009 (TimeManagement) — `StockAdjustmentJob` uses `IDeferredJobScheduler`
- ADR-0017 (Fulfillment) — publisher of `RefundApproved`
