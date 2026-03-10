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

A design review following the initial implementation identified additional aggregate refinements:
validated order numbers (`OrderNumber` VO), an append-only event audit log (`OrderEvent` separate
table), a customer data snapshot (`OrderCustomer` owned type) to preserve historical order accuracy,
compile-time-safe typed IDs (`OrderProductId`, `OrderUserId`), removal of the redundant
`OrderItem.RefundId` (refunds are whole-order operations), and clarification that
`OrderItem.UnitCost` stays as `decimal >= 0` (not `Price` VO) to allow free promotional items.
These refinements are incorporated directly into this ADR — no separate ADR is created.

## Decision

We will build the Sales/Orders bounded context as a parallel implementation per the
Parallel Change strategy (ADR-0002). No existing files are modified until the atomic switch.

### 1. Strongly-typed IDs

`OrderId(int)` and `OrderItemId(int)` — sealed records extending `TypedId<int>` (ADR-0006).
`int` IDs are used (not `Guid`) to maintain alignment with MSSQL identity columns and
existing FK relationships with Customer, Currency, and Payment tables.

`OrderProductId(int)` — replaces `int ItemId` on `OrderItem`. Struct with implicit operator
to/from `int`. Prevents inadvertent mixing with unrelated `int` values at compile time.
Pattern matches `PresaleProductId` (Presale/Checkout BC).

`OrderUserId(string)` — replaces `string UserId` on both `Order` and `OrderItem`. Struct with
implicit operator to/from `string`. Pattern matches `PresaleUserId` (Presale/Checkout BC).

`OrderEventId(int)` — typed ID for the `OrderEvent` audit entity; same struct pattern.

All four additional typed IDs live in `Domain/Sales/Orders/`.

### 2. Rich `Order` aggregate

```
Domain/Sales/Orders/Order.cs
```

Rules (per dotnet-instructions.md §16):

- All properties use `private set`.
- Private parameterless constructor for EF Core materialization.
- Static `Order.Create(int customerId, int currencyId, OrderUserId userId, OrderNumber number, OrderCustomer customer)` factory
  with invariant checks using `DomainException`.
- `OrderCustomer Customer { get; private set; }` — snapshot of customer personal and address
  data at placement time; resolved via `IOrderCustomerResolver` ACL before calling `Create`.
  Immutable after creation. See §11 for full field list.
- `int? DiscountPercent { get; private set; }` — captures the coupon discount (0–100) at
  assignment time. Stored as aggregate state; used internally by `CalculateCost()`.
- `CalculateCost()` — public, no parameters; converts `DiscountPercent` to a rate internally
  (`discountRate = 1 - DiscountPercent / 100m`). Eliminates the LoD violation.
  Enables cost simulation: create `Order` in memory, call `AssignCoupon`, read `Cost` without
  persisting — ideal for checkout price preview.
- State transitions return domain events and append to the event log:
  - `MarkAsPaid(int paymentId) → OrderPaid` domain event; appends `OrderEventTypes.OrderPaid`
  - `MarkAsDelivered() → OrderDelivered` domain event; appends `OrderEventTypes.OrderDelivered`
- Mutations coordinated across the collection:
  - `AssignCoupon(int couponUsedId, int discountPercent)` — validates `discountPercent` is 0–100
    (`DomainException` otherwise), stores `DiscountPercent`, sets `CouponUsedId` on order and all
    items, calls `CalculateCost()`, then appends `OrderEventTypes.CouponApplied`.
  - `RemoveCoupon()` — clears coupon state, recalculates at full rate, appends `OrderEventTypes.CouponRemoved`.
  - `AssignRefund(int refundId)` / `RemoveRefund()` — operates on `Order.RefundId` only;
    does **not** propagate to `OrderItem` (refunds are whole-order operations); appends
    `OrderEventTypes.RefundAssigned` / `OrderEventTypes.RefundRemoved`.
