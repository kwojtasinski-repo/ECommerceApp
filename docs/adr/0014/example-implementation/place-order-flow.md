# Place-Order Flow

The current implementation is `OrderService.PlaceOrderAsync` in
`ECommerceApp.Application/Sales/Orders/Services/OrderService.cs`.

1. `ICustomerExistenceChecker.ExistsAsync` rejects a missing customer with
	`PlaceOrderResult.CustomerNotFound`.
2. `IOrderItemRepository.GetByIdsAsync` loads the requested cart items. An empty result returns
	`CartItemsNotFound`; an item owned by another user returns `CartItemsNotOwnedByUser`.
3. `IOrderCustomerResolver.ResolveAsync` obtains the placement-time `OrderCustomer` snapshot.
4. The service generates an `OrderNumber` and calls `Order.Create`, then begins the Orders unit of
	work through `IOrdersUnitOfWork.BeginTransactionAsync`.
5. The order is added, cart items are assigned with `AssignToOrderAsync`, and the aggregate is
	reloaded with items/events before `CalculateCost()` and update.
6. `IOutboxWriter.EnqueueAsync` stores `OrderPlaced` in the same transaction. The transaction is
	committed only after the aggregate and outbox work are ready.
7. The result is `PlaceOrderResult.Success(orderId)`. An outbox enqueue failure records
	`OrderPlacementFailed` and returns `PlacementFailed` according to the current compensation
	contract.

`OrderPlaced` carries order ID, line IDs and quantities, user ID, expiry, occurrence time, total,
and currency. Its consumers perform downstream work asynchronously: Presale cleans up cart intent,
Payments initializes payment, and Inventory creates hard reservations. Product display snapshots
are handled by `OrderPlacedSnapshotHandler`; `SnapshotOrderItemsJob` is the recovery sweeper.

For delivery, `OrderService.MarkAsDeliveredAsync` loads the aggregate, calls `Fulfill()`, derives
the timestamp from the appended `OrderFulfilled` event, updates the order, and enqueues
`OrderShipped` in the same transaction.

Verified symbols: `PlaceOrderDto`, `PlaceOrderResult`, `IOrdersUnitOfWork`, `IOutboxWriter`,
`OrderPlaced`, `OrderShipped`, and `OrderPlacementFailed`.
