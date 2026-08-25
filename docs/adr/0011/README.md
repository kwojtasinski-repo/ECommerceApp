# ADR-0011: Inventory/Availability BC — StockItem Aggregate Design

**Status**: Accepted — Amended
**BC**: Inventory/Availability
**Last amended**: 2025-06-27

## What this decision covers
Design of `StockItem` counter aggregate, `StockHold` entity, two-phase reservation,
deferred `StockAdjustmentJob` with coalescing, and cross-BC message subscriptions.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0011-inventory-availability-bc-design.md | Core design: StockItem, StockHold, two-phase reserve (§4 summary), §8a summary, cross-BC wiring (§9 summary) — implementation-heavy sections link to companions below | Understanding Inventory BC structure |
| amendments/a1-fulfillment-message-consumption.md | Implemented: ShipmentDelivered/Failed/PartiallyDelivered handlers replacing OrderShippedHandler | Working with Fulfillment→Inventory integration |
| checklist.md | Conformance rules for StockItem and Reservation | Code review — note: this file still uses the pre-existing `Reservation` naming, not yet reconciled with `StockHold`; see phase 19 in `.github/plans/` |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/stock-adjustment-algorithm.md | §8a detail: coalescing setpoint, job execution, retry/backoff, race behavior, rules | Implementing or debugging StockAdjustmentJob |
| example-implementation/two-phase-reservation-flow.md | Soft hold → guaranteed StockHold lifecycle with payment-window timeout | Working with the reservation state machine |
| example-implementation/cross-bc-message-wiring.md | §9 detail: current inbound/outbound message handlers, reconciled against `Extensions.cs` | Adding a new message subscription |

## Key rules
- `StockItem` never loads `StockHold` as a collection — always query separately
- `Adjust` is always deferred through `StockAdjustmentJob` — never inline
- Amendment A1 adds two new inbound handlers — check it before modifying message subscriptions

## Related ADRs
- ADR-0010 (message broker) — all cross-BC triggers
- ADR-0009 (TimeManagement) — `StockAdjustmentJob` uses `IDeferredJobScheduler`
- ADR-0017 (Fulfillment) — publisher of `RefundApproved`
