## §10 Design Amendments — CouponOversizeGuard and Catalog Name Sync (2025-06-27)

> **Status**: Agreed — not yet implemented. Amends §9.3, §9.8, §9.10.

### §10.1 `coupon-oversize-guard` Constraint Rule (amends §9.3)

A new constraint rule prevents wasteful application of fixed-amount coupons when the discount
exceeds the order total. While `FixedAmountOffEvaluator` already caps the reduction at
`Math.Min(amount, total)`, the guard prevents the coupon from being applied at all —
a better UX than silently applying a $50 coupon to a $2 order.

| Rule | Parameters | Notes |
|---|---|---|
| `coupon-oversize-guard` | `{}` | No parameters — auto-injected; applies only to fixed-amount discount coupons |

**Behaviour**:
- **Scope**: Fixed-amount coupons only. Percentage-based coupons are not affected (they scale
  naturally with the order total).
- **Evaluation**: Reads the `fixed-amount-off` rule's `amount` parameter from `CouponRuleDefinition[]`.
  If `amount > CouponEvaluationContext.OriginalTotal` → reject with
  `CouponRuleEvaluationResult.Failed("Coupon discount exceeds order total")`.
- **Auto-injection**: `CouponWorkflowBuilder` automatically appends `coupon-oversize-guard` to
  coupons that have a `fixed-amount-off` discount rule, unless explicitly excluded.
- **Default**: Enabled by default via `CouponsOptions.EnableOversizeGuard = true`.
  Configurable per-deployment; can be disabled for promotions that intentionally allow oversize.

**`CouponRuleNames`**: Add `public const string CouponOversizeGuard = "coupon-oversize-guard";`

**`CouponsOptions`** (amends §9.10):

```csharp
public sealed class CouponsOptions
{
    public int MaxCouponsPerOrder { get; set; } = 5;            // hard ceiling: 10
    public decimal DefaultMinOrderValue { get; set; } = 100m;
    public bool EnableOversizeGuard { get; set; } = true;       // NEW — default ON
}
```

**Multi-coupon interaction**: With `MaxCouponsPerOrder = 5`, if coupon A reduces the running
total to $2 and coupon B is a fixed $10 coupon, the guard evaluates against the **running total
after prior coupons** (from `CouponEvaluationContext`). This is consistent with the independent
evaluation model — each coupon sees the latest state.

### §10.2 Catalog → Coupons Name Sync (amends §9.8)

When product, category, or tag names change in the Catalog BC, `CouponScopeTarget.TargetName`
(display-only snapshot) must be updated. `CouponScopeTarget.UpdateTargetName(string)` already
exists in the domain — this amendment defines the full integration path.

**Catalog BC — new messages** (published by Catalog services on name change):

```csharp
// Application/Catalog/Products/Messages/
public record ProductNameChanged(int ProductId, string NewName, DateTime OccurredAt) : IMessage;
public record CategoryNameChanged(int CategoryId, string NewName, DateTime OccurredAt) : IMessage;
public record TagNameChanged(int TagId, string NewName, DateTime OccurredAt) : IMessage;
```

**Catalog BC — publish points**:
- `ProductService.UpdateProductAsync` → publish `ProductNameChanged` when name differs
- `CategoryService.UpdateCategoryAsync` → publish `CategoryNameChanged` when name differs
- `ProductTagService.UpdateTagAsync` → publish `TagNameChanged` when name differs

**Coupons BC — new repository interface**:

```csharp
// Domain/Sales/Coupons/
public interface IScopeTargetRepository
{
    Task<IReadOnlyList<CouponScopeTarget>> GetByTargetIdAndScopeTypeAsync(
        int targetId, string scopeType, CancellationToken ct = default);
    Task UpdateAsync(CouponScopeTarget target, CancellationToken ct = default);
}
```

**Coupons BC — new handlers**:

```csharp
// Application/Sales/Coupons/Handlers/
internal sealed class ProductNameChangedHandler : IMessageHandler<ProductNameChanged>
{
    // Loads all CouponScopeTarget where TargetId == message.ProductId
    //   AND ScopeType == "per-product"
    // Calls target.UpdateTargetName(message.NewName) for each
    // Persists via IScopeTargetRepository
}

internal sealed class CategoryNameChangedHandler : IMessageHandler<CategoryNameChanged>
{
    // Same pattern: ScopeType == "per-category"
}

internal sealed class TagNameChangedHandler : IMessageHandler<TagNameChanged>
{
    // Same pattern: ScopeType == "per-tag"
}
```

**Architecture test update**: `App_Coupons` test must add `CatalogMessages` to its allowed
dependency list in `BoundedContextDependencyTests.cs`.

**Edge case — product/category/tag deletion**: Catalog BC does not delete products — it
discontinues them (`ProductStatus.Discontinued`). The existing `ProductDiscontinued` message
does NOT trigger a scope target update. Coupons with stale scope targets are naturally
rejected at evaluation time (product not in cart → scope evaluator fails). No cleanup needed.
