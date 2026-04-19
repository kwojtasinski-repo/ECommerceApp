# ADR-0016: Sales/Coupons BC

**Status**: Accepted — Amended
**BC**: Sales/Coupons
**Last amended**: 2025-06-27

## What this decision covers
Design of `Coupon` aggregate, `CouponUsed` entity, rule-based coupon policy engine (Slice 2),
multi-coupon stacking strategy, and Catalog name sync.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0016-sales-coupons-bc-design.md | Core design: §1–§9 Coupon aggregate + Slice 2 rule pipeline | Understanding Coupons BC |
| amendments/a1-oversize-guard-and-catalog-name-sync.md | §10.1 CouponOversizeGuard constraint rule + §10.2 Catalog→Coupons name sync | Working with oversize guard or name sync handlers |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/apply-coupon-flow.md | ApplyCouponAsync multi-coupon stacking: Rule A + Rule B | Implementing coupon application |
| example-implementation/new-coupon-rule-guide.md | How to add a new ICouponRule evaluator | Extending the rules engine |
| example-implementation/coupon-evaluation-context.md | CouponEvaluationContext structure and usage | Writing rule evaluators |

## Key rules
- Max coupons per order: default 5, ceiling 10 (`CouponsOptions.MaxCouponsPerOrder`) — see also copilot-instructions.md §7
- Amendment §10.1: `CouponOversizeGuard` is always-on; `BypassOversizeGuard` per-coupon override
- Switch complete — legacy coupon DI removed

## Related ADRs
- ADR-0014 (Orders) — Order.AssignCoupon() / RemoveCoupon()
- ADR-0007 (Catalog) — ProductNameChanged / CategoryNameChanged / TagNameChanged messages
