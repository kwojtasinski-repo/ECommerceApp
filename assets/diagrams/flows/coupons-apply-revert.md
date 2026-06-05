# Coupons Apply and Revert Flow

Current implementation flow from coupon service and compensation handlers.

```mermaid
graph TD
    A(ApplyCouponAsync called) --> B(Check OrderExists)
    B --> C{Order exists}
    C -->|No| D([OrderNotFound])
    C -->|Yes| E(Get coupon by code)
    E --> F{Coupon available}
    F -->|No| G([CouponNotFound or CouponAlreadyUsed])
    F -->|Yes| H(Check per-order coupon limit and rules pipeline)
    H --> I{Rules pass and reduction > 0}
    I -->|No| J([RulesNotSatisfied or NoDiscountProduced])
    I -->|Yes| K(Mark coupon used + persist CouponUsed + app record)
    K --> L(Publish CouponApplied + OrderPriceAdjusted)
    L --> M([Applied])

    N(RemoveCouponAsync called) --> O(Find coupon use by order)
    O --> P{Found}
    P -->|No| Q([NoCouponApplied])
    P -->|Yes| R(Release coupon + delete CouponUsed)
    R --> S(Publish CouponRemovedFromOrder)
    S --> T([Removed])

    U(OrderCancelled or PaymentExpired message) --> V(Find all CouponUsed for order)
    V --> W(Release coupon + mark record reversed + delete CouponUsed)
```

References:

- ../../../docs/specifications/coupons-apply-revert.md
- ECommerceApp.Application/Sales/Coupons/Services/CouponService.cs
- ECommerceApp.Application/Sales/Coupons/Handlers/CouponsOrderCancelledHandler.cs
- ECommerceApp.Application/Sales/Coupons/Handlers/CouponsPaymentExpiredHandler.cs
