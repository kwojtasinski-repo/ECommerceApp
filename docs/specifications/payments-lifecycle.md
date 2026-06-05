# Flow: Payments Lifecycle

> Domain: Sales/Payments
> Status: Draft
> Last verified: 2026-06-05
> Governing ADR: [docs/adr/0015/0015-sales-payments-bc-design.md](docs/adr/0015/0015-sales-payments-bc-design.md)

---

## Purpose

Describe only the payment lifecycle that is currently implemented in payment domain/service/handlers.

---

## Scope (current code only)

### Included

- Payment creation on `OrderPlaced`.
- Payment timeout expiration job.
- Payment confirmation API/service path.
- Compensation cancellation on `OrderPlacementFailed`.
- Refund processing on `RefundApproved`.

### Excluded

- Future payment event naming not present in code.
- Provider-specific integration contract details.

---

## Implemented statuses

`PaymentStatus`:

- `Pending`
- `Confirmed`
- `Expired`
- `Refunded`
- `Cancelled`

Source: `ECommerceApp.Domain/Sales/Payments/PaymentStatus.cs`.

---

## Lifecycle in code

### Create payment

- Trigger message: `OrderPlaced`
- Handler: `Sales.Payments.Handlers.OrderPlacedHandler`
- Action: `Payment.Create(...)` + persist + schedule `PaymentWindowExpiredJob`.

### Confirm payment

- Service method: `PaymentService.ConfirmAsync(...)`
- Domain action: `payment.Confirm(...)`
- Publishes message: `PaymentConfirmed`.

### Expire payment

- Job: `PaymentWindowExpiredJob`
- Guard: only when status is `Pending`
- Domain action: `payment.Expire()`
- Publishes message: `PaymentExpired`.

### Cancel payment (compensation)

- Trigger message: `OrderPlacementFailed`
- Handler: `Sales.Payments.Handlers.OrderPlacementFailedHandler`
- Domain action: `payment.Cancel()`
- Cancels scheduled timeout job.

### Refund payment

- Trigger message: `RefundApproved`
- Handler: `PaymentRefundApprovedHandler`
- Service action: `ProcessRefundAsync(...)` -> domain `payment.Refund(...)`.

Sources:

- `ECommerceApp.Application/Sales/Payments/Handlers/*.cs`
- `ECommerceApp.Application/Sales/Payments/Services/PaymentService.cs`
- `ECommerceApp.Domain/Sales/Payments/Payment.cs`

---

## Registered message handlers

Payments extension registers:

- `IMessageHandler<OrderPlaced>`
- `IMessageHandler<OrderPlacementFailed>`
- `IMessageHandler<RefundApproved>`
- scheduled task `PaymentWindowExpiredJob`

Source: `ECommerceApp.Application/Sales/Payments/Services/Extensions.cs`.

---

## Rules implemented now

- Only pending payment can be confirmed/expired/cancelled.
- Expiry job is no-op for non-pending payments.
- Confirm publishes `PaymentConfirmed`.
- Expire publishes `PaymentExpired`.
- Compensation failure cancels pending payment and timeout job.
- Refund applies to confirmed payment path through refund-approved message.
