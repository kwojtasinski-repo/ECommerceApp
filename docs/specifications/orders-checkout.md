# Flow: Orders Checkout

> Domain: Orders
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0012/0012-presale-checkout-bc-design.md](docs/adr/0012/0012-presale-checkout-bc-design.md) (with related boundaries from [docs/adr/0014/0014-sales-orders-bc-design.md](docs/adr/0014/0014-sales-orders-bc-design.md) and [docs/adr/0015/0015-sales-payments-bc-design.md](docs/adr/0015/0015-sales-payments-bc-design.md))
> Primary code area (informational only): Orders and Presale/Checkout business flow

---

## Purpose

Convert a customer's prepared cart into a placed order using locked checkout terms, while preventing inconsistent outcomes during reservation expiry or concurrent confirmation attempts.

---

## Scope

### What this spec covers

- Checkout initiation from a valid customer cart.
- Reservation-based checkout window (including expiry).
- Confirmation flow that either places an order or returns a recoverable business failure.
- Terminal outcomes for a single checkout attempt: placed, rejected, expired, or aborted.

### What this spec does NOT cover

- Post-order payment lifecycle (confirmation/refund internals).
- Warehouse/fulfillment execution after order placement.
- UI routing or API contract details.
- Persistence and technical implementation mechanics.

---

## Glossary

| Term | Meaning in this flow |
|---|---|
| Checkout initiation | Moment when checkout terms are locked for selected cart lines |
| Locked price | Price captured for the checkout attempt; used for order placement in this attempt |
| Reservation window | Limited time in which checkout can be confirmed |
| Reservation expiry | Automatic end of reservation window before confirmation completes |
| Confirmation attempt | Customer action to finalize and place the order |
| Business rejection | Controlled, expected rejection (for example unavailable quantity or missing reservation) |
| Terminal state | End state for the current checkout attempt |

---

## Actors

- Customer - starts checkout and confirms or abandons it.
- Orders subsystem - validates and creates the order.
- Payments subsystem - initializes payment context after order placement.
- Inventory subsystem - applies stock reservation effects after order placement.
- Scheduler/system worker - expires stale checkout reservations.

---

## Entry conditions

All must be true:

- Customer is authenticated.
- Cart contains at least one line.
- Cart lines are eligible for checkout at initiation time.
- Checkout attempt is started explicitly by the customer.

---

## Invariants and assumptions

- Checkout uses locked terms captured at initiation for this attempt.
- Expired reservations cannot be used for successful confirmation.
- A successful order placement is the business success endpoint of this flow.
- Cart cleanup tied to successful placement happens after the placed-order event, not at initiation.

---

## States

| State | Description | Terminal? |
|---|---|---|
| ReadyForInitiation | Customer has an eligible cart and may start checkout | No |
| InitiationInProgress | System is creating/updating reservation set for this attempt | No |
| ReservationActive | Reservation window is active; customer may review and confirm | No |
| ReservationPartiallyAvailable | Reservation created only for part of requested lines; customer must adjust | No |
| PriceChangeWarning | Reservation remains valid, but current catalog price differs from locked price for one or more lines | No |
| ConfirmationInProgress | Confirmation started; reservation set is protected during commit decision | No |
| OrderPlaced | Order successfully created from locked checkout terms | Yes |
| CheckoutRejected | Confirmation failed with business reason (no valid reservation, stock/business rejection, order creation rejection) | Yes |
| ReservationExpired | Reservation window ended before successful confirmation | Yes |
| AbortedByCustomer | Customer intentionally exits or cancels the checkout attempt | Yes |

---

## Events

- CheckoutInitiated
- ReservationCreated
- ReservationPartiallyCreated
- ReservationRefreshed (re-initiation refreshes lock window and terms)
- PriceDifferenceDetected
- CheckoutConfirmed
- ConfirmationRejected
- ReservationExpired
- CheckoutAborted
- OrderPlaced

---

## Transition rules