- `IReadOnlyList<OrderItem> OrderItems` backed by `private readonly List<OrderItem> _orderItems`.
- `OrderUserId UserId { get; private set; }` — typed ID; no `ApplicationUser` navigation.
- `OrderNumber Number { get; private set; }` — validated VO. See §12 for format details.
- `private readonly List<OrderEvent> _events` backing field; `IReadOnlyList<OrderEvent> Events`
  read-only property — append-only audit log. See §13 for structure and EF Core configuration.
- Every state transition calls internal `AppendEvent(string eventType, string? payload = null)`.
  `OrderEventTypes` static class defines all valid event type string constants.
- `int CustomerId`, `int CurrencyId`, `int? PaymentId`, `int? RefundId`, `int? CouponUsedId`
  — foreign-key IDs only; no cross-BC navigation properties.

### 3. `OrderItem` child entity

```
Domain/Sales/Orders/OrderItem.cs
```

- `UnitCost` (`decimal`, validated `>= 0`) — captures the item's price at cart-add time from
  the Presale/Checkout BC. Stays as plain `decimal` (not `Price` VO) to allow promotional free
  items (`UnitCost = 0`). The shared `Price` VO enforces `> 0` and is not weakened. See §14 for rationale.
- `OrderProductSnapshot? Snapshot { get; private set; }` — `null` during cart state;
  set at order-placement time via `SetSnapshot`. See §15 for full design.
- Static `OrderItem.Create(OrderProductId itemId, int quantity, decimal unitCost, OrderUserId userId)` factory.
  Snapshot is not a constructor parameter — it is resolved separately at placement time.
- `void SetSnapshot(OrderProductSnapshot snapshot)` — called by `OrderService.PlaceOrderAsync`
  for each cart item before persisting; throws `DomainException` if snapshot is null.
- State-mutation methods: `UpdateQuantity`, `ApplyCoupon`, `RemoveCoupon`.
- `AssignRefund` and `RemoveRefund` do **not** exist on `OrderItem` — refunds are whole-order
  operations owned by `Order.RefundId` and the event log.
- `OrderUserId UserId { get; private set; }` — typed ID; no `ApplicationUser` navigation.
- `OrderProductId ItemId { get; private set; }` — typed ID; no `Item` navigation property.

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
  - `AddScoped<IOrderProductResolver, OrderProductResolver>`

Called from `Application/DependencyInjection.cs` and `Infrastructure/DependencyInjection.cs`
respectively. Old registrations are NOT removed until the atomic switch.

### 10. Integration messages — reuse as-is

`OrderPlaced`, `OrderCancelled`, `OrderShipped` in `Application/Sales/Orders/Messages/`
remain unchanged. `OrderService.PlaceOrderAsync` publishes `OrderPlaced` via `IMessageBroker`
after persisting the order. `MarkAsDeliveredAsync` publishes `OrderShipped`.

### 11. `OrderCustomer` owned type snapshot

`OrderCustomer` is an EF Core owned type on `Order`, persisted as columns in `sales.Orders`.
It captures the customer's personal and address data **at the time of order placement** and is
immutable after creation — a historical record not linked to live `UserProfile` data.

**Fields:**
- `string FirstName`, `string LastName`
- `string Email`, `string PhoneNumber`
- `bool IsCompany`
- `string? CompanyName`, `string? Nip`
- `string Street`, `string BuildingNumber`, `string? FlatNumber`
- `string ZipCode`, `string City`, `string Country`

Fields use plain validated strings (not AccountProfile value objects) to preserve BC isolation.
Validation is enforced in the `OrderCustomer` constructor via `DomainException`.

**How it is populated:**
- `IOrderCustomerResolver` ACL interface lives in `Application/Sales/Orders/Contracts/`.
- `OrderCustomerResolver` in `Infrastructure/Sales/Orders/Adapters/` reads `UserProfileDbContext`.
- `OrderService.PlaceOrderAsync` calls `IOrderCustomerResolver.ResolveAsync(customerId)` and
  passes the result into `Order.Create(...)`.
