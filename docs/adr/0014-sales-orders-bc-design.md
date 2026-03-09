# ADR-0014: Sales/Orders BC — Order and OrderItem Aggregate Design

## Status
Proposed

## Date
2026-03-09

## Context

The current `Order` and `OrderItem` domain models live in `Domain/Model/` as anemic entities
with public setters, `ApplicationUser` navigation properties, and Law of Demeter (LoD) violations.
The most critical one is in `Order.CalculateCost()`:

```csharp
// Domain/Model/Order.cs — current state
var discount = (1 - (CouponUsed?.Coupon?.Discount / 100M) ?? 1);
```

This chains through three navigation properties (`CouponUsed → Coupon → Discount`), binding the
Orders aggregate to Coupons BC internals at the domain-model level.

Three coupling hotspots drive further pain:

| Location | Problem |
|---|---|
| `PaymentHandler.CreatePayment()` | Directly sets `order.IsPaid = true; order.PaymentId = paymentId` — Payments BC mutates Order state |
| `PaymentHandler.HandlePaymentChangesOnOrder()` | Directly clears `order.IsPaid = false; order.Payment = null` |
| `CouponHandler.HandleCouponChangesOnOrder()` | Writes `order.CouponUsedId`, `order.CouponUsed`, calls `order.CalculateCost()` from Coupons BC |

Both `Order` and `OrderItem` carry `ApplicationUser User { get; set; }` navigation properties, violating
the IAM boundary rule (ADR-0002 §8): domain models must only hold `string UserId`.

The `OrderService` is a fat service with 25+ methods. It orchestrates Items, Payments, Coupons, and
Customers BCs through synchronous calls to `_itemHandler`, `_paymentHandler`, and `_couponHandler`.
None of its methods are async. Error reporting is exclusively exception-based, even for
well-understood expected outcomes like "order not found" or "item not in stock".

Three integration messages already exist at `Application/Sales/Orders/Messages/` —
`OrderPlaced`, `OrderCancelled`, `OrderShipped` — and are actively consumed by the
Inventory/Availability BC (`OrderPlacedHandler`, `OrderCancelledHandler`, `OrderShippedHandler`).
These must be preserved unchanged.

A result-based error handling pattern is already established in the codebase via
`ReserveStockResult` (enum) in the Inventory BC (`Application/Inventory/Availability/DTOs/ReserveStockResult.cs`).
Per `dotnet-instructions.md § 4`, returning a result value is preferred over throwing `BusinessException`
for expected business outcomes. The Sales/Orders BC will apply this pattern consistently.

The existing `IOrderRepository` and `IOrderItemRepository` in `Domain/Interface/` are non-async,
do not extend `IGenericRepository<T>`, and are tightly coupled to the shared legacy `Context`.
New repository interfaces will be defined in the BC folder per ADR-0003.

## Decision

We will build the Sales/Orders bounded context as a parallel implementation per the
Parallel Change strategy (ADR-0002). No existing files are modified until the atomic switch.

### 1. Strongly-typed IDs

`OrderId(int)` and `OrderItemId(int)` — sealed records extending `TypedId<int>` (ADR-0006).
`int` IDs are used (not `Guid`) to maintain alignment with MSSQL identity columns and
existing FK relationships with Customer, Currency, and Payment tables.

### 2. Rich `Order` aggregate

```
Domain/Sales/Orders/Order.cs
```

Rules (per dotnet-instructions.md §16):

- All properties use `private set`.
- Private parameterless constructor for EF Core materialization.
- Static `Order.Create(int customerId, int currencyId, string userId, string number)` factory
  with invariant checks using `DomainException`.
- `int? DiscountPercent { get; private set; }` — captures the coupon discount (0–100) at
  assignment time. Stored as aggregate state; used internally by `CalculateCost()`.
- `CalculateCost()` — public, no parameters; converts `DiscountPercent` to a rate internally
  (`discountRate = 1 - DiscountPercent / 100m`). Eliminates the LoD violation.
  Enables cost simulation: create `Order` in memory, call `AssignCoupon`, read `Cost` without
  persisting — ideal for checkout price preview.
- State transitions return domain events:
  - `MarkAsPaid(int paymentId) → OrderPaid` domain event
  - `MarkAsDelivered() → OrderDelivered` domain event
- Mutations coordinated across the collection:
  - `AssignCoupon(int couponUsedId, int discountPercent)` — validates `discountPercent` is 0–100
    (`DomainException` otherwise), stores `DiscountPercent`, sets `CouponUsedId` on order and all
    items, then calls `CalculateCost()`.
  - `RemoveCoupon()` — clears coupon state and recalculates at full rate.
  - `AssignRefund(int refundId)` / `RemoveRefund()` — propagates to order items.
