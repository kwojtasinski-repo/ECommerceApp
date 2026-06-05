# Flow: Coupons Apply-Revert

> Domain: Coupons
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0016/0016-sales-coupons-bc-design.md](docs/adr/0016/0016-sales-coupons-bc-design.md)

## Purpose

Define how a coupon is applied to an order and later reverted so discount entitlement is controlled, one-time usage is enforced, and coupon value can be safely returned when conditions require reversal.

## Scope

### What this spec covers

- Coupon application attempt for an existing order.
- Validation outcomes before coupon activation.
- Coupon activation on order and business event emission.
- Coupon revert initiated by user action or system compensation (for example cancellation/expiration paths).
- Final business outcomes: applied, rejected, reverted, or no-op revert.

### What this spec does NOT cover

- Coupon creation, campaign authoring, and catalog targeting configuration.
- Pricing/tax/shipping recalculation internals.
- UI routing, endpoint contracts, storage schema, or internal integration mechanics.
- Multi-coupon optimization logic beyond configured eligibility/limit checks.

## Glossary

| Term | Meaning in this flow |
|---|---|
| Coupon token | A redeemable discount entitlement identified by a code |
| Coupon assignment | Active relation between one coupon token and one order |
| Apply attempt | A single request to activate a coupon on an order |
| Revert | Returning coupon entitlement and removing active assignment from order |
| Compensation revert | Revert triggered by order lifecycle compensation (not a direct user action) |
| No-op revert | Revert request when no active assignment exists |

## Actors

- Customer: applies or removes a coupon during checkout/order management.
- Administrator: applies or removes a coupon in back-office support scenarios.
- System: triggers compensation reverts from order lifecycle events.

## Entry conditions

All must be true to start an apply attempt:

- Order is present in the sales lifecycle and is still coupon-eligible by policy.
- A coupon code is provided.
- Coupon token is currently eligible for activation.
- Order has not exceeded coupon usage policy limits.
- Order currency/amount context is available for coupon rule checks.

All must be true to start a revert attempt:

- Revert is requested by customer/admin, or compensation event is received.
- The order context is resolvable.
- A coupon assignment may or may not exist (both outcomes are valid and handled).

## Invariants and assumptions

- A coupon token cannot be simultaneously active on multiple orders.
- Coupon activation and coupon return are explicit business state transitions.
- Revert is idempotent at business level: repeated revert requests do not create duplicate side effects.
- Business events represent confirmed state transitions, not intent.

## States

| State | Description | Terminal? |
|---|---|---|
| ReadyForApply | Order and coupon context available; no active assignment for this token-order pair | No |
| ApplyValidationInProgress | Business rules are being evaluated for apply | No |
| ApplyRejected | Apply attempt ended due to failed business rule(s) | Yes |
| AppliedActive | Coupon assignment is active on the order | No |
| RevertRequested | Revert was requested (user/admin/system) for an active or possibly inactive assignment | No |
| RevertValidationInProgress | System verifies whether a reversible active assignment exists | No |
| Reverted | Active assignment removed; coupon entitlement returned | Yes |
| RevertNoOp | Revert request had no active assignment to remove; treated as safe no-op | Yes |

## Events

- Coupon apply requested.
- Coupon validated for application.
- Coupon application rejected.
- Coupon applied to order.
- Coupon revert requested.
- Coupon assignment found for revert.
- Coupon assignment not found for revert.
- Coupon reverted from order.
- Coupon revert completed as no-op.

## Transition rules

| From state | Event | Guard condition | To state | Notes |
|---|---|---|---|---|
| ReadyForApply | Coupon apply requested | Entry conditions for apply are met | ApplyValidationInProgress | Begin rule evaluation |
| ApplyValidationInProgress | Coupon validated for application | All apply rules pass | AppliedActive | Assignment becomes active |
| ApplyValidationInProgress | Coupon application rejected | Any apply rule fails | ApplyRejected | Returns business rejection reason |
| AppliedActive | Coupon revert requested | Request originates from actor or compensation path | RevertRequested | Manual and automatic reverts share same business contract |
| RevertRequested | Coupon assignment found for revert | Active assignment exists | RevertValidationInProgress | Proceed to return entitlement |
| RevertRequested | Coupon assignment not found for revert | No active assignment exists | RevertNoOp | Idempotent terminal no-op |
| RevertValidationInProgress | Coupon reverted from order | Revert checks pass | Reverted | Assignment removed and entitlement returned |

## Business rules

| ID | Rule |
|---|---|
| BR-001 | Coupon can be applied only when the order exists and remains coupon-eligible by lifecycle policy. |
| BR-002 | Unknown coupon code is always rejected. |
| BR-003 | Coupon that is not currently eligible for use is rejected. |
| BR-004 | A coupon token can be active on only one order at a time. |
| BR-005 | An order cannot exceed configured coupon-count policy limits. |
| BR-006 | Successful apply creates exactly one active coupon assignment for the token-order relation. |
| BR-007 | Successful revert removes the active assignment and returns coupon entitlement for future eligible use. |
| BR-008 | Revert request with no active assignment ends as no-op, not as business failure. |
| BR-009 | Compensation revert follows the same entitlement-return semantics as manual revert. |
| BR-010 | Coupon apply/revert outcomes must be externally observable as business events for downstream consistency. |

## Edge cases

- Duplicate apply requests for the same order and coupon in close timing: only one active outcome is allowed; subsequent attempts must resolve to rejection.
- Revert arrives after prior successful manual remove: must resolve as RevertNoOp.
- Apply request races with cancellation/expiration compensation on the same order: final state depends on event ordering; system must converge to either AppliedActive or Reverted/RevertNoOp without duplicate active assignment.
- Order removed outside normal lifecycle governance can leave stale coupon references and require reconciliation.
- Policy limit boundary: request exceeding configured maximum coupons per order must be rejected deterministically.
- Fixed-amount oversize discount policy may reject apply when discount exceeds running order total, depending on active deployment policy.

## Example scenarios

### Happy path - coupon applied and later reverted by user

1. Order is coupon-eligible and customer submits valid coupon code.
2. Apply validation passes all rules.
3. Flow transitions to AppliedActive.
4. Customer requests coupon removal.
5. Active assignment is found and reverted.
6. Flow ends in Reverted; coupon can be used again when eligible.

### Failure path - apply rejected

1. Customer submits coupon code for an existing order.
2. Validation detects a rule violation (for example coupon already used or policy limit exceeded).
3. Flow transitions to ApplyRejected.
4. No active assignment is created.
5. User receives rejection reason and may retry with a different eligible coupon.
