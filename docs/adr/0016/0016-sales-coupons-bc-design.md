# ADR-0016: Sales/Coupons BC

## Status
Accepted — amended by §10; Slice 2 implementation is in progress.

## Date
2026-03-12

## Context
The legacy coupon handler coupled Coupons to Orders by mutating order coupon state and calculating
cost through coupon navigation properties. The target design makes Coupons the owner of coupon
lifecycle and lets Orders apply price changes through messages. Slice 1 established the migration
boundary; Slice 2 adds rule-based policies and multi-coupon application without reviving the legacy
`CouponType` model.

## Decision
Sales/Coupons is a bounded context in the Sales group. Its aggregate owns coupon availability,
`CouponUsed` records usage, and the application service coordinates repositories, rule evaluation,
transactions, and outbox messages. Orders remains responsible for its own price state.

Slice 1 defines the original one-time order-coupon flow and its `CouponApplied` /
`CouponRemovedFromOrder` coordination. Slice 2 defines `RulesJson`-backed policies with one scope,
one discount, and zero or more constraints; independent multi-coupon evaluation; a default maximum
of 5 coupons per order with a hard ceiling of 10; optimistic concurrency; `CouponApplicationRecord`
audit data; and `OrderPriceAdjusted` price events. The currently implemented application path also
caps a reduction at the effective order price and rejects a fixed amount that exceeds the original
order total.

Amendment §10 has precedence over conflicting text in this ADR: the oversize guard is automatically
added for fixed-amount rules, can be bypassed through `CouponEvaluationContext.BypassOversizeGuard`,
and Catalog name-change messages update display-only scope-target names. The amendment's
`EnableOversizeGuard` option is design intent, not a currently implemented option.

Implementation detail is intentionally kept in the companion documents:

- [Apply coupon flow](example-implementation/apply-coupon-flow.md)
- [New coupon rule guide](example-implementation/new-coupon-rule-guide.md)
- [Coupon evaluation context](example-implementation/coupon-evaluation-context.md)

## Consequences
- Coupons and Orders have explicit ownership and a one-way integration boundary.
- Rule policies, multi-coupon limits, audit records, and name synchronization are independently
  testable and extensible.
- Cross-context work is transactional on the Coupons side and delivered to Orders through outbox
  messages, so handlers must tolerate asynchronous delivery and no-op cancellation cases.
- Slice 2 retains legacy compatibility surfaces while migration is incomplete; `CouponType` remains
  excluded from the new rule model and must not be reintroduced into it.
- Fixed-amount oversize behavior is an amendment-controlled rule, and the current implementation's
  option surface does not yet expose the amendment's proposed deployment toggle.

## Alternatives
- Direct synchronous calls from CouponService into Order were rejected because they recreate tight
  coupling and leave partial state when the receiver fails.
- Storing only a coupon code on Order was rejected because usage, release, and audit need their own
  records and invariants.
- Migrating every legacy coupon feature before stabilizing the boundary was rejected; the staged
  Slice 1/Slice 2 approach limits blast radius.

## References
- [ADR-0014 — Sales/Orders BC Design](../0014/0014-sales-orders-bc-design.md)
- [ADR-0015 — Sales/Payments BC Design](../0015/0015-sales-payments-bc-design.md)
- [ADR-0010 — In-Memory Message Broker](../0010/0010-in-memory-message-broker-for-cross-bc-communication.md)
- [ADR-0006 — TypedId and Value Objects](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [Amendment §10 — Oversize Guard and Catalog Name Sync](amendments/a1-oversize-guard-and-catalog-name-sync.md)
- [Conformance checklist](checklist.md)
