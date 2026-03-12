# ADR-0016: Sales/Coupons BC — Slice 1: One-Time Coupon on Order

## Status
Accepted

## Date
2026-03-12

## Context

The legacy `CouponHandler` is the primary coupling hotspot between the Coupons and Orders bounded
contexts. It directly mutates `Order.CouponUsedId`, per-`OrderItem.CouponUsedId`, and calls
`order.CalculateCost()` from outside the aggregate:

```csharp
// Application/Services/Coupons/CouponHandler.cs — current state
public async Task HandleCouponChangesOnOrder(string couponCode, Order order)
{
    var coupon = await _couponRepository.GetCouponByCode(couponCode);
    if (coupon.CouponUsedId.HasValue) throw ...;    // null-check as "used" sentinel
    var couponUsed = new CouponUsed { ... };
    order.CouponUsedId = couponUsed.Id;             // direct mutation from outside aggregate
    order.CalculateCost();                          // LoD chain: CouponUsed?.Coupon?.Discount
}
```

The Orders BC (ADR-0014) has already broken this Law of Demeter chain. `Order.AssignCoupon(couponUsedId, discountPercent)`
and `Order.RemoveCoupon()` are implemented as proper aggregate methods, and `IOrderService.AddCouponAsync`
/ `RemoveCouponAsync` are the public coordination surface. The Orders BC no longer depends on Coupon
navigation properties.

The Coupons BC has not yet been migrated. The legacy `Coupon`, `CouponUsed`, and `CouponType` models
in `Domain/Model/` remain anemic with public setters. The legacy `CouponHandler` still reaches into
Orders directly. This ADR defines the new Coupons BC to eliminate that coupling.

**Scope constraint — Slice 1 only:**

The full Coupons domain includes coupon types (per-order vs per-item), expiry dates, multi-use limits,
and bulk issuance. Implementing all of these upfront would block the BC migration while the core sales
flow is being stabilised. Slice 1 implements exactly one scenario: a single-use (one-time) coupon
applied to a whole order. Slice 2 (all remaining features) is explicitly deferred until Slice 1 is in
production and integration-tested.

The legacy `CouponType` entity is **Slice 2 only** — it is not migrated in Slice 1.

## Decision

We introduce **Sales/Coupons** as a bounded context within the `Sales` group, with two slices:

- **Slice 1 (this ADR)** — `Coupon` aggregate with `CouponStatus`, `CouponUsed` entity,
  `ICouponService` (apply/remove), message-based Orders coordination (`CouponApplied` →
  `OrderCouponAppliedHandler`, `CouponRemovedFromOrder` → `OrderCouponRemovedHandler`),
  `CouponsOrderCancelledHandler` to release coupons when orders are cancelled. Can be
  implemented now.
- **Slice 2 (deferred)** — `CouponType` (per-order / per-item), expiry dates, multi-use limits,
  per-item discount application, bulk issuance. Will be designed in a future ADR amendment
  once Slice 1 is in production.

### 1. `Coupon` aggregate

`Coupon` is the aggregate root. It owns its lifecycle via `CouponStatus`. No navigation property
to `CouponUsed`; the 1:1 relationship is enforced by a unique constraint on `CouponUsed.CouponId`.

```csharp
// Domain/Sales/Coupons/
public class Coupon
{
    public CouponId Id { get; private set; }
    public string Code { get; private set; }
    public int DiscountPercent { get; private set; }
    public string Description { get; private set; }
    public CouponStatus Status { get; private set; }

    private Coupon() { }

    public static Coupon Create(string code, int discountPercent, string description)
        => new Coupon
        {
            Code = code,
            DiscountPercent = discountPercent,
            Description = description,
            Status = CouponStatus.Available
        };

    public void MarkAsUsed()
    {
        if (Status != CouponStatus.Available)
            throw new DomainException($"Coupon '{Code}' is not available.");
        Status = CouponStatus.Used;
    }

    public void Release()
    {
        if (Status != CouponStatus.Used)
            throw new DomainException($"Coupon '{Code}' is not in Used status.");
        Status = CouponStatus.Available;
    }
}

// Domain/Sales/Coupons/
public enum CouponStatus { Available, Used }
```

`CouponId` follows the `TypedId<int>` pattern (ADR-0006).

### 2. `CouponUsed` entity

