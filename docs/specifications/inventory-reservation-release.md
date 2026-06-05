# Flow: Inventory Reservation Release

> Domain: Inventory
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: ADR-0011 (Inventory/Availability), with integration constraints from ADR-0014 amendment A3 and saga behavior from ADR-0026
> Primary code area (informational only): Inventory/Availability release lifecycle in cross-BC order/payment/shipment flows

---

## Purpose

Ensure reserved stock is released safely, exactly once, and only under valid business conditions when an order path does not consume that stock.

---

## Scope

### What this spec covers

- Lifecycle of a stock hold from creation to any release-related terminal outcome.
- Release triggers from payment expiry, order cancellation, order-placement compensation, and manual operator withdrawal.
- Guarded behavior for duplicate/late release triggers (idempotent no-op).
- Terminal outcomes for a hold: released, fulfilled, or withdrawn.

### What this spec does NOT cover

- Initial cart soft-hold behavior before hard reservation creation.
- Payment authorization/capture internals.
- Shipment execution internals beyond release-relevant events.
- Inventory initialization, stock adjustment administration, or catalog publication workflow.
- UI routing, API contracts, and storage mechanics.

---

## Glossary

| Term | Meaning in this flow |
|---|---|
| Stock hold | Quantity blocked for a specific order line so it is not sold to another order |
| Guaranteed hold | Active hold awaiting payment result |
| Confirmed hold | Active hold after payment acceptance, awaiting shipment outcome |
| Release | Returning reserved quantity to available stock |
| Fulfillment | Consuming reserved quantity as delivered goods |
| Withdrawal | Operator-driven forced termination of an active hold |
| Idempotent handling | Repeated trigger does not cause duplicate stock movement |
| Terminal state | Hold state with no allowed further progression in this flow |

---

## Actors

- Customer: indirectly triggers release by not completing payment in time or by cancellation path.
- Payment subsystem: signals payment confirmation or payment expiry outcomes.
- Orders subsystem: signals cancellation and placement-failure compensation events.
- Fulfillment subsystem: signals delivered/failed/partial shipment outcomes.
- Inventory subsystem: owns hold state decisions and stock counter changes.
- Back-office operator: may manually withdraw an active hold.

---

## Entry conditions

All must be true:

- A hard stock hold exists for a specific order and product.
- The hold is associated with a valid quantity greater than zero.
- The hold is in an active state when a release attempt is evaluated (guaranteed or confirmed), unless the event is a duplicate/late signal that should resolve as no-op.

---

## Invariants and assumptions

- Inventory is the single owner of hold state transitions and stock counter effects.
- A release action is allowed only from eligible active states.
- Terminal states are non-reversible within this workflow.
- Duplicate release triggers must not reduce stock below valid bounds.
- A single business event may affect many holds for one order; each hold is evaluated independently.

---

## States (exhaustive for this workflow)

| State | Description | Terminal? |
|---|---|---|
| NotReserved | No hard hold exists yet for this order-product pair | No |
| Guaranteed | Hold created and active; payment still pending | No |
| Confirmed | Hold still active; payment accepted | No |
| Released | Hold ended with stock returned to available pool | Yes |
| Fulfilled | Hold ended by stock consumption for delivered goods | Yes |
| Withdrawn | Hold ended by operator-forced withdrawal with stock return | Yes |

---

## Events

- HoldCreated
- PaymentConfirmed
- PaymentExpired
- OrderCancelled
- OrderPlacementCompensationRequested
- ShipmentDelivered
- ShipmentFailed
- ShipmentPartiallyDelivered
- HoldWithdrawnByOperator
- ReleaseRequestedForTerminalHold (duplicate/late replay)
- ReleaseRequestedForMissingHold

---

## Transition rules

