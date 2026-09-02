# New Coupon Rule Guide

Rules are registered through `CouponWorkflowBuilder.DefineRule(name, category, syncEvaluator,
asyncEvaluator, parameterValidator, parameterSchema)`. `CouponRuleCategory` is `Scope`, `Discount`, or
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

A rule that takes parameters must also implement `ICouponRuleParameterSchema`, declared **on the
evaluator itself** next to the `Validate` that enforces it — there is deliberately no central schema
registry to drift out of sync. The schema drives the generated admin form, so it must agree with the
validator: every parameter marked `Required` has to be one the validator rejects when absent, and
`CouponRuleSchemaTests` asserts exactly that agreement across the whole vocabulary.

Two conventions the schema must respect:

- **Defaults come from `CouponsOptions`, not literals**, when the default is a business-policy number
  (`min-order-value`, `percentage-off`). The evaluator's fallback and the schema's `DefaultValue` read
  the same option, so they cannot diverge. Structural defaults that are not policy — a `quantity` of 1,
  an "unlimited" `maxUses` — stay in code.
- **`DefaultValue` is formatted with `InvariantCulture`**, because it round-trips through the form into
  persisted `RulesJson`.

`Min`/`Max` are inclusive bounds for the generated input control, not the authority on validity; the
validator is. For a strictly positive integer the schema states the smallest valid value (`1`), while a
strictly positive decimal states `0` and lets the validator reject the boundary.

The rules JSON wire format is owned by `CouponRuleDefinition.ParseList`/`SerializeList`. Never
hand-roll a `JsonSerializer` call for rules: `Category` is written as a string and needs the
`JsonStringEnumConverter` those methods configure. A duplicate serializer that omitted it once made
every string-category coupon fail to create while the unit suite stayed green, because the tests
serialized with the same omission they were reading back.

`CouponWorkflowBuilder.Build()` automatically adds the amendment §10
`coupon-oversize-guard` constraint when `fixed-amount-off` is present. Do not register a second
descriptor with that name. The evaluator applies only to fixed-amount parameters, compares the
amount with `CouponEvaluationContext.OriginalTotal`, and honors `BypassOversizeGuard`.

A new rule appears in the admin rule builder automatically once registered — the form is generated
from `ICouponService.GetRuleCatalog()`, which reads the registry. No view needs editing.
`coupon-oversize-guard` is excluded from that catalog because it is auto-injected, not admin-selectable.

`RulesJson` is an **internal representation**, not an admin-facing input. Nobody types rule JSON: the
form binds a typed `CouponRulesFormDto` and `CouponService` produces the JSON, taking each rule's
category from the registry descriptor rather than from the posted payload.

The one thing a new rule still needs by hand is **Polish display text** — add its name and every
parameter key to `ECommerceApp.Web/Areas/Sales/CouponRuleDisplayNames.cs`. The schema's `Label` stays
English and developer-facing; translation belongs to the presentation layer.
`CouponRuleDisplayNamesTests` walks the real registry and fails the build if an entry is missing, so a
new rule cannot reach the admin as raw kebab-case.

When adding a rule, add its `CouponRuleNames` constant, evaluator, optional async evaluator,
validator and parameter schema, registration in `Rules/Extensions.cs`, and focused unit tests. If the rule changes the
database model or cross-BC messages, update the amendment/ADR and the conformance checklist rather
than hiding that decision in this guide.
