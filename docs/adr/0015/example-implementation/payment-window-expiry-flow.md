# Payment Window Expiry Flow

## Scheduling

Implementation: [OrderPlacedHandler.cs](../../../../ECommerceApp.Application/Sales/Payments/Handlers/OrderPlacedHandler.cs)
creates the payment inside a Payments unit-of-work transaction, records the outbox message
with `IProcessedMessageGuard`, and schedules `PaymentWindowExpiredJob.JobTaskName` with the
database payment ID and `OrderPlaced.ExpiresAt`.

## Execution

[PaymentWindowExpiredJob.cs](../../../../ECommerceApp.Application/Sales/Payments/Handlers/PaymentWindowExpiredJob.cs)
reports failure for a missing or non-integer entity ID. It reports a successful no-op when
the payment is missing or no longer `Pending`. Otherwise it calls `Payment.Expire()`, updates
the payment, and enqueues `PaymentExpired(PaymentId, OrderId, OccurredAt, CorrelationId)` in
the outbox before committing.

```text
OrderPlaced
	-> Payments.OrderPlacedHandler
	-> Payment.Pending + scheduled job
	-> PaymentWindowExpiredJob
	-> Payment.Expired + PaymentExpired outbox message
	-> Orders.OrderPaymentExpiredHandler -> Order.ExpirePayment()
```

Inventory and Coupons also consume `PaymentExpired`. Inventory may release its reservation
through an independent timeout; both expiry paths are designed to be idempotent.

## Related source

- [PaymentExpired.cs](../../../../ECommerceApp.Application/Sales/Payments/Messages/PaymentExpired.cs)