- `Order.CustomerId` (`int?`) is retained as a cross-BC raw ID reference.

### 12. `OrderNumber` value object

`OrderNumber` is a `sealed record` in `Domain/Sales/Orders/ValueObjects/`.

**Format:** `ORD-{yyyyMMdd}-{8-char-hex}` — e.g. `ORD-20260310-A1B2C3D4`

- Date segment: UTC `yyyyMMdd` — no separators, safe in URLs and file names.
- Random segment: first 8 characters of `Guid.NewGuid()` formatted as `"N"`, uppercased —
  gives ~4 billion combinations per day.
- Max length: 20 chars; EF Core column type: `varchar(25)` (buffer).

`OrderNumber` exposes:
- Private constructor validated against `^ORD-\d{8}-[A-F0-9]{8}$`; throws `DomainException` on failure.
- `static OrderNumber.Generate()` factory using `DateTime.UtcNow` + `Guid.NewGuid()`.
- Implicit `string` operator for EF Core value conversion and interop.

`Order.Number` is mapped with `HasConversion<string>()` in EF Core configuration,
stored as `varchar(25)` with a `UNIQUE` constraint on `sales.Orders`.

### 13. `OrderEvent` audit log (separate table)

`OrderEvent` is an append-only child entity stored in `sales.OrderEvents`.

**Why a separate table (not a JSON column):**
- Rows are individually queryable (`WHERE EventType = 'OrderPaid'`).
- Rows are individually immutable — a JSON column must be rewritten on every append.
- No column size limit; schema evolution requires no migration of existing rows.
- Scalar fields on `Order` remain the source of truth — events are never replayed to derive state.

**Entity structure:**
- `OrderEventId(int)` typed ID.
- `OrderId OrderId` — typed FK (matches `Order.Id`); no navigation property back to `Order`.
- `string EventType` — value from `OrderEventTypes` static constants class (`OrderPlaced`,
  `OrderPaid`, `OrderDelivered`, `CouponApplied`, `CouponRemoved`, `RefundAssigned`, `RefundRemoved`).
  String constants, not an `enum` — adding new types requires no migration.
- `string? Payload` — nullable JSON string for supplementary data.
- `DateTime OccurredAt` — UTC; no public setter; set only in constructor.

EF Core configuration:
`HasMany(o => o.Events).WithOne().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade)`
with `.Navigation(o => o.Events).HasField("_events").UsePropertyAccessMode(PropertyAccessMode.Field)`.

### 14. `OrderItem.UnitCost` stays `decimal`

`OrderItem.UnitCost` remains a `decimal` field validated as `>= 0` in `OrderItem.Create(...)`.
It is **not** the shared `Price` VO.

The `Price` VO enforces `> 0` — semantically correct for catalog pricing. Promotional free
items require `UnitCost = 0`. Rather than weaken `Price`, this constraint is localised to the
order-line context where it is intentional.

> **Future consideration — Catalog free item pricing policy:**
> When free or promotional pricing is introduced in the Catalog BC, a dedicated ADR should
> evaluate the options: an `IsFree` flag on `Product`, a `PromotionalPrice` nullable property,
> a `FreeItemPolicy` strategy, or a scoped relaxation of `Price` to `>= 0` with an explicit
> justification invariant. This is out of scope for the current ADR.

### 15. `OrderProductSnapshot` — product display data at placement time

`OrderProductSnapshot` is an EF Core owned type on `OrderItem`, persisted in the separate
`sales.OrderItemSnapshots` table. It captures the product's display metadata — name and main
image — **at the time of order placement**, not at cart-add time. This ensures the order
record is an accurate historical snapshot regardless of future Catalog changes.

**Fields:**
- `string ProductName` — required; throws `DomainException` if null or whitespace.
- `string? ImageFileName` — optional; `null` if the product has no image at placement time.

