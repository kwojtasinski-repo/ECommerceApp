# ADR-0026: Order Lifecycle Saga

**Status**: Superseded — implementation removed 2026-10-18 (legacy `PlaceOrderAsync(PlaceOrderDto)` had no production caller; order failures are handled by the saga engine, roadmap F3)
**BC**: Cross-BC (Sales/Orders, Sales/Payments, Inventory, Presale)

> **Superseded.** `OrderPlacementFailed`, its 3 handlers, `PlaceOrderAsync(PlaceOrderDto)` and the fan-out tests were deleted as unreachable. This record is kept for history only.

## What this decision covers
Option A compensation saga for failed order placement:
`OrderPlacementFailed` message + 3 handlers (Payments cancel, Inventory release, Presale cleanup).

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0026-order-lifecycle-saga.md | Full saga design: Option A, compensation handlers, Payment.Cancel() | Working with order placement failure handling |

## Key rules
- Saga uses compensation (rollback) not forward recovery
- `Payment.Cancel()` + `PaymentStatus.Cancelled` added for this saga
- 6 cross-BC integration tests cover the fan-out: `OrderPlacementFailedFanOutTests`

## Related ADRs
- ADR-0014 (Orders) — OrderPlacementFailed publisher
- ADR-0015 (Payments) — Payment.Cancel() compensation
- ADR-0011 (Inventory) — reservation release compensation
- ADR-0012 (Presale) — cart/reservation cleanup compensation