| From state | Event | Guard condition | To state | Notes |
|---|---|---|---|---|
| NotReserved | HoldCreated | Product and quantity are eligible | Guaranteed | Hard hold begins |
| Guaranteed | PaymentConfirmed | Hold is still active | Confirmed | No stock release here |
| Guaranteed | PaymentExpired | Hold still guaranteed at trigger time | Released | Standard timeout release |
| Guaranteed | OrderCancelled | Cancellation accepted | Released | Cancellation-driven release |
| Guaranteed | OrderPlacementCompensationRequested | Upstream placement failed after partial side effects | Released | Compensation release |
| Guaranteed | HoldWithdrawnByOperator | Operator has withdrawal authority | Withdrawn | Manual termination with stock return |
| Guaranteed | ShipmentFailed | Shipment failure affects guaranteed hold | Released | Defensive release path |
| Confirmed | OrderCancelled | Cancellation accepted in confirmed stage | Released | Return reserved stock |
| Confirmed | ShipmentFailed | Shipment failed for confirmed hold | Released | Failed delivery returns stock |
| Confirmed | ShipmentPartiallyDelivered | Item portion for this hold failed | Released | Per-line failed portion release semantics |
| Confirmed | HoldWithdrawnByOperator | Operator has withdrawal authority | Withdrawn | Exceptional manual resolution |
| Confirmed | ShipmentDelivered | Delivery succeeds for this hold | Fulfilled | Stock consumed, not released |
| Released | ReleaseRequestedForTerminalHold | Any duplicate/late release trigger | Released | Idempotent no-op |
| Fulfilled | ReleaseRequestedForTerminalHold | Any duplicate/late release trigger | Fulfilled | No reverse transition |
| Withdrawn | ReleaseRequestedForTerminalHold | Any duplicate/late release trigger | Withdrawn | No reverse transition |
| Any active state | ReleaseRequestedForMissingHold | No matching hold found | No state change | Treated as safe no-op |
| Confirmed | PaymentExpired | Payment expiry arrives after confirmation | Confirmed | No-op to prevent double-release |

---

## Business rules

| ID | Rule |
|---|---|
| BR-001 | Hard hold release is valid only for active holds; terminal holds are immutable in this flow. |
| BR-002 | Payment expiry may release only holds still awaiting payment outcome. |
| BR-003 | Cancellation and placement-failure compensation must release all related active holds for the order. |
| BR-004 | Shipment success consumes reserved stock (fulfillment), not release. |
| BR-005 | Shipment failure outcomes must return relevant reserved stock to availability. |
| BR-006 | Manual withdrawal is an explicit terminal action and cannot be undone within this workflow. |
| BR-007 | Replayed, duplicate, or late release triggers must be idempotent and produce no additional stock movement. |
| BR-008 | Missing-hold release requests must resolve safely (no-op) and not fail the global business process. |
| BR-009 | Confirmed holds are protected against timeout-style release paths to avoid double-release races. |
| BR-010 | Each hold in a multi-line order is evaluated independently, but order-level compensation must still complete for all lines. |
| BR-011 | Terminal outcomes for this workflow are exactly: Released, Fulfilled, Withdrawn. |
| BR-012 | Audit visibility must exist for release-relevant transitions and no-op replay decisions. |

---

## Edge cases

- Duplicate timeout/cancellation signals arrive for the same hold after a prior release.
- Timeout-like signal arrives after payment confirmation; hold must remain non-released.
- Compensation message is replayed after successful previous compensation.
- Multi-line order has mixed outcomes (some delivered, some failed); only failed portions are released.
- Operator withdraws a hold while other asynchronous events are still in flight.
- Release trigger references a hold that no longer exists or was never created.
- Parallel events attempt conflicting transitions near the same moment; deterministic state guards must prevent reverse/illegal transitions.
- Legacy and new expiry trigger paths overlap during migration windows; business outcome must still enforce single effective release per hold.

---

## Example scenarios

### Happy path - payment expires and reserved stock is released

1. A hard hold is created for an order line and enters Guaranteed.
2. Payment is not completed within the allowed window.
3. Payment expiry trigger is processed for that hold.
4. Hold transitions from Guaranteed to Released.
5. Reserved quantity is returned to available stock.

### Failure path - late duplicate release trigger after terminal transition

1. A hold has already reached Released due to a prior valid trigger.
2. The same release trigger (or equivalent replay) arrives again later.
3. Inventory evaluates current state as terminal.
4. No additional stock movement is performed.
5. Hold remains Released and the replay is treated as idempotent no-op.

### Alternative terminal path - delivery succeeds

1. Hold moves from Guaranteed to Confirmed after payment acceptance.
2. Shipment success event arrives for the held quantity.
3. Hold transitions to Fulfilled.
4. Reserved stock is consumed, not released.
5. Any subsequent release signal is ignored as terminal-state no-op.