**Why placement time (not cart-add time):**
- Cart items are ephemeral and mutable — the user may sit in the cart for hours or days.
- The "contract" forms at placement; display data should reflect the same business moment as price.
- Avoids unnecessary Catalog BC reads for cart items that are never ordered.
- Consistent with `OrderCustomer` snapshot, which is also resolved at placement time (§11).

**Price vs. display data separation:**
- `UnitCost` (`decimal`) is captured at **cart-add time** from the Presale/Checkout BC.
  This is intentional: the price the customer was shown during browsing is the committed price.
- `OrderProductSnapshot` (name, image) is captured at **order-placement time** from the
  Catalog BC. Display metadata does not affect pricing and can be resolved at placement.

**How it is populated — `SnapshotOrderItemsJob` (implemented):**

`OrderService.PlaceOrderAsync` does **not** resolve snapshots synchronously. Populating
snapshots inline would block order placement while waiting on N Catalog BC reads (one per
cart item), which degrades latency for large carts.

Instead, snapshots are populated asynchronously by `SnapshotOrderItemsJob`:

- `IOrderProductResolver` ACL interface lives in `Application/Sales/Orders/Contracts/`.
- `OrderProductResolver` in `Infrastructure/Sales/Orders/Adapters/` calls
  `IProductService.GetProductDetails(productId, ct)` to read product name and main image URL
  from the Catalog BC.
- `OrderPlacedSnapshotHandler` (`Application/Sales/Orders/Handlers/OrderPlacedSnapshotHandler.cs`)
  subscribes to `OrderPlaced` and resolves snapshots directly for the items of that specific order:
  1. Loads `OrderItem` rows for the order via `IOrderItemRepository.GetByOrderIdAsync(orderId)`.
  2. Calls `IOrderProductResolver.ResolveAsync(productId)` for each item's `ItemId`.
  3. Calls `IOrderItemRepository.SetSnapshotsAsync` with the resolved results.
  The message broker dispatches handlers asynchronously in the background, so placement latency
  is unaffected. No `IJobTrigger` dependency — `OrderService` is entirely decoupled from job infrastructure.
- `SnapshotOrderItemsJob` remains registered as an `IScheduledTask` sweeper (batch size: 64)
  to catch any items whose snapshot was not resolved by the handler (e.g. transient Catalog BC failure).
- `OrderItem.SetSnapshot(OrderProductSnapshot snapshot)` — domain method; throws
  `DomainException` if snapshot is null.
- `OrderItem.Snapshot` is `null` for cart items and transiently `null` for freshly placed
  order items until the handler runs. ViewModels must use null-safe access
  (`i.Snapshot?.ProductName`).

**EF Core configuration:**
```csharp
builder.OwnsOne(oi => oi.Snapshot, s =>
{
    s.ToTable("OrderItemSnapshots");
    s.WithOwner().HasForeignKey("OrderItemId");
    s.Property(p => p.ProductName).HasMaxLength(300).IsRequired();
    s.Property(p => p.ImageFileName).HasMaxLength(255);
});
```

Stored in `sales.OrderItemSnapshots` (separate table) rather than inline columns on
`sales.OrderItems` — mirrors the `sales.OrderCustomers` pattern (§11) and keeps
`sales.OrderItems` narrow.

## Consequences

### Positive

- `Order.CalculateCost()` (no parameters) eliminates the LoD violation — `DiscountPercent` is
  stored as aggregate state, so the aggregate always knows how to calculate its own cost.
  No cross-BC navigation chain at the domain level.
- Simulation support: any service can create an `Order` in memory, call `AssignCoupon`, and
  read `Cost` without persisting — enables checkout price preview without side effects.
- `OrderItem.UnitCost` captures price at cart-add time from the Presale/Checkout BC —
  removes dependency on `Item.Cost` navigation and decouples pricing from Catalog changes.
- `OrderProductSnapshot` preserves product display data (name, image) as it was at placement
  time — Catalog changes never corrupt historical order presentation.