`CouponUsed` records the fact that a `Coupon` was applied to a specific order. It is a plain
entity (not an aggregate root). `OrderId` is a plain `int` — no navigation property to `Order`.

```csharp
// Domain/Sales/Coupons/
public class CouponUsed
{
    public CouponUsedId Id { get; private set; }
    public CouponId CouponId { get; private set; }
    public int OrderId { get; private set; }    // plain int — no nav prop to Order
    public DateTime UsedAt { get; private set; }

    private CouponUsed() { }

    public static CouponUsed Create(CouponId couponId, int orderId)
        => new CouponUsed
        {
            CouponId = couponId,
            OrderId = orderId,
            UsedAt = DateTime.UtcNow
        };
}
```

`CouponUsedId` follows the `TypedId<int>` pattern (ADR-0006).

### 3. `ICouponService` — apply and remove

```csharp
// Application/Sales/Coupons/Services/
public interface ICouponService
{
    Task<CouponApplyResult> ApplyCouponAsync(string couponCode, int orderId, CancellationToken ct = default);
    Task<CouponRemoveResult> RemoveCouponAsync(int orderId, CancellationToken ct = default);
}
```

**`ApplyCouponAsync` flow:**

1. Verify order exists via `IOrderExistenceChecker.ExistsAsync(orderId, ct)`.
   If not found → return `CouponApplyResult.OrderNotFound`.
2. Find `Coupon` by `Code` from `ICouponRepository.GetByCodeAsync(couponCode, ct)`.
   If not found → return `CouponApplyResult.CouponNotFound`.
3. Check `Coupon.Status == CouponStatus.Available`.
   If not → return `CouponApplyResult.CouponAlreadyUsed`.
4. Check `ICouponUsedRepository.FindByOrderIdAsync(orderId, ct)`.
   If a `CouponUsed` already exists for this order → return `CouponApplyResult.OrderAlreadyHasCoupon`.
5. Call `coupon.MarkAsUsed()`.
6. Create `CouponUsed.Create(coupon.Id, orderId)` and persist via `ICouponUsedRepository.AddAsync`.
7. Persist coupon status change via `ICouponRepository.UpdateAsync(coupon)`.
8. Publish `CouponApplied(orderId, couponUsed.Id.Value, coupon.DiscountPercent)` via `IMessageBroker`.
9. Return `CouponApplyResult.Applied`.

**`RemoveCouponAsync` flow:**

1. Find `CouponUsed` by `orderId` via `ICouponUsedRepository.FindByOrderIdAsync(orderId, ct)`.
   If not found → return `CouponRemoveResult.NoCouponApplied`.
2. Load `Coupon` by `CouponId` from `ICouponRepository.GetByIdAsync`.
3. Call `coupon.Release()`.
4. Delete `CouponUsed` via `ICouponUsedRepository.DeleteAsync(couponUsed, ct)`.
5. Persist coupon status change via `ICouponRepository.UpdateAsync(coupon)`.
6. Publish `CouponRemovedFromOrder(orderId)` via `IMessageBroker`.
7. Return `CouponRemoveResult.Removed`.

`CouponService` is `internal sealed`.

### 4. Result types

```csharp
// Application/Sales/Coupons/Results/
public enum CouponApplyResult
{
    Applied,
    CouponNotFound,
    CouponAlreadyUsed,
    OrderAlreadyHasCoupon,
    OrderNotFound
}

public enum CouponRemoveResult
{
    Removed,
    NoCouponApplied
}
```

Expected business outcomes are returned as result values, not `BusinessException`. `DomainException`
from aggregate methods (e.g., `MarkAsUsed()` when status is not `Available`) is a true invariant
violation and propagates as `BusinessException` via `ExceptionMiddleware`.

### 5. Message-based Orders coordination

Coupons BC publishes; Orders BC reacts. This is the same one-way message pattern established by
Payments BC (ADR-0015).

**Integration messages (Coupons → Orders):**

```csharp
// Application/Sales/Coupons/Messages/
public record CouponApplied(int OrderId, int CouponUsedId, int DiscountPercent) : IMessage;
public record CouponRemovedFromOrder(int OrderId) : IMessage;
```

**Handlers in Orders BC:**

