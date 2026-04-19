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
- **Slice 2 (designed — §9)** — Rule-based coupon policy model. Replaces `DiscountPercent` +
  `CouponStatus` with `RulesJson` (serialized `CouponRuleDefinition[]`). Three rule categories:
  Scope (required ×1), Discount (required ×1), Constraint (optional ×N, zero = unrestricted).
  Multi-coupon per order (default 5, max 10), independent evaluation, optimistic concurrency,
  `OrderPriceAdjusted` boundary event, `PriceAdjustmentLedger` on Order, creation-time
  validation, and ML extensibility seam (future — not implemented in Slice 2).

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

### 9. Slice 2 — Rule-Based Coupon Policy Model

> **Status**: Designed. Replaces the preliminary §9.1–9.6 with a comprehensive rule-based
> architecture that subsumes CouponType, expiry, multi-use, per-item scoping, and extensibility
> through a single engine.

#### 9.1 Core Architecture

Slice 1 uses a fixed `DiscountPercent`. Slice 2 replaces this with a **rule-based coupon
policy model**:

1. An `ICouponRuleRegistry` singleton is built at startup from
   `CouponWorkflowBuilder.DefineRule<T>("name")` calls — a **flat, immutable vocabulary**.
2. Each coupon carries `RulesJson` — serialized `CouponRuleDefinition[]` selecting rules and
   supplying per-coupon parameters.
3. Three categories: **Scope** (required ×1), **Discount** (required ×1),
   **Constraint** (optional ×N — zero = unrestricted).
4. Each `DefineRule<T>()` registers both a runtime **evaluator** and a creation-time
   **parameter validator**. Bad coupons never reach the database.

#### 9.2 `Coupon` Aggregate Redesign

- `RulesJson` (`nvarchar(max)`) replaces `DiscountPercent` and `CouponStatus`.
- `Version` (`rowversion`) for optimistic concurrency.
- `Coupon.Create(code, description, rulesJson, scopeTargets)` validates:
  exactly one scope rule, exactly one discount rule, scope ↔ targets consistency
  (e.g., `per-product` scope requires non-empty targets; `order-total` requires empty targets).
  Violations throw `DomainException`.

#### 9.3 Rule Vocabulary (Initial)

**Scope:** `order-total`, `per-product`, `per-category`, `per-tag`.

**Discount:**

| Rule | Parameters | Notes |
|---|---|---|
| `percentage-off` | `{ "percent": "15" }` | |
| `fixed-amount-off` | `{ "amount": "50" }` | |
| `free-item` | `{ "productId": "123", "quantity": "1" }` | Targeted product |
| `gift-product` | `{ "productId": "456", "quantity": "1" }` | Requires stock check; out of stock → reject |
| `free-cheapest-item` | `{ "maxFreeUnits": "1" }` | Auto-selects from cart |

**Constraint** (zero = unrestricted):

| Rule | Parameters | Notes |
|---|---|---|
| `max-uses` | `{ "maxUses": "100" }` | Total usage count |
| `max-uses-per-user` | `{ "maxUsesPerUser": "1" }` | Per-user count |
| `valid-date-range` | `{ "validFrom": "...", "validTo": "..." }` | Time window |
| `min-order-value` | `{ "minValue": "100" }` | Default: 100 (configurable by admin per coupon) |
| `special-event` | `{ "eventCode": "BLACK_FRIDAY" }` | `ISpecialEventCache` lookup |
| `first-purchase-only` | `{}` | Zero completed orders |

All parameters use **defaults-when-missing** convention (`.TryGetValue()` with fallback).

#### 9.4 Supporting Entities

**`CouponUsed` (enhanced):**

- `CouponId` (`CouponId?`, nullable) — set for DB coupons. `RuntimeCouponSnapshot` (`string?`,
  JSON) — set for runtime/ML coupons (code, source, discountPercent, scope). Invariant: exactly
  one must be non-null.
- `UserId` (`string`, required) — added for `max-uses-per-user`.
- Unique constraints on `CouponId` and `OrderId` **removed** (multi-use, multi-coupon).
- Two factory methods: `CreateForDbCoupon(couponId, orderId, userId)` and
  `CreateForRuntimeCoupon(snapshotJson, orderId, userId)`.

**`CouponApplicationRecord` (new — audit):**

- Fields: `CouponUsedId` (plain `int` — no DB FK constraint; audit reference only; survives
  `CouponUsed` deletion), `CouponCode`, `DiscountType`, `DiscountValue`, `OriginalTotal`,
  `Reduction`, `AppliedAt`, `WasReversed` (bool), `ReversedAt` (DateTime?).
- Never deleted — only `WasReversed = true` on cancellation/refund.

**`SpecialEvent` (new):** `Code` (unique), `Name`, `StartsAt`, `EndsAt`, `IsActive`.
`ISpecialEventCache` — `IMemoryCache`, 5-min TTL, admin-invalidatable.

**`CouponScopeTarget` (new):** `CouponId` (FK), `ScopeType`, `TargetId`, `TargetName`
(display-only snapshot). No FK from `TargetId` to Catalog (cross-BC). Engine uses only `TargetId`.

#### 9.5 Validation Engine