- Separating price capture (cart-add, Presale BC) from display-data capture (placement,
  Catalog BC) aligns each BC's responsibility with the correct business moment.
- `PaymentHandler` coupling is broken: `Order.MarkAsPaid(paymentId)` is the only entry point
  for transitioning payment state; external mutation (`order.IsPaid = true`) becomes a
  compile error after the switch (private setter).
- Result objects provide explicit, type-safe error handling contracts without exception overhead
  on hot paths (e.g. "item out of stock" is checked on every add-to-cart).
- Consistent with `ReserveStockResult` pattern already in Inventory BC — no new patterns introduced.
- Fully async service layer reduces thread starvation risk under load.
- `OrdersDbContext` in `sales` schema isolates Orders persistence from legacy `dbo` tables.
- Two focused services reduce cognitive load vs. the current 25-method `OrderService`.
- `OrderNumber` VO ensures all order numbers are consistently formatted and validated at the
  domain boundary — human-readable, safe in URLs, and unambiguous.
- Every state transition is permanently recorded in `sales.OrderEvents` — enables customer
  support and audit workflows without application-level log scraping.
- `OrderCustomer` snapshot preserves the customer's data as it was at placement time —
  profile changes never corrupt historical order records.
- Typed IDs (`OrderProductId`, `OrderUserId`) eliminate a class of silent data-corruption
  bugs at compile time.
- `OrderItem` is simpler and more honest — refund lifecycle is owned entirely by `Order`.
- `Price` and `Money` shared VOs remain semantically strict (`> 0`), preserving their
  invariants across the codebase.

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
- `OrderCustomer` adds ~12 columns to `sales.Orders`; EF Core owned type projections must be
  included in queries that need customer data.
- `OrderEvent` requires a new table and `DbSet<OrderEvent>`; read queries needing the event
  log must join or use navigation.
- `IOrderCustomerResolver` introduces a synchronous cross-BC read from Sales/Orders →
  AccountProfile at order placement time.
- `IOrderProductResolver` introduces N synchronous Catalog BC reads (one per cart item) during
  `PlaceOrderAsync` — mitigated by `AsNoTracking()` queries but may impact placement latency
  for large carts.
- `OrderItem.Snapshot` is nullable in cart state — enforcement (non-null after placement) relies
  on `PlaceOrderAsync` calling `SetSnapshot`; any direct `OrderItem` persistence outside that
  flow must enforce this invariant separately.

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
- **Risk:** `OrderCustomerResolver` returns null if the user deletes their profile between
  cart checkout and order placement. **Mitigation:** resolver throws
  `BusinessException("CustomerNotFound")` which propagates through `ExceptionMiddleware`.
- **Risk:** `OrderNumber` collision (~1 in 4 billion per day). **Mitigation:** `UNIQUE`
  constraint on `sales.Orders.Number`; `PlaceOrderAsync` retries on constraint violation
  (max 3 attempts).
- **Risk:** `sales.OrderEvents` grows large for high-volume stores. **Mitigation:** index
  on `(OrderId, OccurredAt)`; archival is a separate operational concern.

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
- **JSON column for events (`Order.EventsJson`)** — rejected because rows in `sales.OrderEvents`
  are individually queryable, individually immutable, have no size limit, and allow schema
  evolution without rewriting existing data.
- **`Price` VO for `OrderItem.UnitCost`** — rejected because `Price` enforces `> 0`, conflicting
  with free promotional items. Keeping `UnitCost` as `decimal >= 0` localises the relaxed
  constraint to the order-line context.
- **`enum` for `EventType`** — rejected because adding a new event type requires a DB migration
  when using EF Core value converters. String constants require no migration.
- **Embed `OrderCustomer` fields directly on `Order`** — rejected; grouping as an owned type
  makes intent clear and allows future refactoring without changing the aggregate API.
- **Remove `Order.CustomerId` once `OrderCustomer` snapshot exists** — rejected; `CustomerId`
  is still needed for cross-BC queries ("show all orders for customer X").