```csharp
// Application/Sales/Orders/Handlers/
internal sealed class OrderCouponAppliedHandler : IMessageHandler<CouponApplied>
{
    private readonly IOrderService _orders;

    public async Task HandleAsync(CouponApplied message, CancellationToken ct = default)
        => await _orders.AddCouponAsync(message.OrderId, message.CouponUsedId, message.DiscountPercent, ct);
}

internal sealed class OrderCouponRemovedHandler : IMessageHandler<CouponRemovedFromOrder>
{
    private readonly IOrderService _orders;

    public async Task HandleAsync(CouponRemovedFromOrder message, CancellationToken ct = default)
        => await _orders.RemoveCouponAsync(message.OrderId, ct);
}
```

Both handlers are registered in `Application/Sales/Orders/Services/Extensions.cs`.

**Order cancellation — `CouponsOrderCancelledHandler`:**

When an `OrderCancelled` message is published by the Orders BC, the Coupons BC must release the
coupon so it becomes available for future use.

```csharp
// Application/Sales/Coupons/Handlers/
internal sealed class CouponsOrderCancelledHandler : IMessageHandler<OrderCancelled>
{
    private readonly ICouponUsedRepository _couponUsed;
    private readonly ICouponRepository _coupons;

    public async Task HandleAsync(OrderCancelled message, CancellationToken ct = default)
    {
        var couponUsed = await _couponUsed.FindByOrderIdAsync(message.OrderId, ct);
        if (couponUsed is null)
            return;  // no coupon was applied to this order — no-op

        var coupon = await _coupons.GetByIdAsync(couponUsed.CouponId.Value, ct);
        coupon.Release();
        await _coupons.UpdateAsync(coupon, ct);
        await _couponUsed.DeleteAsync(couponUsed, ct);
    }
}
```

This handler does **not** publish `CouponRemovedFromOrder` — the order is already cancelled;
updating its `CouponUsedId` is immaterial.

### 6. `IOrderExistenceChecker` ACL

Coupons BC needs to verify an order exists before creating a `CouponUsed`. The ACL interface
decouples the Coupons domain from the Orders service type.

```csharp
// Application/Sales/Coupons/Contracts/
public interface IOrderExistenceChecker
{
    Task<bool> ExistsAsync(int orderId, CancellationToken ct = default);
}
```

Infrastructure adapter:

```csharp
// Infrastructure/Sales/Coupons/Adapters/
internal sealed class OrderExistenceCheckerAdapter : IOrderExistenceChecker
{
    private readonly IOrderService _orders;

    public async Task<bool> ExistsAsync(int orderId, CancellationToken ct)
        => await _orders.GetOrderDetailsAsync(orderId, ct) is not null;
}
```

### 7. DB schema (`sales.*`) and `CouponsDbContext`

```
sales.Coupons
  Id              int            PK IDENTITY
  Code            nvarchar(50)   NOT NULL  UNIQUE
  DiscountPercent int            NOT NULL  (1–100)
  Description     nvarchar(500)  NOT NULL
  Status          nvarchar(20)   NOT NULL  ('Available' / 'Used')

sales.CouponUsed
  Id        int       PK IDENTITY
  CouponId  int       NOT NULL  FK → sales.Coupons(Id)  UNIQUE
  OrderId   int       NOT NULL  UNIQUE
  UsedAt    datetime2 NOT NULL
```

`CouponUsed.CouponId` has a unique constraint — one `CouponUsed` record per `Coupon`.
`CouponUsed.OrderId` has a unique constraint — one coupon per order.
No FK from `CouponUsed.OrderId` to `Orders` (cross-BC boundary — IDs only).

```csharp
internal sealed class CouponsDbContext : DbContext
{
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<CouponUsed> CouponUsed { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sales");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CouponsDbContext).Assembly,
            t => t.Namespace?.Contains("Sales.Coupons") == true);
    }
}
```

### 8. Folder structure

