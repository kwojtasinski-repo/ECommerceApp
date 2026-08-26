# Order Product Snapshot

`OrderProductSnapshot` is nullable while an item is a cart line or while asynchronous placement
processing is pending. It stores the product display name and optional image file name as they were
resolved for the order. It does not replace `UnitCost`, which is captured earlier at cart-add time.

## Placement handler

`OrderPlacedSnapshotHandler` in
`ECommerceApp.Application/Sales/Orders/Handlers/OrderPlacedSnapshotHandler.cs` handles the
`OrderPlaced` message:

1. Load the order's items with `IOrderItemRepository.GetByOrderIdAsync`.
2. Resolve each `OrderItem.ItemId` through `IOrderProductResolver.ResolveAsync`.
3. Persist resolved values with `IOrderItemRepository.SetSnapshotsAsync`.

The handler is registered by `Application/Sales/Orders/Services/Extensions.cs`. The infrastructure
adapter `OrderProductResolver` delegates to `IProductService`. A missing Catalog product is left
without a snapshot rather than inventing display data.

## Recovery and persistence

`SnapshotOrderItemsJob` remains an `IScheduledTask` and processes unsnapshotted placed items in
batches. This covers transient Catalog failures or a failed message handler.

`OrderItemConfiguration` maps the owned snapshot to `sales.OrderItemSnapshots`, with required
`ProductName` and optional `ImageFileName`. View-model mapping must use null-safe snapshot access
because a newly placed item can be visible before the handler completes.

Verified symbols: `OrderProductSnapshot`, `IOrderProductResolver`, `OrderProductResolver`,
`OrderPlacedSnapshotHandler`, `SnapshotOrderItemsJob`, and `OrderItem.SetSnapshot`.
