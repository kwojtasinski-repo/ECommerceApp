## Conformance checklist

### Domain aggregate rules (per dotnet-instructions.md §16)
- [ ] `Order` and `OrderItem` live under `Domain/Sales/Orders/` with namespace `ECommerceApp.Domain.Sales.Orders`
- [ ] All properties on `Order` and `OrderItem` use `private set`
- [ ] `Order` has a `private Order()` parameterless constructor for EF Core
- [ ] `OrderItem` has a `private OrderItem()` parameterless constructor for EF Core
- [ ] `Order.Create(int customerId, int currencyId, OrderUserId userId, OrderNumber number, OrderCustomer customer)` static factory method exists
- [ ] `OrderItem.Create(OrderProductId itemId, int quantity, decimal unitCost, OrderUserId userId)` static factory method exists
- [ ] `OrderItem.UnitCost` property exists (captures price at cart-add time)
- [ ] `int? DiscountPercent { get; private set; }` property exists on `Order` — captures coupon discount (0–100) as aggregate state
- [ ] `Order.CalculateCost()` takes no parameters — converts stored `DiscountPercent` to a rate internally; no navigation chain to `CouponUsed.Coupon.Discount`
- [ ] `Order.AssignCoupon(int couponUsedId, int discountPercent)` validates `discountPercent` is 0–100 via `DomainException`
- [ ] `Order.MarkAsPaid(int paymentId)` returns `OrderPaid` domain event — **superseded**: `ConfirmPayment(int paymentId)` appends `OrderPaymentConfirmed` event, transitions `Status` to `PaymentConfirmed`
- [ ] `Order.MarkAsDelivered()` returns `OrderDelivered` domain event — **superseded**: `Fulfill()` appends `OrderFulfilled` event, transitions `Status` to `Fulfilled`
- [ ] `Events/OrderPaid.cs` and `Events/OrderDelivered.cs` do NOT exist — deleted at atomic switch (§4)
- [ ] `OrderStatus Status { get; private set; }` exists on `Order` (§16)
- [ ] `bool IsPaid`, `bool IsDelivered`, `bool IsCancelled`, `DateTime? CancelledAt`, `DateTime? Delivered` do NOT exist on `Order` (§16)
- [ ] `int? PaymentId`, `int? RefundId` do NOT exist on `Order` — values are in event payloads (§17)
- [ ] `int? CouponUsedId`, `int? DiscountPercent` exist on `Order` (deferred to Coupons BC — §18)
- [ ] Event payload records exist in `Domain/Sales/Orders/Events/Payloads/` (§17)
- [ ] `AppendEvent<T>` is generic — serializes payload to JSON; no-payload overload passes `null` (§17)
- [ ] `UnitCost UnitCost { get; private set; }` on `OrderItem` — typed VO from `Domain/Shared/`, enforces `>= 0` (§14)
- [ ] `UnitCost` VO exists in `Domain/Shared/UnitCost.cs` (§14)
- [ ] `OrderItem.Create` accepts `UnitCost unitCost` parameter, not `decimal` (§14)
- [ ] `Order.CalculateCost()` uses `i.UnitCost.Amount` (§14)
- [ ] `OrderItemConfiguration` has `HasConversion` for `UnitCost` (§14)
- [ ] No `ApplicationUser` navigation property on `Order` or `OrderItem` — `OrderUserId UserId` only
- [ ] No cross-BC navigation properties on `Order` — `int CustomerId`, `int CurrencyId`, `int? PaymentId`, `int? CouponUsedId`, `int? RefundId` only
- [ ] No `Item` navigation property on `OrderItem` — `OrderProductId ItemId` only
- [ ] `Order.OrderItems` is `IReadOnlyList<OrderItem>` backed by `private readonly List<OrderItem> _orderItems`
- [ ] `OrderId(int)` and `OrderItemId(int)` are `sealed record` types extending `TypedId<int>`
- [ ] `OrderProductId(int)` has an implicit operator to/from `int`
- [ ] `OrderUserId(string)` has an implicit operator to/from `string`
- [ ] `OrderEventId(int)` typed ID exists in `Domain/Sales/Orders/`
- [ ] `OrderNumber` lives in `Domain/Sales/Orders/ValueObjects/` as a `sealed record`
- [ ] `OrderNumber` validates against regex `^ORD-\d{8}-[A-F0-9]{8}$` and throws `DomainException` on failure
- [ ] `OrderNumber.Generate()` static factory uses `DateTime.UtcNow` + `Guid.NewGuid()`
- [ ] `OrderEventType` is an `enum` stored as a string in the DB via EF Core `HasConversion<string>()` in `OrderEventConfiguration` — values: `OrderPlaced`, `OrderPaymentConfirmed`, `OrderPaymentExpired`, `OrderFulfilled`, `OrderCancelled`, `CouponApplied`, `CouponRemoved`, `RefundAssigned`, `RefundRemoved` (§13, §17)
- [ ] `Order` has `private readonly List<OrderEvent> _events` backing field; `EventType` is `OrderEventType` (enum)
- [ ] Every state transition method in `Order` calls `AppendEvent(...)` before returning
- [ ] `OrderEvent.OccurredAt` has no public setter; set only in constructor
- [ ] `OrderItem` has no `RefundId` property, no `AssignRefund()`, no `RemoveRefund()`
- [ ] `OrderItem.UnitCost` is `decimal` (not `Price` VO); `Create` validates `unitCost >= 0`
- [ ] `IOrderCustomerResolver` lives in `Application/Sales/Orders/Contracts/`
- [ ] Domain events `OrderPaid` and `OrderDelivered` are `record` types in `Domain/Sales/Orders/Events/`