```
ECommerceApp.Domain/Sales/Coupons/
  Coupon.cs
  CouponId.cs
  CouponUsed.cs
  CouponUsedId.cs
  CouponStatus.cs
  ICouponRepository.cs
  ICouponUsedRepository.cs

ECommerceApp.Application/Sales/Coupons/
  Contracts/
    IOrderExistenceChecker.cs
  Services/
    ICouponService.cs
    CouponService.cs              <- internal sealed
    Extensions.cs
  Results/
    CouponApplyResult.cs
    CouponRemoveResult.cs
  Messages/
    CouponApplied.cs
    CouponRemovedFromOrder.cs
  Handlers/
    CouponsOrderCancelledHandler.cs   <- IMessageHandler<OrderCancelled>
  DTOs/
    ApplyCouponDto.cs
  ViewModels/
    CouponVm.cs
    CouponListVm.cs

ECommerceApp.Application/Sales/Orders/Handlers/
  OrderCouponAppliedHandler.cs        <- IMessageHandler<CouponApplied>; calls IOrderService.AddCouponAsync
  OrderCouponRemovedHandler.cs        <- IMessageHandler<CouponRemovedFromOrder>; calls IOrderService.RemoveCouponAsync

ECommerceApp.Infrastructure/Sales/Coupons/
  CouponsDbContext.cs
  CouponsDbContextFactory.cs
  CouponsConstants.cs
  Repositories/
    CouponRepository.cs
    CouponUsedRepository.cs
  Configurations/
    CouponConfiguration.cs
    CouponUsedConfiguration.cs
  Adapters/
    OrderExistenceCheckerAdapter.cs   <- IOrderExistenceChecker → IOrderService
  Extensions.cs
  Migrations/
    (generated)
```

### 9. Slice 2 — deferred features

The following are explicitly out of scope for Slice 1. They will be addressed in a future ADR
amendment once Slice 1 is in production and integration-tested.

#### 9.1 `CouponType` — per-order vs per-item distinction

The legacy `CouponType.Type` string (`"for only one Order"` / `"for only one Item"`) becomes a
proper enum in Slice 2:

```csharp
// Domain/Sales/Coupons/
public enum CouponScope { Order, Item }
```

`Coupon` gains a `CouponScope Scope { get; private set; }` field. `Coupon.Create(...)` is extended
with a `CouponScope scope = CouponScope.Order` parameter. Existing Slice 1 tests remain valid:
`CouponScope.Order` is the default and covers the Slice 1 behaviour.

The `CouponApplied` message gains an optional `int? OrderItemId` field to carry the targeted item
when `Scope == Item`. `OrderCouponAppliedHandler` in the Orders BC calls a new
`IOrderService.AddItemCouponAsync(orderId, orderItemId, couponUsedId, discountPercent)` for
per-item application. `Order.AssignItemCoupon(orderItemId, couponUsedId, discountPercent)` is a new
aggregate method that applies the discount to one `OrderItem` only and recalculates `Cost`.

#### 9.2 Expiry dates

`Coupon` gains `DateTime? ValidFrom` and `DateTime? ValidTo`. A new `CouponStatus.Expired` value is
added. `CouponService.ApplyCouponAsync` checks both fields before step 5 (`coupon.MarkAsUsed()`):
- `ValidFrom > UtcNow` → return `CouponApplyResult.NotYetValid` (new enum value)
- `ValidTo < UtcNow` → return `CouponApplyResult.Expired` (new enum value)

A background scheduled task `ExpiredCouponsJob` (registered in TimeManagement) runs daily,
queries `sales.Coupons WHERE Status = 'Available' AND ValidTo < GETUTCDATE()`, and calls
`coupon.MarkAsExpired()` (new method — transitions `Available → Expired`). `Expired` coupons
cannot be `MarkAsUsed()` and cannot be `Release()`d.

#### 9.3 Multi-use limits

`Coupon` gains `int? UsageLimit` and `int UsageCount` (default `0`). The 1:1 unique constraint on
`CouponUsed.CouponId` is **relaxed** to allow N `CouponUsed` records per `Coupon` (when
`UsageLimit > 1`). The unique constraint on `CouponUsed.OrderId` **remains** (one coupon per order
regardless).

`Coupon.MarkAsUsed()` increments `UsageCount`. If `UsageLimit.HasValue && UsageCount >= UsageLimit`
the coupon status transitions to `Used` (exhausted). If `UsageLimit` is null the coupon may be used
indefinitely — `MarkAsUsed()` does not change `Status`, only increments the counter.

#### 9.4 Bulk issuance

A `CouponBatch` value object carries a template: `DiscountPercent`, `CouponScope`, `ValidFrom`,
`ValidTo`, `UsageLimit`. `ICouponService.IssueBulkAsync(CouponBatch batch, int count)` generates
`count` `Coupon` entities with unique codes (e.g., `BATCH-XXXX-YYYY` format), persists them, and
returns the list of generated codes.

