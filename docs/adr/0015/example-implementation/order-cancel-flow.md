# Order Cancellation Flow

## Current Orders behavior

Implementation: [Order.cs](../../../../ECommerceApp.Domain/Sales/Orders/Order.cs) owns the
status transition. `Order.Cancel(reason)` is valid only from `Placed`, sets status to
`Cancelled`, and appends `OrderCancelled` with the reason payload. Payment expiry uses the
parallel `Order.ExpirePayment()` method, which also requires `Placed`, sets `Cancelled`, and
appends `OrderPaymentExpired`.

## Message handlers

[OrderPaymentExpiredHandler.cs](../../../../ECommerceApp.Application/Sales/Orders/Handlers/OrderPaymentExpiredHandler.cs)
loads the order, no-ops when it is missing or no longer `Placed`, then calls `ExpirePayment()`
and persists the order. The handler does not mutate the aggregate from Payments and does not
publish a second `OrderCancelled` message; the order event is recorded by the Orders model.

[OrderPaymentConfirmedHandler.cs](../../../../ECommerceApp.Application/Sales/Orders/Handlers/OrderPaymentConfirmedHandler.cs)
similarly no-ops unless the order is `Placed`, then calls `ConfirmPayment(message.PaymentId)`.

```text
PaymentExpired -> OrderPaymentExpiredHandler -> Order.ExpirePayment()
PaymentConfirmed -> OrderPaymentConfirmedHandler -> Order.ConfirmPayment(paymentId)
```

## Related source

- [OrderCancelled.cs](../../../../ECommerceApp.Application/Sales/Orders/Messages/OrderCancelled.cs)
- [OrderEventType.cs](../../../../ECommerceApp.Domain/Sales/Orders/OrderEventType.cs)