| From state | Event | Guard condition | To state | Notes |
|---|---|---|---|---|
| ReadyForInitiation | CheckoutInitiated | Entry conditions satisfied | InitiationInProgress | Start single checkout attempt |
| InitiationInProgress | ReservationCreated | At least one line reserved and none missing | ReservationActive | Full reservation success |
| InitiationInProgress | ReservationPartiallyCreated | Some lines unavailable at initiation | ReservationPartiallyAvailable | Customer must adjust |
| ReservationPartiallyAvailable | ReservationRefreshed | Customer updates cart and re-initiates | InitiationInProgress | New attempt over updated cart |
| ReservationActive | PriceDifferenceDetected | One or more lines have changed display price vs locked price | PriceChangeWarning | Warning is advisory |
| PriceChangeWarning | CheckoutConfirmed | Reservation still valid | ConfirmationInProgress | Customer accepts warning |
| ReservationActive | CheckoutConfirmed | Reservation still valid | ConfirmationInProgress | Direct confirm path |
| ConfirmationInProgress | OrderPlaced | Order accepted | OrderPlaced | Success terminal |
| ConfirmationInProgress | ConfirmationRejected | Business validation/order creation rejection | CheckoutRejected | Recoverable failure; new attempt possible |
| ReservationActive | ReservationExpired | Reservation window elapsed | ReservationExpired | Confirmation no longer valid |
| PriceChangeWarning | ReservationExpired | Reservation window elapsed | ReservationExpired | Warning no longer actionable |
| ReservationActive | CheckoutAborted | Customer exits/cancels | AbortedByCustomer | Explicit abort |
| PriceChangeWarning | CheckoutAborted | Customer exits/cancels | AbortedByCustomer | Explicit abort |

---

## Business rules

| ID | Rule |
|---|---|
| BR-001 | Checkout initiation requires an authenticated customer and a non-empty cart. |
| BR-002 | Checkout is based on reservation-window terms; confirmation outside this window must not succeed. |
| BR-003 | Locked price captured at initiation is authoritative for this checkout attempt. |
| BR-004 | Price-difference information is advisory; customer may still proceed while reservation remains valid. |
| BR-005 | Confirmation must use an all-or-fail decision for the current attempt: either order placed, or rejection with no success side effect for that attempt. |
| BR-006 | If confirmation fails after reservation commit starts, reservation state must remain recoverable for a subsequent attempt unless the reservation has already expired. |
| BR-007 | Cart lines must not be removed at checkout initiation; cleanup is tied to successful order placement. |
| BR-008 | Re-initiating checkout refreshes reservation terms/window and supersedes prior active reservation state for that customer attempt. |
| BR-009 | Expired reservation attempts must return a clear restart-required outcome, not a silent success/failure ambiguity. |
| BR-010 | Successful order placement ends the checkout flow, and downstream payment/stock processes continue outside this specification boundary. |

---

## Edge cases

- Reservation expiry races with customer confirmation at boundary time; system must yield deterministic outcome (placed or restart-required), never partial ambiguity.
- Customer refreshes/restarts checkout repeatedly; the latest reservation terms supersede previous active terms.
- Partial availability at initiation produces a valid partial state rather than a generic failure.
- Price changes between initiation and confirmation produce warning state without rewriting locked terms for the active attempt.
- Confirmation retried after a rejection should be evaluated against current valid reservation state, not stale prior attempt state.
- Cart must remain intact if initiation occurs but order is never placed (aligned with [known issue resolution KI-011 in .github/context/known-issues.md](.github/context/known-issues.md)).

---

## Example scenarios

### Happy path - order successfully placed

1. Customer with eligible cart initiates checkout.
2. Reservation is created for all requested lines and becomes active.
3. Customer confirms within reservation window.
4. System processes confirmation and places the order.
5. Flow ends in OrderPlaced.

### Failure path - reservation expired before confirmation

1. Customer initiates checkout and receives active reservation.
2. Customer waits past reservation window before confirming.
3. Expiry event closes reservation attempt.
4. Confirmation is rejected as restart-required.
5. Flow ends in ReservationExpired.

### Failure path - confirmation rejected by business constraints

1. Customer initiates checkout with active reservation.
2. Customer confirms within window.
3. During confirmation, order creation is rejected (business reason).
4. Checkout returns explicit rejection outcome.
5. Flow ends in CheckoutRejected.