### Infrastructure rules (per ADR-0003, ADR-0013)
- [ ] `OrdersDbContext` is `internal sealed` and lives under `Infrastructure/Sales/Orders/`
- [ ] `OrdersDbContext` uses schema `"sales"` via `modelBuilder.HasDefaultSchema(OrdersConstants.SchemaName)`
- [ ] `OrderRepository` and `OrderItemRepository` are `internal sealed`
- [ ] Both repositories inject `OrdersDbContext` — not the shared `Context` or any other BC's DbContext
- [ ] `OrderConfiguration` includes `builder.Navigation(o => o.OrderItems).HasField("_orderItems").UsePropertyAccessMode(PropertyAccessMode.Field)`
- [ ] `OrderItemConfiguration` maps `OrderItemId` with `HasConversion(x => x.Value, v => new OrderItemId(v)).ValueGeneratedOnAdd()`
- [ ] `OrderConfiguration` maps `OrderId` with `HasConversion(x => x.Value, v => new OrderId(v)).ValueGeneratedOnAdd()`
- [ ] `OrderItem.OrderId` FK configured with `OnDelete(DeleteBehavior.Cascade)` and `IsRequired(false)`
- [ ] `Infrastructure/Sales/Orders/Extensions.cs` registers `AddDbContext<OrdersDbContext>`, `IDbContextMigrator`, `IOrderRepository`, `IOrderItemRepository`, `IOrderCustomerResolver`
- [ ] `OrdersDbContext` includes `DbSet<OrderEvent>`
- [ ] `OrderConfiguration` configures `OwnsOne<OrderCustomer>()` with all required columns
- [ ] `OrderConfiguration` maps `OrderNumber` with `HasConversion<string>()` and `varchar(25)` + `UNIQUE` constraint
- [ ] `OrderConfiguration` maps `OrderUserId` with `HasConversion<string>()`
- [ ] `OrderEventConfiguration` sets `OnDelete(DeleteBehavior.Cascade)`, `HasField("_events")`, `UsePropertyAccessMode(PropertyAccessMode.Field)`
- [ ] `OrderItemConfiguration` uses `OrderProductId` and `OrderUserId` conversions; no `RefundId` mapping
- [ ] `OrderCustomerResolver` lives in `Infrastructure/Sales/Orders/Adapters/`

### Application service rules
- [ ] `OrderService` is `internal sealed` in `Application/Sales/Orders/Services/`
- [ ] `OrderItemService` is `internal sealed` in `Application/Sales/Orders/Services/`
- [ ] `IOrderService` and `IOrderItemService` are `public` interfaces
- [ ] All service methods are `async Task<T>` — no synchronous public methods
- [ ] `PlaceOrderResult` record exists with `Success(int orderId)`, `CustomerNotFound`, `CartItemsNotFound`, `CartItemsNotOwnedByUser` factory methods
- [ ] `OrderOperationResult` enum exists with `Success`, `OrderNotFound`, `AlreadyPaid`, `NotPaid`, `NotDelivered`, `AlreadyDelivered`, `CouponNotAssigned` values
- [ ] No `BusinessException` thrown for expected business outcomes in `OrderService` or `OrderItemService` — result types used instead
- [ ] `OrderService.PlaceOrderAsync` publishes `OrderPlaced` via `IMessageBroker` after persisting
- [ ] `OrderService.MarkAsDeliveredAsync` publishes `OrderShipped` via `IMessageBroker` after persisting
- [ ] Services map to ViewModels manually (no `IMapper` dependency) — new BC does not rely on legacy `MappingProfile`
- [ ] `Application/Sales/Orders/Services/Extensions.cs` registers both service pairs as `Scoped`

### Integration messages
- [ ] `OrderPlaced`, `OrderCancelled`, `OrderShipped` in `Application/Sales/Orders/Messages/` are unchanged
- [ ] `OrderRequiresAttention` in `Application/Sales/Orders/Messages/` — published for operator alerting (§19)

### Tests
- [ ] `UnitTests/Sales/Orders/OrderAggregateTests.cs` covers: `Create`, `CalculateCost`, `MarkAsPaid`, `MarkAsDelivered`, `AssignCoupon`, `RemoveCoupon`, `AssignRefund`, `RemoveRefund` and asserts an `OrderEvent` is appended for each transition
- [ ] `UnitTests/Sales/Orders/OrderItemTests.cs` covers: `Create`, `UpdateQuantity`, `ApplyCoupon`, `RemoveCoupon` (no refund tests)
- [ ] `UnitTests/Sales/Orders/ValueObjects/OrderNumberTests.cs` exists with validation and `Generate()` tests
- [ ] `UnitTests/Sales/Orders/OrderCustomerTests.cs` exists with construction validation tests
- [ ] All existing unit and integration tests still pass after new BC is registered in DI