- `IReadOnlyList<OrderItem> OrderItems` backed by `private readonly List<OrderItem> _orderItems`.
- No `ApplicationUser` navigation property — `string UserId` only.
- `int CustomerId`, `int CurrencyId`, `int? PaymentId`, `int? RefundId`, `int? CouponUsedId`
  — foreign-key IDs only; no cross-BC navigation properties.

### 3. `OrderItem` child entity

```
Domain/Sales/Orders/OrderItem.cs
```

- `UnitCost` property — captures the item's price at the moment it is added to the cart.
  This is the key change that eliminates the `OrderItem.Item.Cost` navigation chain.
- Static `OrderItem.Create(int itemId, int quantity, decimal unitCost, string userId)` factory.
- State-mutation methods: `UpdateQuantity`, `ApplyCoupon`, `RemoveCoupon`, `AssignRefund`, `RemoveRefund`.
- No `ApplicationUser` navigation — `string UserId` only.
- No `Item` navigation property — `int ItemId` only.

### 4. Domain events

```
Domain/Sales/Orders/Events/OrderPaid.cs
Domain/Sales/Orders/Events/OrderDelivered.cs
```

Both are `record` types in past tense. Returned from aggregate state-transition methods.
The service layer is responsible for consuming them (e.g. publishing integration messages).

### 5. Result-based error handling (key design decision)

Expected business failure outcomes are communicated via result types, not exceptions.
`DomainException` is reserved for programming invariant violations (null IDs, invalid state).

**Result types:**

`Application/Sales/Orders/Results/PlaceOrderResult.cs` — record with factory methods:
- `Success(int orderId)` — order was created and persisted.
- `CustomerNotFound(int customerId)` — requested customer does not exist.
- `CartItemsNotFound` — none of the provided order-item IDs exist in the cart table.
- `CartItemsNotOwnedByUser` — one or more cart items belong to a different user.

`Application/Sales/Orders/Results/OrderOperationResult.cs` — enum for single-aggregate operations:
- `Success`
- `OrderNotFound`
- `AlreadyPaid`
- `NotPaid`
- `NotDelivered`
- `AlreadyDelivered`
- `CouponNotAssigned`

These mirror the `ReserveStockResult` enum pattern used in Inventory/Availability BC.

Service method signatures use these result types as return values. Controllers and API endpoints
map result values to HTTP responses; they never catch `BusinessException` for these paths.

### 6. Separated service responsibilities

Two `internal sealed` services behind public interfaces:

- `IOrderService` / `OrderService` — order lifecycle:
  `PlaceOrderAsync`, `GetOrderDetailsAsync`, `UpdateOrderAsync`, `DeleteOrderAsync`,
  `MarkAsDeliveredAsync`, `AddCouponAsync`, `RemoveCouponAsync`, `AddRefundAsync`,
  `RemoveRefundByRefundIdAsync`, `GetAllOrdersAsync` (paginated), `GetOrdersByUserIdAsync`,
  `GetOrdersByCustomerIdAsync`, `GetAllPaidOrdersAsync`, `GetCustomerIdAsync`.

- `IOrderItemService` / `OrderItemService` — cart management:
  `AddCartItemAsync`, `DeleteCartItemAsync`, `GetByIdAsync`, `GetCartItemsByUserIdAsync`,
  `GetCartItemIdsByUserIdAsync`, `GetAllPagedAsync`, `GetCartItemCountByUserIdAsync`.

All methods are fully async (`Task<T>`). Services inject `IOrderRepository` and/or
`IOrderItemRepository` from the Domain layer. No cross-BC service dependencies injected.

### 7. Own DbContext in `sales` schema

```
Infrastructure/Sales/Orders/OrdersDbContext.cs   — internal sealed
Infrastructure/Sales/Orders/OrdersDbContextFactory.cs
Infrastructure/Sales/Orders/OrdersConstants.cs   — SchemaName = "sales"
```

`OrdersDbContext` owns `DbSet<Order>` and `DbSet<OrderItem>`. Configured in `sales` schema.
The existing shared `dbo.Orders` / `dbo.OrderItem` tables are untouched until the atomic switch.