## Migration plan

Parallel Change — existing code untouched until the atomic switch.

**Phase 1 — Domain layer**
1. `Domain/Sales/Orders/OrderItemId.cs` — strongly-typed ID
2. `Domain/Sales/Orders/OrderProductId.cs` — typed ID (`int`) for `OrderItem.ItemId`
3. `Domain/Sales/Orders/OrderUserId.cs` — typed ID (`string`) for `Order.UserId` and `OrderItem.UserId`
4. `Domain/Sales/Orders/OrderEventId.cs` — typed ID for `OrderEvent`
5. `Domain/Sales/Orders/ValueObjects/OrderNumber.cs` — VO with `Generate()` factory and regex validation
6. `Domain/Sales/Orders/OrderCustomer.cs` — owned value object with validated string fields + address
7. `Domain/Sales/Orders/OrderEventTypes.cs` — static string constants class
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

## Implementation Status

| Layer | Status |
|---|---|
| Domain — initial design (OrderId, OrderItemId, Order, OrderItem, events, repository interfaces) | ✅ Done |
| Application — initial design (DTOs, ViewModels, result types, service interface + impl, DI) | ✅ Done |
| Infrastructure — initial design (DbContext, schema, EF configs, repositories, DI) | ✅ Done |
| Unit tests — initial design | ✅ Done |
| Domain — refinements (OrderNumber, OrderCustomer, OrderEvent, OrderProductId, OrderUserId, OrderEventTypes, OrderEventId, OrderProductSnapshot) | ✅ Done |
| Application — refinements (IOrderCustomerResolver, IOrderProductResolver, updated OrderService + OrderItemService, updated ViewModels) | ✅ Done |
| Infrastructure — refinements (OrderCustomerResolver, OrderProductResolver, updated EF configs, OrderEventConfiguration, SetSnapshotsAsync) | ✅ Done |
| Unit tests — refinements (updated Order/OrderItem tests, SetSnapshot tests) | ✅ Done |
| Domain — FK type alignment (OrderItem.OrderId → OrderId?, OrderEvent.OrderId → OrderId) | ✅ Done |
| Application — SnapshotOrderItemsJob (sweeper) + OrderPlacedSnapshotHandler (event-driven, resolves snapshots for the placed order's items directly) | ✅ Done |
| Infrastructure — AssignToOrderAsync switched to change-tracking (ExecuteUpdateAsync incompatible with value converters) | ✅ Done |
| DB migration (`InitSalesSchema` at `Infrastructure/Sales/Orders/Migrations/`) | ✅ Done — pending production sign-off per migration policy |
| Integration tests | ⬜ Not started |
| Controller migration (Web + API atomic switch) | ⬜ Not started |
| Atomic switch | ⬜ After integration tests |

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
- [ ] `Order.MarkAsPaid(int paymentId)` returns `OrderPaid` domain event
- [ ] `Order.MarkAsDelivered()` returns `OrderDelivered` domain event
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
- [ ] `OrderEventTypes` is a `static class` with `string` constants (no `enum`)
- [ ] `Order` has `private readonly List<OrderEvent> _events` backing field
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

### Tests
- [ ] `UnitTests/Sales/Orders/OrderAggregateTests.cs` covers: `Create`, `CalculateCost`, `MarkAsPaid`, `MarkAsDelivered`, `AssignCoupon`, `RemoveCoupon`, `AssignRefund`, `RemoveRefund` and asserts an `OrderEvent` is appended for each transition
- [ ] `UnitTests/Sales/Orders/OrderItemTests.cs` covers: `Create`, `UpdateQuantity`, `ApplyCoupon`, `RemoveCoupon` (no refund tests)
- [ ] `UnitTests/Sales/Orders/ValueObjects/OrderNumberTests.cs` exists with validation and `Generate()` tests
- [ ] `UnitTests/Sales/Orders/OrderCustomerTests.cs` exists with construction validation tests
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