#### 9.5 Admin CRUD

New service methods on `ICouponService`:
- `CreateCouponAsync(CreateCouponDto dto)` → `CouponCreateResult`
- `UpdateCouponAsync(UpdateCouponDto dto)` → `CouponOperationResult`
- `DeleteCouponAsync(int couponId)` → `CouponOperationResult` (only if `Status == Available` and
  `UsageCount == 0`)

These replace the legacy admin UI that currently calls `CouponService` (legacy `AbstractService`).
An admin controller migration is part of the Slice 2 atomic switch.

#### 9.6 Legacy `CouponType` migration

The legacy `CouponType` entity (`Domain/Model/CouponType.cs`) is removed as part of the Slice 2
switch. Existing `CouponType.Type` string values are mapped to the new `CouponScope` enum during the
data migration step (Slice 2 migration checklist). No new `CouponType` entity is created — the
`CouponScope` enum field on `Coupon` replaces it entirely.

## Consequences

### Positive
- **`CouponHandler` coupling eliminated** — Coupons BC no longer mutates `Order` directly; the
  Orders BC reacts to `CouponApplied` / `CouponRemovedFromOrder` messages via its own handlers.
- **Coupon lifecycle is explicit** — `CouponStatus.Available` / `CouponStatus.Used` replaces the
  legacy `CouponUsedId.HasValue` null-check pattern, making state transitions auditable.
- **Order cancellation handled correctly** — `CouponsOrderCancelledHandler` releases coupons
  automatically when orders are cancelled, preventing permanent coupon lockout.
- **ACL isolates BC dependency** — `IOrderExistenceChecker` documents the exact dependency on
  the Orders BC. Switching to an HTTP adapter for future microservice extraction requires only
  changing the Infrastructure adapter.
- **Slice 1 is focused and safe** — implementing only one-time order coupons limits blast radius
  and allows integration testing before expanding to complex coupon types.
- **Result-based error handling** — `CouponApplyResult` / `CouponRemoveResult` enums cover all
  expected business outcomes without exceptions.

### Negative
- `CouponUsed.OrderId` is a plain `int` with a unique constraint but no FK to `Orders`. Stale
  references can exist if an order is hard-deleted outside the cancellation flow.
- Applying a coupon is now two writes (Coupons BC) + one message handler invocation (Orders BC)
  instead of one synchronous `CouponHandler` call. The in-memory broker makes this sub-millisecond
  in the monolith, but it adds a conceptual step.

### Risks & mitigations
- **Risk**: `CouponsOrderCancelledHandler` fires after a manual `RemoveCouponAsync`, finding
  `CouponUsed` already deleted.
  **Mitigation**: Handler guards with a null check (`if couponUsed is null → return`), consistent
  with the no-op pattern established by `SoftReservationExpiredJob` and `PaymentWindowTimeoutJob`.
- **Risk**: Race condition — `ApplyCouponAsync` executes after `CouponsOrderCancelledHandler` on
  the same order.
  **Mitigation**: Acceptable for Slice 1 (monolith, in-memory broker, synchronous handlers). The
  `IOrderExistenceChecker` step gates against non-existent orders; a cancelled order still passes
  the existence check. Optimistic locking on `Coupon` can be added in Slice 2 if needed.
- **Risk**: `sales.Coupons` table name conflicts with legacy `dbo.Coupons`.
  **Mitigation**: `CouponsDbContext` uses `HasDefaultSchema("sales")`; legacy `Context` uses the
  default `dbo` schema. No name conflict.

## Alternatives considered

- **`CouponService` calls `IOrderService.AddCouponAsync` directly (no messaging)** — rejected.
  Direct synchronous cross-BC calls create tight coupling. If the call fails mid-way, the coupon
  is already marked `Used` with no order updated. The message pattern makes the eventual consistency
  explicit and the failure mode observable.
- **Keep coupon state in `Order.CouponCode` string (no `CouponUsed` entity)** — rejected. The
  `CouponUsed` entity is the audit record of coupon usage. Without it there is no way to enforce
  the one-time-use constraint or release the coupon on order cancellation.