**Two-tier runtime:** Tier 1 (sync, zero DB) → Tier 2 (async, DB/cache) — Tier 2 runs only
if Tier 1 passes. **Creation-time:** each rule's parameter validator called by
`CreateCouponAsync()` before persisting.

#### 9.6 Multi-Coupon Evaluation, Concurrency, and Discount Cap

**Independent evaluation:** each coupon against **original order total**. Reductions summed.
Deterministic (order-independent).

**Discount cap (Checkout BC enforces):** floor at `max(0, original - sum)`. New coupon
**rejected** if sum already ≥ originalTotal.

**Max coupons:** default 5 (`CouponsOptions.MaxCouponsPerOrder`), hard ceiling 10.
Per-coupon `IsExclusive` flag → if set, no more coupons can be added. Returned in
`CouponApplicationResult` for Checkout BC enforcement.

**Concurrency:** `Version` (`rowversion`) on `Coupon`. On `DbUpdateConcurrencyException` →
reload + re-evaluate + retry (max 2). First successful write wins.

#### 9.7 Orders Integration

**`OrderPriceAdjusted`** replaces `CouponApplied`:

```csharp
public record OrderPriceAdjusted(
    int OrderId, decimal NewPrice, decimal Delta,
    string AdjustmentType, int ReferenceId) : IMessage;
```

Orders BC receives thin price events — no coupon domain knowledge.

**`PriceAdjustmentLedger`:** replaces `CouponUsedId` + `DiscountPercent` on `Order` with a
`PriceAdjustment` collection. `ApplyPriceAdjustment()` replaces `AssignCoupon()`.
`RemovePriceAdjustment()` replaces `RemoveCoupon()`. `CalculateCost()` sums items minus
adjustments.

#### 9.8 Cancellation, Refund, and Operational Policies

**Token return:** `CouponsOrderCancelledHandler` finds all `CouponUsed` for the order (list),
iterates. For each: finds the matching `CouponApplicationRecord` by plain `CouponUsedId` int and
marks `WasReversed = true`, then deletes the `CouponUsed` record. Ordering invariant: mark before
delete — `CouponUsed` must still exist during the match step. User can re-use coupon if still valid.

**Catalog event subscription:** `ProductRenamed`, `CategoryRenamed`, `TagRenamed` messages
(Catalog BC publishes) → Coupons BC updates `CouponScopeTarget.TargetName`. Display-only sync.
**Gap:** current `Product` aggregate lacks rename event — prerequisite for Slice 2.
Product unpublish/discontinue → do nothing.

**Code collision:** DB coupons take priority. Runtime ML coupons: max 10% for ephemeral.
Higher → ML persists to DB via `CreateCouponAsync`. **Gift stock:** out of stock → reject
coupon entirely (stock check via `IStockClient`).

#### 9.9 ML/Runtime Extensibility (Future — Not Implemented in Slice 2)

The interface seam is defined; no runtime coupon source ships in Slice 2.

- `IRuntimeCouponSource` — `Task<RuntimeCoupon?> SuggestCouponAsync(userId, context, ct)`.
  `NullRuntimeCouponSource` registered as default (returns `null`).
- Two ML tiers (when implemented): **Ephemeral** (≤10%) → `RuntimeCouponSnapshot` JSON;
  **Persistent** (>10%) → ML creates DB coupon via `CreateCouponAsync`.
- `CouponSuggested` (`IMessage`) — published from future suggestion-display endpoint, not from
  `ApplyCouponAsync`. Interface defined; flow built when ML is implemented.

#### 9.10 Configuration

```csharp
public sealed class CouponsOptions
{
    public int MaxCouponsPerOrder { get; set; } = 5;        // hard ceiling: 10
    public decimal DefaultMinOrderValue { get; set; } = 100m;
}
```

#### 9.11 DB Schema Changes (Slice 2 Migration)

```
sales.Coupons        — RulesJson (nvarchar(max)), Version (rowversion);
                       DiscountPercent + Status removed
sales.CouponUsed     — CouponId nullable, + RuntimeCouponSnapshot (nvarchar(max)),
                       + UserId (nvarchar(450)); unique constraints removed
                       CHECK: exactly one of CouponId / RuntimeCouponSnapshot is NOT NULL

sales.CouponScopeTargets       — NEW (CouponId FK, ScopeType, TargetId, TargetName)
sales.CouponApplicationRecords — NEW (CouponUsedId int (no DB FK — plain audit ref), CouponCode, DiscountType, DiscountValue,
                                      OriginalTotal, Reduction, AppliedAt, WasReversed, ReversedAt)
sales.SpecialEvents            — NEW (Code UNIQUE, Name, StartsAt, EndsAt, IsActive)
```

**Data migration:** existing `sales.Coupons` → generate `RulesJson` from `DiscountPercent`
(`[{ scope: "order-total" }, { discount: "percentage-off", parameters: { percent: "<value>" } }]`).
Existing `sales.CouponUsed` → set `UserId` from legacy join or sentinel.

#### 9.12 Legacy Migration

The legacy `CouponType` entity (`Domain/Model/CouponType.cs`) is removed in the Slice 2 atomic
switch. Its `Type` string values are no longer needed — the rule-based model replaces `CouponType`
entirely through scope + discount rule combinations.

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
