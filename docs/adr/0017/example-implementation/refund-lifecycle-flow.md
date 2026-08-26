# Refund Lifecycle Flow

This companion shows the implemented Slice 1 path from request through cross-context processing.
The architectural decision is in [ADR-0017](../0017-sales-fulfillment-bc-design.md).

## Request

1. The caller submits `RequestRefundDto` with `OrderId`, `UserId`, reason, warranty flag, and one or
	more product/quantity lines.
2. `RefundService.RequestRefundAsync` sends `OrderExistsQuery` through the module client.
3. A missing order returns `RefundRequestResult.OrderNotFound`.
4. An existing requested refund for the order returns `RefundRequestResult.RefundAlreadyExists`.
5. Otherwise `Refund.Create` validates the IDs, user, reason, and item list. The service persists a
	`Refund` in `Requested` state and returns `RefundRequestResult.Requested`.

## Approve

1. `ApproveRefundAsync` loads the aggregate. A missing refund returns `RefundNotFound`; a non-requested
	refund returns `AlreadyProcessed`.
2. `Refund.Approve()` sets `Approved` and `ProcessedAt`.
3. The service maps every `RefundItem` to `RefundApprovedItem` and updates the aggregate.
4. In the same Fulfillment transaction, the outbox receives one Fulfillment-owned `RefundApproved`
	message containing `RefundId`, `OrderId`, all items, and `OccurredAt`.
5. The outbox delivers the event to Orders, Payments, Inventory, and Communication. Payments calls
	`ProcessRefundAsync(orderId, refundId)` once; Inventory returns stock for each item.

## Reject

1. `RejectRefundAsync` applies the same not-found and already-processed guards.
2. `Refund.Reject()` sets `Rejected` and `ProcessedAt`.
3. The aggregate update and one `RefundRejected` outbox message commit together.
4. Communication may notify the customer; no payment or stock reversal is performed.

## Relevant implementation surfaces

- `ECommerceApp.Domain/Sales/Fulfillment/Refund.cs`
- `ECommerceApp.Application/Sales/Fulfillment/Services/RefundService.cs`
- `ECommerceApp.Application/Sales/Fulfillment/Messages/RefundApproved.cs`
- `ECommerceApp.Application/Sales/Payments/Services/IPaymentService.cs`
- `ECommerceApp.Application/Inventory/Availability/Handlers/RefundApprovedHandler.cs`
