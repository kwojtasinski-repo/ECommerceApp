# ADR-0012: Presale/Checkout BC

**Status**: Accepted
**BC**: Presale/Checkout
**Last amended**: —

## What this decision covers
Design of the shopping cart, soft reservations, price-change detection, and
`CheckoutService.ConfirmAsync` which places an order via the Orders BC ACL.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0012-presale-checkout-bc-design.md | Core design: CartLine, SoftReservation, ICheckoutService, IOrderClient ACL, API endpoints | Understanding checkout flow |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Slice 1 + Slice 2 implementation steps (completed) | Historical reference |
| example-implementation/checkout-confirm-flow.md | POST /confirm full sequence: Presale→Orders→Payments | End-to-end checkout |
| example-implementation/soft-reservation-lifecycle.md | SoftReservation create→confirm→expire lifecycle | Working with reservations |
| example-implementation/price-change-detection.md | GET /price-changes: how stale prices are detected | Price validation logic |

## Key rules
- EC-001 decision: Accept the race condition on concurrent checkout — no distributed lock
- `IOrderClient` is the ACL — Presale never calls Orders BC directly
- Switch complete — no legacy CartController exists

## Related ADRs
- ADR-0011 (Inventory) — soft reservations translate to StockHolds
- ADR-0014 (Orders) — PlaceOrderFromPresaleAsync called via IOrderClient
- ADR-0015 (Payments) — payment initialized on OrderPlaced
