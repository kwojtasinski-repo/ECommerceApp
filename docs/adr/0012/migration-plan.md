## Migration plan

**Slice 1 (this ADR) - no Orders dependency, can start now:**

1. Add `Discontinued` to `ProductStatus`, add `UnpublishReason` enum to Catalog domain. Update
   `Unpublish(reason)`, add `Discontinue()`. Update `ProductUnpublished` message to carry
   `UnpublishReason`. Add `GetUnitPriceAsync(int id)` to `IProductService`, `IProductRepository`,
   and `ProductRepository` (Catalog BC prerequisite; `CatalogClientAdapter` depends on this).
2. Add `StockAvailabilityChanged` integration message in
   `Application/Inventory/Availability/Messages/`. Update `StockService` to inject
   `IMessageBroker` and publish `StockAvailabilityChanged` after `InitializeAsync`,
   `ReserveAsync`, `ReleaseAsync`, `ReturnAsync`, and `AdjustAsync`.
3. Create `Domain/Presale/Checkout/` with `CartLine`, `SoftReservation` (entity),
   `SoftReservationId`, `StockSnapshot`, `ICartLineRepository`, `ISoftReservationRepository`,
   `IStockSnapshotRepository`.
4. Create `Application/Presale/Checkout/` with `ICatalogClient`, `IStockClient`,
   `ICartService`, `CartService` (write-through cache), `ISoftReservationService`,
   `SoftReservationService` (DB + cache + `IDeferredJobScheduler`),
   `StockAvailabilityChangedHandler`, `SoftReservationExpiredJob`, DTOs, ViewModels,
   `PresaleOptions`.
5. Create `Infrastructure/Presale/Checkout/` with `PresaleDbContext` (`CartLines`,
   `SoftReservations`, `StockSnapshots` DbSets), EF configurations (composite PK for
   `CartLine`, `ValueGeneratedNever` for `StockSnapshot.ProductId`), repositories,
   `CatalogClientAdapter`, `StockClientAdapter`, DI registration. Register
   `SoftReservationExpiredJob` in TimeManagement DI alongside `PaymentWindowTimeoutJob`.
6. Generate EF migration `InitPresaleSchema` targeting `PresaleDbContext`.
7. Move `SoftHoldTtl` from `InventoryOptions` to `PresaleOptions.SoftReservationTtl`. Add
   `IValidateOptions<PresaleOptions>` startup validation.
8. Atomic switch: remove `ICheckoutSoftHoldService`, `CheckoutSoftHoldService`, `SoftHold`
   from Inventory codebase. Remove the `_softHoldService.RemoveAsync(...)` call from
   `StockService.ReserveAsync`. Update Inventory DI registration and `StockServiceTests`.
9. Create `StorefrontController` in `ECommerceApp.API/Controllers/Presale/` (BFF endpoint).
10. Write unit tests for `CartService`, `SoftReservationService`,
    `StockAvailabilityChangedHandler`, and `SoftReservationExpiredJob`.

**Slice 2 (unblocked — Sales/Orders BC complete):**

11. Add `ICheckoutService`, `CheckoutService` (internal sealed), and `CheckoutResult` to
    `Application/Presale/Checkout/Services/` and `Application/Presale/Checkout/Results/`.
    Register `ICheckoutService` → `CheckoutService` in `Extensions.cs`.
12. Add `ISoftReservationService.GetAllForUserAsync` and `GetPriceChangesAsync`. Add
    `SoftReservationPriceChangeVm` to `Application/Presale/Checkout/ViewModels/`. Add
    `ISoftReservationRepository.GetAllByUserIdAsync` to the repository interface and implement
    in `SoftReservationRepository`. Implement both new service methods in `SoftReservationService`.
13. Add `PlaceOrderLineDto` and `PlaceOrderFromPresaleDto` to `Application/Sales/Orders/DTOs/`.
    Add `IOrderService.PlaceOrderFromPresaleAsync` and implement in `OrderService` (uses supplied
    `UnitPrice` directly; bypasses `IOrderProductResolver` price lookup). `PlaceOrderAsync`
    (legacy `CartItemIds` path) is left unchanged.
14. Implement `CheckoutService.PlaceOrderAsync` coordination flow (§12). Expose a checkout
    endpoint in `StorefrontController` (or a dedicated `CheckoutController`) that calls
    `GetPriceChangesAsync` before confirming and `PlaceOrderAsync` on confirmation. Write unit
    tests for `CheckoutService`, `SoftReservationService.GetAllForUserAsync`, and
    `GetPriceChangesAsync`.
