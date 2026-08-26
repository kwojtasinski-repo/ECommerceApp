# Payment State Machine

## Aggregate contract

Implementation: [Payment.cs](../../../../ECommerceApp.Domain/Sales/Payments/Payment.cs),
[PaymentStatus.cs](../../../../ECommerceApp.Domain/Sales/Payments/PaymentStatus.cs), and
[PaymentId.cs](../../../../ECommerceApp.Domain/Sales/Payments/PaymentId.cs).

`Payment.Create` starts in `Pending`. `Confirm` moves `Pending` to `Confirmed` and returns
`PaymentConfirmedEvent`. `Expire` moves `Pending` to `Expired` and returns
`PaymentExpiredEvent`. `IssueRefund` or `Refund` moves `Confirmed` to `Refunded`.
`Cancel` moves `Pending` to `Cancelled` for order-placement compensation.

```text
Pending --Confirm------> Confirmed --IssueRefund/Refund--> Refunded
Pending --Expire-------> Expired
Pending --Cancel-------> Cancelled
```

Every transition is guarded by the current status. Invalid transitions throw
`DomainException`; message handlers treat already-terminal states as no-ops where their
contract requires retry tolerance. Domain events are aggregate return values, while
`PaymentConfirmed` and `PaymentExpired` are cross-BC messages.

## Related source

- [PaymentConfirmedEvent.cs](../../../../ECommerceApp.Domain/Sales/Payments/Events/PaymentConfirmedEvent.cs)
- [PaymentExpiredEvent.cs](../../../../ECommerceApp.Domain/Sales/Payments/Events/PaymentExpiredEvent.cs)
- [RefundIssuedEvent.cs](../../../../ECommerceApp.Domain/Sales/Payments/Events/RefundIssuedEvent.cs)
- [PaymentRefundedEvent.cs](../../../../ECommerceApp.Domain/Sales/Payments/Events/PaymentRefundedEvent.cs)
- [OrderPlacementFailedHandler.cs](../../../../ECommerceApp.Application/Sales/Payments/Handlers/OrderPlacementFailedHandler.cs)
