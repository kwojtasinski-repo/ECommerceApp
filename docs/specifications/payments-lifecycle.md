# Flow: Payments Lifecycle

> Domain: Payments
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: ADR-0015 (Sales/Payments), ADR-0026 (Order Lifecycle Saga), ADR-0014 Amendment A3 (integration flow decisions)

---

## Purpose

Define how a payment moves from initialization to final business outcomes, ensuring consistent handling of successful payment, expiry, cancellation, and refund while protecting order and stock consistency.

---

## Scope

### What this spec covers

- Payment lifecycle from creation after order placement to terminal outcomes.
- Business events that move payment through states.
- Timeout behavior for unpaid payments.
- Compensation behavior when order placement fails after payment creation.
- Refund transition for already confirmed payment.

### What this spec does NOT cover

- Checkout item pricing and coupon calculation logic.
- Inventory reservation algorithms.
- Shipment and delivery lifecycle.
- UI routing details, technical integration contracts, and storage internals.

---

## Glossary

| Term | Meaning in this flow |
|---|---|
| Payment window | Time period in which customer can complete payment before it expires |
| Compensation | Corrective action taken when an upstream process fails after partial success |
| Payment confirmation | External or internal confirmation that funds were accepted |
| Payment expiry | Automatic closure of unpaid payment after window ends |
| Payment cancellation | Early voiding of pending payment due to order placement failure |
| Refund | Reversal after a previously confirmed payment |

---

## Actors

- Customer: initiates payment and may complete it within the allowed window.
- System: creates payment, tracks timeout, performs automatic expiry and compensation.
- Payment provider: confirms payment result.
- Operations/Backoffice: may review outcomes and trigger corrective operational actions where allowed.
- Refund process owner: authorizes refund decision that triggers payment reversal.

---

## Entry conditions

All conditions must be true:

- A valid order exists and is eligible for payment initiation.
- Payment amount and currency are fixed for this transaction attempt.
- Payment window end time is defined.
- The flow has not already produced a terminal payment outcome for the same order.

---

## Invariants and assumptions

- One active payment lifecycle exists per order at a time.
- Lifecycle outcomes are monotonic: terminal outcomes are not reversed within this flow.
- Expiry and confirmation are mutually exclusive outcomes for the same pending payment.
- Confirmed payment may transition to refunded if refund is approved.
- Cancellation due to compensation applies only before confirmation.

---

## States

| State | Description | Terminal? |
|---|---|---|
| Pending | Payment is initialized and waiting for customer confirmation | No |
| Confirmed | Payment accepted within payment window | No |
| Expired | Payment window closed without successful confirmation | Yes |
| Cancelled | Payment voided because order placement failed after payment initialization | Yes |
| Refunded | Previously confirmed payment has been reversed | Yes |

---

## Events

- Payment initialized
- Payment confirmation received
- Payment window elapsed
- Order placement failed compensation triggered
- Refund approved
- Duplicate or invalid transition attempt detected (no state change)

---

## Transition rules

| From state | Event | Guard condition | To state | Notes |
|---|---|---|---|---|
| None | Payment initialized | Entry conditions satisfied | Pending | Lifecycle starts |
| Pending | Payment confirmation received | Confirmation arrives before effective expiry and payment still pending | Confirmed | Successful payment |
| Pending | Payment window elapsed | No successful confirmation before deadline | Expired | Automatic timeout closure |
| Pending | Order placement failed compensation triggered | Upstream order placement failure detected before confirmation | Cancelled | Prevents orphan pending payment |
| Confirmed | Refund approved | Refund request approved for this payment | Refunded | Post-payment reversal |
| Confirmed | Payment confirmation received again | Already confirmed | Confirmed | No-op, idempotent handling |
| Expired | Any further business event | Terminal state reached | Expired | No-op |
| Cancelled | Any further business event | Terminal state reached | Cancelled | No-op |
| Refunded | Any further business event | Terminal state reached | Refunded | No-op |

---

## Business rules

| ID | Rule |
|---|---|
| BR-001 | Payment must start in Pending after successful initialization. |
| BR-002 | Payment confirmation is allowed only while payment is Pending. |
| BR-003 | If payment window ends while payment is still Pending, payment must become Expired. |
| BR-004 | Expired, Cancelled, and Refunded are terminal outcomes in this lifecycle. |
| BR-005 | Compensation cancellation is valid only when payment is still Pending. |
| BR-006 | Refund can be applied only to a Confirmed payment. |
| BR-007 | Duplicate confirmation must not create a second successful transition. |
| BR-008 | Lifecycle must prevent multiple active payment outcomes for the same order. |
| BR-009 | Timeout and confirmation races must resolve to a single final outcome without double transition. |
| BR-010 | When lifecycle action is not applicable to current state, state must remain unchanged and outcome must be explicitly classified as no-op or rejection. |

---

## Edge cases

- Confirmation arrives at the same time as timeout execution.
- Duplicate payment confirmation notifications are received.
- Payment initialization event is replayed for an order that already has an active or terminal payment.
- Compensation cancellation arrives after payment has already been confirmed.
- Refund approval arrives for payment that is not confirmed.
- Timeout execution is delayed; payment is already confirmed when timeout process runs.
- Payment exists but timeout scheduling failed, causing risk of prolonged Pending state.
- Payment provider confirms transaction after lifecycle already reached Expired or Cancelled.

---

## Example scenarios

### Happy path - payment completed successfully

1. Order becomes eligible for payment and payment is initialized.
2. Payment enters Pending.
3. Customer completes payment within the payment window.
4. Confirmation is received and validated.
5. Payment transitions to Confirmed.
6. No further lifecycle transition occurs unless a separate refund process is approved later.

### Failure path - payment window expires

1. Payment is initialized and enters Pending.
2. Customer does not complete payment before deadline.
3. Payment window elapsed event occurs.
4. Payment transitions from Pending to Expired.
5. Later confirmation attempts are treated as invalid or no-op for this lifecycle outcome.

### Compensation failure path - upstream placement failure after initialization

1. Payment is initialized and enters Pending.
2. Upstream order placement failure is detected.
3. Compensation cancellation is triggered.
4. Payment transitions from Pending to Cancelled.
5. Any later confirmation message is ignored as non-applicable.
