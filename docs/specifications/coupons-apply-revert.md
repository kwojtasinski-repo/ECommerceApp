# Flow: Coupons Apply and Revert

> Domain: Sales/Coupons
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0016/0016-sales-coupons-bc-design.md](docs/adr/0016/0016-sales-coupons-bc-design.md)

---

## Purpose

Describe coupon apply/remove behavior exactly as currently implemented in coupon service and compensation handlers.

---

## Scope (current code only)

### Included

- `ApplyCouponAsync(...)` validation and successful apply path.
- `RemoveCouponAsync(...)` manual remove path.
- Compensation cleanup on `OrderCancelled` and `PaymentExpired`.
- Coupon usage and application-record updates.

### Excluded

- Future coupon event taxonomy not present in code.
- UI/API contract details outside observable service behavior.

---

## Apply operation (implemented)

Method: `CouponService.ApplyCouponAsync(...)`

Validation and outcomes currently implemented via `CouponApplyResult`:

- `Applied`
- `CouponNotFound`
- `CouponAlreadyUsed`
- `OrderAlreadyHasCoupon`
- `OrderNotFound`
- `RulesNotSatisfied`
- `NoDiscountProduced`

On success:

1. Marks coupon as used.
2. Creates `CouponUsed` entry.
3. Persists coupon/application record.
4. Publishes `CouponApplied` and `OrderPriceAdjusted`.

Sources:

- `ECommerceApp.Application/Sales/Coupons/Services/CouponService.cs`
- `ECommerceApp.Application/Sales/Coupons/Results/CouponApplyResult.cs`

---

## Remove operation (implemented)

Method: `CouponService.RemoveCouponAsync(orderId)`

Outcomes via `CouponRemoveResult`:

- `Removed`
- `NoCouponApplied`

On `Removed`:

1. Releases coupon.
2. Deletes `CouponUsed` entry.
3. Updates coupon.
4. Publishes `CouponRemovedFromOrder`.

Sources:

- `ECommerceApp.Application/Sales/Coupons/Services/CouponService.cs`
- `ECommerceApp.Application/Sales/Coupons/Results/CouponRemoveResult.cs`

---

## Compensation handlers (implemented)

### On `OrderCancelled`

- Handler: `CouponsOrderCancelledHandler`
- For all coupon uses on order:
  - release coupon,
  - mark application record reversed,
  - delete coupon-use record.

### On `PaymentExpired`

- Handler: `CouponsPaymentExpiredHandler`
- Same effective compensation behavior as cancellation path.

Registration source:

- `ECommerceApp.Application/Sales/Coupons/Services/Extensions.cs`

---

## Rules implemented now

- Apply requires order existence and available coupon.
- Max coupons per order enforced (`CouponsOptions.MaxCouponsPerOrder`, capped to 10 in service).
- Rule pipeline may reject apply.
- Remove and compensation paths are safe when no coupon-use rows exist.
- Compensation updates both entitlement state and application records.
