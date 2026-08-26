# Apply Coupon Flow

`CouponService.ApplyCouponAsync` is the application-facing flow for rule-based coupons. The
service first verifies the order through `OrderExistsQuery`, loads an available coupon by code,
and reads all existing `CouponUsed` records for the order. The configured limit is
`Math.Min(CouponsOptions.MaxCouponsPerOrder, 10)`; reaching it returns
`CouponApplyResult.OrderAlreadyHasCoupon`.

For an order with previous applications, the service loads its `CouponApplicationRecord` entries,
sums their reductions, and computes the effective price. A non-positive effective price returns
`NoDiscountProduced`. The coupon's `CouponRuleDefinition` list is evaluated by
`ICouponRulePipeline`; a failure returns `RulesNotSatisfied`. The current service also rejects a
fixed-amount rule whose amount exceeds the original order total before running the pipeline.

The pipeline reduction is capped at the effective price. If the resulting reduction is positive,
the service marks the coupon used, creates `CouponUsed.CreateForDbCoupon`, records the application,
and enqueues both `CouponApplied` and `OrderPriceAdjusted` in the Coupons unit-of-work transaction.
The result is `Applied`. Expected failures are result values: `OrderNotFound`, `CouponNotFound`,
`CouponAlreadyUsed`, `OrderAlreadyHasCoupon`, `RulesNotSatisfied`, and `NoDiscountProduced`.

`RemoveCouponAsync` finds the order's usage record, releases the coupon, deletes the usage record,
and enqueues `CouponRemovedFromOrder` in one transaction. Missing usage is the no-op result
`NoCouponApplied`.

## Amendment behavior

The §10 oversize guard is a constraint evaluator automatically added by
`CouponWorkflowBuilder` when a fixed-amount rule is registered. It passes when
`CouponEvaluationContext.BypassOversizeGuard` is true and otherwise fails when the `amount`
parameter exceeds `OriginalTotal`. The amendment's `EnableOversizeGuard` configuration property
is not present in the current `CouponsOptions` implementation.

Cancellation is handled separately: the coupon cancellation handler releases and removes usage when
the order has no matching record it returns immediately, and it does not publish a removal event for
an already-cancelled order.
