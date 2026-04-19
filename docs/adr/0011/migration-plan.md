## Migration plan

1. Create `Domain/Inventory/Availability/` with `StockItem`, `StockItemId`, `Reservation`,
   `ReservationId`, `ReservationStatus`, `ProductSnapshot`, `CatalogProductStatus`,
   repository interfaces, domain events.
2. Create `Infrastructure/Inventory/Availability/` with `AvailabilityDbContext`,
   `StockItemConfiguration` (`RowVersion` → `IsRowVersion()`), `ReservationConfiguration`,
   `ProductSnapshotConfiguration`, all three repositories.
3. Create `Application/Inventory/Availability/` with `IStockService`, `StockService`,
   all message handlers, `PaymentWindowTimeoutJob`, DTOs, ViewModels.
4. Register via `AddAvailabilityServices()` in `Application/DependencyInjection.cs` and
   `AddAvailabilityInfrastructure()` in `Infrastructure/DependencyInjection.cs`.
5. Generate EF migration `InitInventorySchema` targeting `AvailabilityDbContext`.
6. Create data migration to copy `Items.Id`/`Items.Quantity` → `inventory.StockItems` and
   populate `inventory.ProductSnapshots` from existing product data.
7. Write unit tests for `StockItem` aggregate (reserve, release, fulfill, return, adjust,
   boundary invariants, concurrency guard).
8. Write unit tests for `StockService`.
9. Create message contracts (`OrderPlaced`, `OrderCancelled`, `PaymentConfirmed`,
   `OrderShipped`, `RefundApproved`) in the respective publisher `Messages/` folders.
10. Verify all existing tests still pass.
11. **Switch**: replace `ItemHandler.HandleItemsChangesOnOrder()` calls in `OrderService`
    with `IMessageBroker.PublishAsync(new OrderPlaced(...))`.
12. Remove `Item.Quantity`, `ItemHandler` stock logic, `IItemHandler`, and
    `GetAllAvailableItems`/`GetAvailableItemsCount` from `IItemRepository`/`ItemRepository`.
13. Delete `UnitTests/Services/Item/ItemHandlerTests.cs` only after new tests cover all
    equivalent scenarios.

No existing code is removed until Step 11. Parallel change strategy applies.