EF Core backing-field configuration for `Order.OrderItems`:
```csharp
builder.Navigation(o => o.OrderItems)
    .HasField("_orderItems")
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

`OnDelete(DeleteBehavior.Cascade)` on the `OrderItem.OrderId` nullable FK — when an order is
deleted, its items are deleted by the database; cart items (null `OrderId`) are unaffected.

### 8. Repository interfaces (Domain layer)

```
Domain/Sales/Orders/IOrderRepository.cs
Domain/Sales/Orders/IOrderItemRepository.cs
```

Custom async interfaces — not extending legacy `IGenericRepository<T>` (which requires
`BaseEntity` and is tied to the shared `Context`). Pattern matches `IStockItemRepository`
in the Inventory BC.

### 9. DI registration

Application layer: `Application/Sales/Orders/Services/Extensions.cs`
→ `AddOrderServices(this IServiceCollection)` registers both service scoped pairs.

Infrastructure layer: `Infrastructure/Sales/Orders/Extensions.cs`
→ `AddOrdersInfrastructure(this IServiceCollection, IConfiguration)` registers:
  - `AddDbContext<OrdersDbContext>` with `DefaultConnection`
  - `AddScoped<IDbContextMigrator, DbContextMigrator<OrdersDbContext>>`
  - `AddScoped<IOrderRepository, OrderRepository>`
  - `AddScoped<IOrderItemRepository, OrderItemRepository>`

Called from `Application/DependencyInjection.cs` and `Infrastructure/DependencyInjection.cs`
respectively. Old registrations are NOT removed until the atomic switch.

### 10. Integration messages — reuse as-is

`OrderPlaced`, `OrderCancelled`, `OrderShipped` in `Application/Sales/Orders/Messages/`
remain unchanged. `OrderService.PlaceOrderAsync` publishes `OrderPlaced` via `IMessageBroker`
after persisting the order. `MarkAsDeliveredAsync` publishes `OrderShipped`.

## Consequences

### Positive

- `Order.CalculateCost()` (no parameters) eliminates the LoD violation — `DiscountPercent` is
  stored as aggregate state, so the aggregate always knows how to calculate its own cost.
  No cross-BC navigation chain at the domain level.
- Simulation support: any service can create an `Order` in memory, call `AssignCoupon`, and
  read `Cost` without persisting — enables checkout price preview without side effects.
- `OrderItem.UnitCost` captures price at cart-add time — removes dependency on `Item.Cost`
  navigation at order-placement time.
- `PaymentHandler` coupling is broken: `Order.MarkAsPaid(paymentId)` is the only entry point
  for transitioning payment state; external mutation (`order.IsPaid = true`) becomes a
  compile error after the switch (private setter).
- Result objects provide explicit, type-safe error handling contracts without exception overhead
  on hot paths (e.g. "item out of stock" is checked on every add-to-cart).
- Consistent with `ReserveStockResult` pattern already in Inventory BC — no new patterns introduced.
- Fully async service layer reduces thread starvation risk under load.
- `OrdersDbContext` in `sales` schema isolates Orders persistence from legacy `dbo` tables.
- Two focused services reduce cognitive load vs. the current 25-method `OrderService`.

### Negative

- `PaymentHandler` must be updated before the atomic switch to call `order.MarkAsPaid(paymentId)`
  instead of mutating properties directly. This is a coordinated change with Payments BC migration.
- `CouponHandler` must be updated before the atomic switch to call
  `order.AssignCoupon(couponUsedId, discountPercent)` — the Coupons BC must resolve
  `discountPercent` (integer 0–100) from its own `Coupon` aggregate before calling in.
- `UnitCost` on `OrderItem` introduces a new responsibility at cart-add time: the caller must
  provide the current item price. This requires an ACL interface (`ICatalogClient`) or a
  direct lookup from the legacy `IItemService` during the transition period.
- Two separate schemas (`dbo` legacy, `sales` new) coexist until the switch — migration approval
  required before new tables can be created in production.
- Additional boilerplate: result type files per operation type.

### Risks & mitigations

- **Risk:** `PaymentHandler` still uses `order.IsPaid = true` after switch — compile error
  breaks payment flow. **Mitigation:** `PaymentHandler` is updated in the same PR as the
  atomic switch; the PR checklist must include payment integration tests passing.
- **Risk:** `CouponHandler` calls `order.CalculateCost()` and mutates `order.CouponUsedId`
  directly — both are compile errors after the switch (private setter; `CalculateCost()` has
  no `discountRate` parameter on the new aggregate). **Mitigation:** `CouponHandler` updated
  to call `order.AssignCoupon(couponUsedId, discountPercent)` where `discountPercent` is
  resolved from `Coupon.Discount` within the Coupons BC before calling into Sales/Orders.
- **Risk:** `ItemHandler.HandleItemsChangesOnOrder` reads `orderItem.Item.Cost` via navigation
  to adjust item quantities. After switch, `OrderItem` has no `Item` navigation property.
  **Mitigation:** `ItemHandler` must be updated to read item cost from the Catalog BC
  (`IProductService` or `ICatalogClient`) before the switch.
- **Risk:** Cart items (null `OrderId`) exist in both `dbo.OrderItem` and `sales.OrderItems`
  tables during the transition — split state. **Mitigation:** Switch is atomic per controller;
  `OrderItemController` and `OrderController` are updated in the same PR.
- **Risk:** Incomplete result-type coverage in controllers causes unhandled `null` returns
  after switch. **Mitigation:** Integration test for every controller action before merging
  the switch PR.

## Alternatives considered

- **Keep exception-based error handling** — rejected because `BusinessException` is wasteful
  on expected hot paths (add-to-cart stock check, "order already paid" check), and the
  `ReserveStockResult` pattern is already established and consistent within the codebase.
- **Use a generic `Result<T, E>` wrapper** — rejected in favour of the simpler named result
  record + `OrderOperationResult` enum approach already used by the Inventory BC
  (`ReserveStockResult`). The generic wrapper adds syntactic overhead without meaningful benefit
  given the narrow set of failure modes per operation.
- **Extend `IGenericRepository<T>`** — rejected because new BC aggregates use strongly-typed IDs
  (`OrderId`) and their own `OrdersDbContext`, neither of which is compatible with
  `GenericRepository<T>` (requires `BaseEntity` and the shared `Context`). Pattern matches
  the Inventory BC (`IStockItemRepository` does not extend `IGenericRepository<T>`).
- **Use `Guid` IDs** — rejected per `implementation-patterns.md` note: `int` IDs are preferred
  for BCs with FK relationships to existing MSSQL identity columns (Customer, Currency, Payment).
- **Single `IOrderService` for both orders and cart** — rejected in favour of two focused
  services (`IOrderService` + `IOrderItemService`) to match the separation already present in
  the legacy codebase and reduce the 25-method fat service problem.
- **Pass `discountRate` as a parameter to `CalculateCost(decimal discountRate)`** — rejected
  because it leaks the discount calculation responsibility to the caller (service layer must
  know how to compute the rate from the coupon), and it blocks the simulation use-case
  (checkout cannot preview the final cost without orchestrating the calculation externally).
  Storing `DiscountPercent` as aggregate state keeps calculation self-contained and allows
  any caller to `AssignCoupon(couponUsedId, discountPercent)` → read `Cost` in-memory.
- **Reuse existing `dbo.Orders` / `dbo.OrderItem` tables** — rejected to maintain the Parallel
  Change strategy. New tables in `sales` schema allow the new BC to be independently tested
  before the switch without risk of corrupting legacy data.

## Migration plan

Parallel Change — existing code untouched until the atomic switch.

**Phase 1 — Domain layer**
1. `Domain/Sales/Orders/OrderItemId.cs` — strongly-typed ID
2. `Domain/Sales/Orders/OrderItem.cs` — factory, `UnitCost`, private setters
3. `Domain/Sales/Orders/Order.cs` — factory, `DiscountPercent`, `CalculateCost()`, state transitions
4. `Domain/Sales/Orders/Events/OrderPaid.cs`, `OrderDelivered.cs`
5. `Domain/Sales/Orders/IOrderRepository.cs`, `IOrderItemRepository.cs`

**Phase 2 — Application layer**
6. Result types: `PlaceOrderResult`, `OrderOperationResult`
7. DTOs: `PlaceOrderDto`, `AddOrderItemDto`, `UpdateOrderDto`
8. ViewModels: `OrderDetailsVm`, `OrderForListVm`, `OrderListVm`, `OrderItemVm`, `OrderItemListVm`
9. `IOrderService`, `OrderService`, `IOrderItemService`, `OrderItemService`
10. `Application/Sales/Orders/Services/Extensions.cs`
11. Register in `Application/DependencyInjection.cs`
12. **Build must be green before proceeding**

**Phase 3 — Infrastructure layer**
13. `OrdersConstants.cs`, `OrdersDbContext.cs`, `OrdersDbContextFactory.cs`
14. `OrderConfiguration.cs`, `OrderItemConfiguration.cs`
15. `OrderRepository.cs`, `OrderItemRepository.cs`
16. `Infrastructure/Sales/Orders/Extensions.cs`
17. Register in `Infrastructure/DependencyInjection.cs`
18. **Build must be green before proceeding**

**Phase 4 — Unit tests**
19. `UnitTests/Sales/Orders/OrderAggregateTests.cs`
20. `UnitTests/Sales/Orders/OrderItemTests.cs`

**Phase 5 — DB migration (requires approval per migration policy)**
21. `dotnet ef migrations add InitSalesSchema --project Infrastructure --context OrdersDbContext`
22. Submit migration for approval — do not apply to production without sign-off.

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

## Implementation Status

| Layer | Status |
|---|---|
| Domain (aggregate, value objects, domain events, repository interfaces) | ⬜ Not started |
| Infrastructure (DbContext, schema, EF configs, repositories, DI) | ⬜ Not started |
| Application (DTOs, ViewModels, result types, service interface + impl, DI) | ⬜ Not started |
| Unit tests | ⬜ Not started |
| DB migration | ⬜ Pending approval |
| Integration tests | ⬜ Not started |
| Controller migration (Web + API atomic switch) | ⬜ Not started |
| Atomic switch | ⬜ After integration tests |

## Conformance checklist

### Domain aggregate rules (per dotnet-instructions.md §16)
- [ ] `Order` and `OrderItem` live under `Domain/Sales/Orders/` with namespace `ECommerceApp.Domain.Sales.Orders`
- [ ] All properties on `Order` and `OrderItem` use `private set`
- [ ] `Order` has a `private Order()` parameterless constructor for EF Core
- [ ] `OrderItem` has a `private OrderItem()` parameterless constructor for EF Core
- [ ] `Order.Create(int customerId, int currencyId, string userId, string number)` static factory method exists
- [ ] `OrderItem.Create(int itemId, int quantity, decimal unitCost, string userId)` static factory method exists
- [ ] `OrderItem.UnitCost` property exists (captures price at cart-add time)
- [ ] `int? DiscountPercent { get; private set; }` property exists on `Order` — captures coupon discount (0–100) as aggregate state
- [ ] `Order.CalculateCost()` takes no parameters — converts stored `DiscountPercent` to a rate internally; no navigation chain to `CouponUsed.Coupon.Discount`
- [ ] `Order.AssignCoupon(int couponUsedId, int discountPercent)` validates `discountPercent` is 0–100 via `DomainException`
- [ ] `Order.MarkAsPaid(int paymentId)` returns `OrderPaid` domain event
- [ ] `Order.MarkAsDelivered()` returns `OrderDelivered` domain event
- [ ] No `ApplicationUser` navigation property on `Order` or `OrderItem` — `string UserId` only
- [ ] No cross-BC navigation properties on `Order` — `int CustomerId`, `int CurrencyId`, `int? PaymentId`, `int? CouponUsedId`, `int? RefundId` only
- [ ] No `Item` navigation property on `OrderItem` — `int ItemId` only
- [ ] `Order.OrderItems` is `IReadOnlyList<OrderItem>` backed by `private readonly List<OrderItem> _orderItems`
- [ ] `OrderId(int)` and `OrderItemId(int)` are `sealed record` types extending `TypedId<int>`
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
- [ ] `Infrastructure/Sales/Orders/Extensions.cs` registers `AddDbContext<OrdersDbContext>`, `IDbContextMigrator`, `IOrderRepository`, `IOrderItemRepository`

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

### Tests
- [ ] `UnitTests/Sales/Orders/OrderAggregateTests.cs` covers: `Create`, `CalculateCost`, `MarkAsPaid`, `MarkAsDelivered`, `AssignCoupon`, `RemoveCoupon`, `AssignRefund`, `RemoveRefund`
- [ ] `UnitTests/Sales/Orders/OrderItemTests.cs` covers: `Create`, `UpdateQuantity`, `ApplyCoupon`, `RemoveCoupon`, `AssignRefund`, `RemoveRefund`
- [ ] All existing unit and integration tests still pass after new BC is registered in DI

## References

- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](0003-feature-folder-organization-for-new-bounded-context-code.md)
- [ADR-0004 — Module Taxonomy and Bounded Context Grouping](0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0011 — Inventory/Availability BC Design](0011-inventory-availability-bc-design.md) — `ReserveStockResult` pattern reference
- [ADR-0012 — Presale/Checkout BC Design](0012-presale-checkout-bc-design.md) — `CartLine`, `SoftReservation`, dependent on Orders switch
- [ADR-0013 — Per-BC DbContext Interfaces](0013-per-bc-dbcontext-interfaces.md) — DbContext accessibility rules
- [Bounded Context Map](../architecture/bounded-context-map.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) §4 (result-based error handling), §16 (domain model richness)
- [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
- [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
- [`.github/instructions/migration-policy.md`](../../.github/instructions/migration-policy.md)
- Related ADRs: ADR-0002, ADR-0003, ADR-0006, ADR-0011, ADR-0012, ADR-0013
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