- **Implement Slice 2 (CouponType etc.) immediately** — rejected per explicit user decision. The
  risk of parallel-building a complex Coupons BC while Orders and Payments BCs are not yet switched
  is too high. Slice 1 gives a stable, testable foundation.

## Migration plan

**Slice 1 (this ADR):**

1. Create target folder structure: `Domain/Sales/Coupons/`, `Application/Sales/Coupons/`,
   `Infrastructure/Sales/Coupons/`.
2. Create `Domain/Sales/Coupons/`: `Coupon`, `CouponId`, `CouponUsed`, `CouponUsedId`,
   `CouponStatus`, `ICouponRepository`, `ICouponUsedRepository`.
3. Create `Application/Sales/Coupons/`: `ICouponService`, `CouponService` (internal sealed),
   `CouponApplyResult`, `CouponRemoveResult`, `CouponApplied`, `CouponRemovedFromOrder`,
   `CouponsOrderCancelledHandler`, `IOrderExistenceChecker`, DI `Extensions.cs`.
4. Create `Application/Sales/Orders/Handlers/OrderCouponAppliedHandler` and
   `OrderCouponRemovedHandler`. Register both in `Application/Sales/Orders/Services/Extensions.cs`.
5. Create `Infrastructure/Sales/Coupons/`: `CouponsDbContext`, EF configurations
   (`CouponConfiguration`, `CouponUsedConfiguration`), `CouponRepository`,
   `CouponUsedRepository`, `OrderExistenceCheckerAdapter`, DI `Extensions.cs`.
6. Register `CouponsDbContext` in `Infrastructure/DependencyInjection.cs` alongside other BC
   contexts.
7. Generate EF migration `InitCouponsSchema` targeting `CouponsDbContext`.
8. Write unit tests: `CouponAggregateTests` (domain), `CouponServiceTests` (mocked repos + broker
   + existence checker), `CouponsOrderCancelledHandlerTests`, `OrderCouponAppliedHandlerTests`,
   `OrderCouponRemovedHandlerTests`.
9. Write integration tests: `CouponServiceIntegrationTests` — `ApplyCouponAsync` happy path,
   coupon not found, coupon already used, order not found; `RemoveCouponAsync` happy path,
   no coupon applied.
10. Atomic switch: update controllers / API controllers to use `ICouponService` instead of
    `ICouponHandler`. Remove legacy `CouponHandler` DI registration and all direct references
    to the legacy `CouponHandler` from controllers and services.

**Slice 2 (deferred — future ADR amendment or dedicated ADR):**

11. Add `CouponType` aggregate (per-order / per-item enum), add `ValidFrom`/`ValidTo` expiry
    fields and `CouponStatus.Expired`, add `UsageLimit` with remaining-uses tracking.
12. Add per-item coupon application: `ApplyCouponToItemAsync(couponCode, orderId, orderItemId)`.
    Extend `CouponApplied` message to carry per-item discount info.
13. Add bulk coupon issuance: `IssueBulkAsync(templateId, count)`.
14. Add admin CRUD for `Coupon` via new BC service methods and V2 controllers.
15. Migrate legacy `CouponType` data and remove the legacy `CouponType` model from `Domain/Model/`.

## Conformance checklist

