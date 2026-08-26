# Order Aggregate Usage

This companion records the current domain surface. The aggregate root is
`ECommerceApp.Domain.Sales.Orders.Order`.

## Construction and pricing

- `Order.Create(customerId, currencyId, userId, number, customer)` validates the aggregate
	boundary, creates a `Placed` order, and appends `OrderPlaced` to `Events`.
- `OrderItem.Create(itemId, quantity, unitCost, userId)` creates a cart item. `unitCost` is
	`ECommerceApp.Domain.Shared.UnitCost`, so zero is valid and negative values are rejected.
- `Order.CalculateCost()` reads the aggregate's `DiscountPercent` and each
	`OrderItem.UnitCost.Amount`; it takes no discount parameter.
- `Order.AssignCoupon(couponUsedId, discountPercent)` updates the order and all child items,
	recalculates cost, and appends `CouponApplied`. `RemoveCoupon()` reverses that state.

## Lifecycle methods

Use domain methods rather than mutating state:

| Operation | Domain method | Resulting event/status |
|---|---|---|
| Confirm payment | `ConfirmPayment(paymentId)` | `OrderPaymentConfirmed` / `PaymentConfirmed` |
| Fulfil order | `Fulfill()` | `OrderFulfilled` / `Fulfilled` |
| Cancel manually | `Cancel(reason)` | `OrderCancelled` / `Cancelled` |
| Expire payment | `ExpirePayment()` | `OrderPaymentExpired` / `Cancelled` |
| Assign/remove refund | `AssignRefund(refundId)` / `RemoveRefund()` | refund event; order-level operation |

`Events` is read-only and backed by the aggregate's private event list. Event payloads are
serialized by `AppendEvent` and persisted with the status change. `PaymentId` and `RefundId` are
not scalar `Order` properties.

## Verified code locations

- Aggregate: `ECommerceApp.Domain/Sales/Orders/Order.cs`
- Child: `ECommerceApp.Domain/Sales/Orders/OrderItem.cs`
- Event types and payloads: `ECommerceApp.Domain/Sales/Orders/OrderEventType.cs` and
	`ECommerceApp.Domain/Sales/Orders/Events/Payloads/`
- Aggregate tests: `ECommerceApp.UnitTests/Sales/Orders/OrderAggregateTests.cs`
