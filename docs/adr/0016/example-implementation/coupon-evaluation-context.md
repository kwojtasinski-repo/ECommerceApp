# Coupon Evaluation Context

`CouponEvaluationContext` is the immutable input passed to synchronous and asynchronous rule
evaluators:

```csharp
public CouponEvaluationContext(
	int orderId,
	string userId,
	decimal originalTotal,
	IReadOnlyList<CouponEvaluationItem> items,
	bool bypassOversizeGuard = false)
```

It carries the order identity, user identity, original order total, and the order items. Each
`CouponEvaluationItem` carries `ProductId`, `CategoryId`, tag IDs, unit price, and quantity.
Evaluators use `OriginalTotal` for the order-total baseline and `Items` for product, category, tag,
free-item, gift, and cheapest-item rules.

`BypassOversizeGuard` is the amendment §10 per-evaluation override. The oversize guard passes when
it is true; otherwise it reads the fixed-amount rule's `amount` parameter and fails when that amount
exceeds `OriginalTotal`. This is separate from the proposed `CouponsOptions.EnableOversizeGuard`
deployment setting, which is not currently implemented.

The context is supplied to `ICouponRuleEvaluator.Evaluate` and
`IAsyncCouponRuleEvaluator.EvaluateAsync`. The pipeline executes synchronous rules first with no
database work, then asynchronous rules only when Tier 1 has no failures. It accumulates reductions
and returns failure reasons through `CouponRulePipelineResult`.
