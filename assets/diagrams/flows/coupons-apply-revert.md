# Coupons Apply Revert Flow

High-level coupon application and rollback conditions.
Detailed business rules will be maintained in docs/specifications.

```mermaid
graph TD
    A(User applies coupon) --> B(Resolve coupon scope and eligibility)
    B --> C{Eligible}
    C -->|No| D([Reject coupon])
    C -->|Yes| E(Apply discount to order context)
    E --> F(Record coupon usage intent)
    F --> G{Order finalization outcome}
    G -->|Order paid| H([Coupon usage confirmed])
    G -->|Order cancelled or expired| I(Revert coupon usage)
    I --> J([Coupon usable again under rules])
```

References:
- ../../../docs/specifications/coupons-apply-revert.md
- docs/adr/0016/0016-sales-coupons-bc-design.md
- docs/roadmap/README.md