- [ ] `Coupon` aggregate lives under `Domain/Sales/Coupons/` — not `Domain/Model/`
- [ ] `Coupon` has private setters and a `private` parameterless constructor for EF Core
- [ ] `Coupon.Create(code, discountPercent, description)` static factory method present
- [ ] `CouponId` and `CouponUsedId` follow the `TypedId<int>` pattern (ADR-0006)
- [ ] `CouponStatus` enum has exactly `Available` and `Used` in Slice 1
- [ ] `CouponUsed.OrderId` is a plain `int` — no navigation property to `Order`
- [ ] `CouponUsed.CouponId` has a unique constraint in `CouponUsedConfiguration`
- [ ] `CouponUsed.OrderId` has a unique constraint in `CouponUsedConfiguration`
- [ ] No FK constraint from `sales.CouponUsed.OrderId` to Orders table — cross-BC boundary
- [ ] `CouponService` is `internal sealed`
- [ ] `ICouponService` has exactly `ApplyCouponAsync` and `RemoveCouponAsync` in Slice 1
- [ ] `CouponApplyResult` and `CouponRemoveResult` are enums — expected outcomes are NOT `BusinessException`
- [ ] `CouponApplied` and `CouponRemovedFromOrder` live in `Application/Sales/Coupons/Messages/`
- [ ] `OrderCouponAppliedHandler` and `OrderCouponRemovedHandler` live in `Application/Sales/Orders/Handlers/`
- [ ] Both Order-side handlers are registered in `Application/Sales/Orders/Services/Extensions.cs`
- [ ] `CouponsOrderCancelledHandler` is `internal sealed`, lives in `Application/Sales/Coupons/Handlers/`
- [ ] `CouponsOrderCancelledHandler` is a no-op (returns immediately) when no `CouponUsed` found
- [ ] `CouponsOrderCancelledHandler` does NOT publish `CouponRemovedFromOrder` — cancelled order state is irrelevant
- [ ] `IOrderExistenceChecker` lives in `Application/Sales/Coupons/Contracts/`
- [ ] `OrderExistenceCheckerAdapter` lives in `Infrastructure/Sales/Coupons/Adapters/`
- [ ] `CouponsDbContext` uses schema `"sales"` — DbSets: `Coupons`, `CouponUsed`
- [ ] `CouponType` is NOT present anywhere in the Slice 1 domain or infrastructure
- [ ] Legacy `CouponHandler` is NOT removed until atomic switch (step 10) is verified green with all tests passing

## Implementation Status

| Step | Description | Status |
|------|-------------|--------|
| 1 | Folder structure created | ⬜ Not started |
| 2 | `Domain/Sales/Coupons/`: `Coupon`, `CouponId`, `CouponUsed`, `CouponUsedId`, `CouponStatus`, repository interfaces | ⬜ Not started |
| 3 | `Application/Sales/Coupons/`: `ICouponService`, `CouponService`, result types, messages, `CouponsOrderCancelledHandler`, `IOrderExistenceChecker`, DI | ⬜ Not started |
| 4 | `Application/Sales/Orders/Handlers/`: `OrderCouponAppliedHandler`, `OrderCouponRemovedHandler`; registered in Orders DI | ⬜ Not started |
| 5 | `Infrastructure/Sales/Coupons/`: `CouponsDbContext`, EF configs, repositories, `OrderExistenceCheckerAdapter`, DI | ⬜ Not started |
| 6 | `CouponsDbContext` registered in `Infrastructure/DependencyInjection.cs` | ⬜ Not started |
| 7 | EF migration `InitCouponsSchema` targeting `CouponsDbContext` | ⬜ Pending human approval (migration-policy.md) |
| 8 | Unit tests: `CouponAggregateTests`, `CouponServiceTests`, `CouponsOrderCancelledHandlerTests`, `OrderCouponAppliedHandlerTests`, `OrderCouponRemovedHandlerTests` | ⬜ Not started |
| 9 | Integration tests: `CouponServiceIntegrationTests` | ⬜ Not started |
| 10 | Atomic switch: controllers → `ICouponService`; remove legacy `CouponHandler` | ⬜ After integration tests |
| 11–15 | Slice 2 — deferred features (CouponType, expiry, per-item, bulk, admin CRUD) | ⬜ Future ADR |

## References

- Related ADRs:
  - [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 - Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 - Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md) (`Sales/Coupons` in `Sales` group)
  - [ADR-0006 - TypedId and Value Objects as Shared Domain Primitives](./0006-typedid-and-value-objects-as-shared-domain-primitives.md) (`CouponId`, `CouponUsedId`)
  - [ADR-0010 - In-Memory Message Broker](./0010-in-memory-message-broker-for-cross-bc-communication.md) (`CouponApplied` / `CouponRemovedFromOrder` cross-BC messages)
  - [ADR-0013 - Per-BC DbContext Interfaces](./0013-per-bc-dbcontext-interfaces.md) (`CouponsDbContext` registration pattern)
  - [ADR-0014 - Sales/Orders BC Design](./0014-sales-orders-bc-design.md) (`Order.AssignCoupon()` / `RemoveCoupon()` already implemented; `IOrderService.AddCouponAsync` / `RemoveCouponAsync` receiver surface)
  - [ADR-0015 - Sales/Payments BC Design](./0015-sales-payments-bc-design.md) (message-handler coordination pattern reference)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
