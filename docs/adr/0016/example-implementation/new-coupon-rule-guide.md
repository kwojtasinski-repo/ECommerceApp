# New Coupon Rule Guide

Rules are registered through `CouponWorkflowBuilder.DefineRule(name, category, syncEvaluator,
asyncEvaluator, parameterValidator)`. `CouponRuleCategory` is `Scope`, `Discount`, or
`Constraint`. The registry is assembled by `AddCouponRuleEngine`; it exposes descriptors through
`ICouponRuleRegistry` and keeps the vocabulary flat.

The current built-in vocabulary is:

- Scope: `order-total`, `per-product`, `per-category`, `per-tag`.
- Discount: `percentage-off`, `fixed-amount-off`, `free-item`, `gift-product`,
  `free-cheapest-item`.
- Constraint: `max-uses`, `max-uses-per-user`, `valid-date-range`, `min-order-value`,
  `special-event`, `first-purchase-only`.

Each rule should provide an evaluator and, where parameters need validation, an
`ICouponRuleParameterValidator`. Synchronous evaluators run in Tier 1; asynchronous evaluators
for database or cache checks run in Tier 2 only after Tier 1 passes. Evaluators return
`CouponRuleEvaluationResult`, not exceptions, for expected rule failures. Parameter lookups follow
the existing defaults-when-missing convention used by the evaluators.

`CouponWorkflowBuilder.Build()` automatically adds the amendment §10
`coupon-oversize-guard` constraint when `fixed-amount-off` is present. Do not register a second
descriptor with that name. The evaluator applies only to fixed-amount parameters, compares the
amount with `CouponEvaluationContext.OriginalTotal`, and honors `BypassOversizeGuard`.

When adding a rule, add its `CouponRuleNames` constant, evaluator, optional async evaluator and
validator, registration in `Rules/Extensions.cs`, and focused unit tests. If the rule changes the
database model or cross-BC messages, update the amendment/ADR and the conformance checklist rather
than hiding that decision in this guide.
