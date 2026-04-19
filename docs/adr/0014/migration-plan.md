## Migration plan

Parallel Change — existing code untouched until the atomic switch.

**Phase 1 — Domain layer**
1. `Domain/Sales/Orders/OrderItemId.cs` — strongly-typed ID
2. `Domain/Sales/Orders/OrderProductId.cs` — typed ID (`int`) for `OrderItem.ItemId`
3. `Domain/Sales/Orders/OrderUserId.cs` — typed ID (`string`) for `Order.UserId` and `OrderItem.UserId`
4. `Domain/Sales/Orders/OrderEventId.cs` — typed ID for `OrderEvent`
5. `Domain/Sales/Orders/ValueObjects/OrderNumber.cs` — VO with `Generate()` factory and regex validation
6. `Domain/Sales/Orders/OrderCustomer.cs` — owned value object with validated string fields + address
7. `Domain/Sales/Orders/OrderEventType.cs` — `enum` stored as string via `HasConversion<string>()` in `OrderEventConfiguration` (no migration needed to add new values)
8. `Domain/Sales/Orders/OrderEvent.cs` — append-only child entity
9. `Domain/Sales/Orders/OrderItem.cs` — factory (`OrderProductId`, `OrderUserId`, `UnitCost >= 0`), no `RefundId`
10. `Domain/Sales/Orders/Order.cs` — factory (`OrderNumber`, `OrderUserId`, `OrderCustomer`), `_events` backing field, `AppendEvent` on every transition
11. `Domain/Sales/Orders/Events/OrderPaid.cs`, `OrderDelivered.cs`
12. `Domain/Sales/Orders/IOrderRepository.cs`, `IOrderItemRepository.cs`

**Phase 2 — Application layer**
13. `Application/Sales/Orders/Contracts/IOrderCustomerResolver.cs` — ACL interface
14. Result types: `PlaceOrderResult`, `OrderOperationResult`
15. DTOs: `PlaceOrderDto`, `AddOrderItemDto`, `UpdateOrderDto`
16. ViewModels: `OrderDetailsVm`, `OrderForListVm`, `OrderListVm`, `OrderItemVm`, `OrderItemListVm`
    (include `OrderCustomer` fields in order-level ViewModels)
17. `IOrderService`, `OrderService` (call `IOrderCustomerResolver.ResolveAsync` in `PlaceOrderAsync`),
    `IOrderItemService`, `OrderItemService`
18. `Application/Sales/Orders/Services/Extensions.cs`
19. Register in `Application/DependencyInjection.cs`
20. **Build must be green before proceeding**

**Phase 3 — Infrastructure layer**
21. `OrdersConstants.cs`, `OrdersDbContext.cs` (add `DbSet<OrderEvent>`), `OrdersDbContextFactory.cs`
22. `Infrastructure/Sales/Orders/Configurations/OrderEventConfiguration.cs` — cascade delete, backing field
23. `OrderConfiguration.cs` — `OwnsOne<OrderCustomer>`, `OrderNumber` conversion, `OrderUserId` conversion, events navigation
24. `OrderItemConfiguration.cs` — `OrderProductId` conversion, `OrderUserId` conversion, remove `RefundId` mapping
25. `Infrastructure/Sales/Orders/Adapters/OrderCustomerResolver.cs` — reads `UserProfileDbContext`
26. `OrderRepository.cs`, `OrderItemRepository.cs`
27. `Infrastructure/Sales/Orders/Extensions.cs` (register `IOrderCustomerResolver`)
28. Register in `Infrastructure/DependencyInjection.cs`
29. **Build must be green before proceeding**

**Phase 4 — Unit tests**
30. `UnitTests/Sales/Orders/OrderAggregateTests.cs` — update for `OrderNumber`, `OrderUserId`,
    `OrderCustomer`, and event log assertions on every state transition
31. `UnitTests/Sales/Orders/OrderItemTests.cs` — update for `OrderProductId`, `OrderUserId`;
    remove refund method tests
32. `UnitTests/Sales/Orders/ValueObjects/OrderNumberTests.cs` — VO validation, `Generate()` factory
33. `UnitTests/Sales/Orders/OrderCustomerTests.cs` — construction validation

**Phase 5 — DB migration (requires approval per migration policy)**
34. `dotnet ef migrations add InitSalesSchema --project Infrastructure --context OrdersDbContext`
    Creates: `sales.Orders` (with `OrderCustomer_*` columns, `Number varchar(25) UNIQUE`),
    `sales.OrderItems` (typed ID conversions, no `RefundId` column),
    `sales.OrderEvents` (with index on `(OrderId, OccurredAt)`).
35. Submit migration for approval — do not apply to production without sign-off.

**Phase 6 — Integration tests**
23. `IntegrationTests/Sales/Orders/` — service-level and API-level tests
24. All existing integration tests must still pass.

**Phase 7 — Atomic switch (only when all tests pass)**
25. Update `PaymentHandler.CreatePayment()` to call `order.MarkAsPaid(paymentId)` (new aggregate).
26. Update `CouponHandler.HandleCouponChangesOnOrder()` to call `order.AssignCoupon(couponUsedId, discountPercent)` — resolve `discountPercent` from `Coupon.Discount` within the Coupons BC.
27. Update `ItemHandler.HandleItemsChangesOnOrder()` — remove `orderItem.Item.Cost` navigation chain.
28. Update `OrderController` and `OrderItemController` (Web + API) to inject new `IOrderService` / `IOrderItemService`.
29. Remove old DI registrations for legacy `OrderService`, `OrderItemService`, `OrderRepository`, `OrderItemRepository`.
30. Run full test suite — confirm zero regressions.
31. Update `## Implementation Status` table below (mark all rows ✅ Done).
32. Update `docs/architecture/bounded-context-map.md` — move Sales/Orders from "In progress" to "Completed (switch pending)" or "Switched".
