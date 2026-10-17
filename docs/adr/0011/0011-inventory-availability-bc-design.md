# ADR-0011: Inventory/Availability BC — StockItem Aggregate Design

## Status
Accepted

## Date
2026-03-03

## Context

ECommerceApp currently has **no dedicated Inventory bounded context**. Stock-level tracking is
scattered across the legacy Catalog domain and the Orders handler layer:

- **`Item.Quantity`** (`Domain/Model/Item.cs`, line 11) — a public-setter `int` property on
  the legacy Catalog aggregate. Any service can read or write it without invariant checks.
- **`ItemHandler`** (`Application/Services/Items/ItemHandler.cs`) — contains all stock
  decrement/increment logic. It directly mutates `item.Quantity` when an order is placed,
  updated, or cancelled. Despite living under `Services/Items/` (Catalog), it is called
  exclusively by `OrderService` (Sales/Orders BC) — a clear BC boundary violation.
- **`ItemRepository.GetAllAvailableItems()`** (`Infrastructure/Repositories/ItemRepository.cs`,
  lines 159–173) — filters items by `Quantity > 0`. This availability query is an Inventory
  concern embedded in the Catalog repository.
- **DTOs and ViewModels** (`AddItemDto.Quantity`, `UpdateItemDto.Quantity`, `ItemDto.Quantity`,
  `NewItemVm.Quantity`, `ItemDetailsVm.Quantity`, `ItemVm.Quantity`) — expose Quantity as a
  Catalog property, but it semantically belongs to Inventory.

The new Catalog BC (`Domain/Catalog/Products/Product.cs`) has already removed the `Quantity`
property — migration `RemoveQuantityFromProduct` drops the column from `catalog.Products`.
This confirms the strategic decision that **Quantity does not belong in Catalog**.

ADR-0002 § 4 explicitly calls for:
> "optimistic concurrency, reservation expiration, and idempotent reservation commands for
> inventory-sensitive operations."

ADR-0004 defines the canonical location as `Inventory/Availability` (greenfield).

The bounded context map (`docs/architecture/bounded-context-map.md`, line 24) lists
`Inventory/Availability` as `[greenfield]`.

The following forces apply:

- Stock-level management is a distinct domain concern — it should not leak into Catalog
  (product descriptions) or Orders (order lifecycle).
- The legacy `ItemHandler` tightly couples Orders ↔ Catalog through direct `Item.Quantity`
  mutation. This coupling must be broken.
- The new Catalog BC (`Product`) has no Quantity — if the Availability BC is not built, there
  is no home for stock tracking once the legacy `Item` is retired.
- Concurrent order placement can cause race conditions on stock levels — the current code has
  no concurrency protection (`ItemHandler` reads and writes `Item.Quantity` without row version
  or optimistic locking).
- Cross-BC communication should use `IMessageBroker` (ADR-0010), not direct service injection.
- Users should not be able to pay for stock that is no longer available — the reservation window
  must align with the payment window.

## Decision

We introduce the **Inventory/Availability** bounded context with a `StockItem` **counter
aggregate** and a separate `Reservation` entity. `StockItem` owns all stock-level counters and
invariants. `Reservation` tracks active holds independently — it is never loaded as a collection
inside `StockItem`. A `ProductSnapshot` ACL read model bridges the Catalog lifecycle into the
Inventory BC without creating a compile-time dependency.

### 1. Aggregate root: `StockItem` (counter pattern)

`StockItem` holds two `StockQuantity` counters: physical quantity on hand and the total reserved
quantity (sum of all active `Reservation.Quantity` rows for that product across all orders).
Both are backed by the `StockQuantity` value object (non-negative guard in constructor, `int` column
in DB via EF `HasConversion`). It never loads a `Reservation` collection — doing so would
over-lock and produce large aggregates under concurrent load.

`RowVersion` is mapped with `.IsRowVersion()` in `StockItemConfiguration`, which maps to SQL
Server's `rowversion` type (`binary(8)`). The database auto-increments it on every `UPDATE` —
the application never touches it. This is why `byte[]` is required: `rowversion` is 8-byte
binary, not an integer. Using `int` with `IsConcurrencyToken()` would work but would require
manual incrementing in every mutating method — omitting one call silently breaks concurrency
protection.

```csharp
namespace ECommerceApp.Domain.Inventory.Availability;

public class StockItem
{
    public StockItemId Id { get; private set; }
    public StockProductId ProductId { get; private set; }       // Inventory-local typed wrapper for Catalog product PK
    public StockQuantity Quantity { get; private set; }          // physical stock on-hand; guards value >= 0
    public StockQuantity ReservedQuantity { get; private set; }  // sum of all active Reservation.Quantity; guards value >= 0
    public byte[] RowVersion { get; private set; } = default!;  // auto-managed by DB via IsRowVersion()

    private StockItem() { }                                      // EF Core

    public static (StockItem, StockAdjusted) Create(StockProductId productId, StockQuantity initialQuantity)
    {
        var stock = new StockItem
        {
            ProductId = productId,
            Quantity = initialQuantity,
            ReservedQuantity = new StockQuantity(0)
        };
        return (stock, new StockAdjusted(stock.Id, productId.Value, 0, initialQuantity.Value, DateTime.UtcNow));
    }

    // Called at Order Created — increments ReservedQuantity counter
    public StockReserved Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Reserve quantity must be positive.");
        if (quantity > AvailableQuantity)
            throw new DomainException(
                $"Cannot reserve {quantity} — only {AvailableQuantity} available.");
        ReservedQuantity = new StockQuantity(ReservedQuantity.Value + quantity);
        return new StockReserved(Id, ProductId.Value, quantity, DateTime.UtcNow);
    }

    // Pure predicate — no side effects; used by handlers before calling Release
    public bool CanRelease(int qty) => qty > 0 && qty <= ReservedQuantity.Value;

    // Called at PaymentWindowTimeout (Guaranteed status) or OrderCancelled
    public StockReleased Release(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Release quantity must be positive.");
        if (quantity > ReservedQuantity.Value)
            throw new DomainException(
                $"Cannot release {quantity} — only {ReservedQuantity} reserved.");
        ReservedQuantity = new StockQuantity(ReservedQuantity.Value - quantity);
        return new StockReleased(Id, ProductId.Value, quantity, DateTime.UtcNow);
    }

    // Pure predicate — no side effects; used by handlers before calling Fulfill
    public bool CanFulfill(int qty) => qty > 0 && qty <= ReservedQuantity.Value;

    // Called at OrderShipped — actual stock deduction
    public StockFulfilled Fulfill(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Fulfill quantity must be positive.");
        if (quantity > ReservedQuantity.Value)
            throw new DomainException(
                $"Cannot fulfill {quantity} — only {ReservedQuantity} reserved.");
        ReservedQuantity = new StockQuantity(ReservedQuantity.Value - quantity);
        Quantity = new StockQuantity(Quantity.Value - quantity);
        return new StockFulfilled(Id, ProductId.Value, quantity, DateTime.UtcNow);
    }

    // Called at RefundApproved — stock returned to on-hand
    public StockReturned Return(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Return quantity must be positive.");
        Quantity = new StockQuantity(Quantity.Value + quantity);
        return new StockReturned(Id, ProductId.Value, quantity, DateTime.UtcNow);
    }

    // Admin stock correction — absolute set (REPLACE, not additive); StockQuantity constructor
    // guards newQuantity >= 0; this method only checks the reservation lower bound.
    public StockAdjusted Adjust(StockQuantity newQuantity)
    {
        if (newQuantity.Value < ReservedQuantity.Value)
            throw new DomainException(
                $"Cannot adjust to {newQuantity} — {ReservedQuantity} units currently reserved.");
        var previous = Quantity.Value;
        Quantity = newQuantity;
        return new StockAdjusted(Id, ProductId.Value, previous, newQuantity.Value, DateTime.UtcNow);
    }

    public int AvailableQuantity => Quantity.Value - ReservedQuantity.Value;
}
```

### 2. `Reservation` entity (reservation lifecycle table)

`Reservation` is a **separate entity**, never a collection inside `StockItem`. It is queried
independently when needed. The table holds **all** reservations — both active and terminal.
Terminal transitions update `Status` to `Released` or `Fulfilled` instead of deleting the row.
This preserves the full reservation history for the `Historia zmian` audit view in the
Inventory admin UI (ADR-0022).

One `Order` with multiple `OrderItem`s produces **one `Reservation` row per product** — a
single `OrderId` can appear in multiple rows.

```csharp
namespace ECommerceApp.Domain.Inventory.Availability;

public class Reservation
{
    public ReservationId Id { get; private set; }
    public StockProductId ProductId { get; private set; }       // Inventory-local typed wrapper for Catalog product PK
    public ReservationOrderId OrderId { get; private set; }     // Inventory-local typed wrapper for Sales order PK; one OrderId, many products
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime ReservedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }             // = OrderPlaced.ExpiresAt (payment window)

    private Reservation() { }

    public static Reservation Create(StockProductId productId, ReservationOrderId orderId, int quantity, DateTime expiresAt)
        => new Reservation
        {
            ProductId  = productId,
            OrderId    = orderId,
            Quantity   = quantity,
            Status     = ReservationStatus.Guaranteed,
            ReservedAt = DateTime.UtcNow,
            ExpiresAt  = expiresAt
        };

    public bool IsGuaranteed => Status == ReservationStatus.Guaranteed; // pure predicate; guards StockService.ReleaseAsync

    public void Confirm()         => Status = ReservationStatus.Confirmed;
    public void MarkAsReleased()  => Status = ReservationStatus.Released;
    public void MarkAsFulfilled() => Status = ReservationStatus.Fulfilled;
}

public enum ReservationStatus : byte
{
    Guaranteed = 0,  // active   — stock counter held, awaiting payment
    Confirmed  = 1,  // active   — payment confirmed, awaiting shipment
    Released   = 2,  // terminal — stock returned (payment timeout or order cancellation)
    Fulfilled  = 3,  // terminal — stock consumed (OrderShipped); mirrors OrderStatus.Fulfilled
}
```

**Terminal transitions** (Reservation row updated to terminal status, never deleted):

| Trigger | Counter change on `StockItem` | Reservation row |
|---|---|---|
| `PaymentWindowTimeout` (Guaranteed) | `Release(qty)` | `Status → Released` |
| `OrderCancelled` | `Release(qty)` | `Status → Released` |
| `OrderShipped` | `Fulfill(qty)` | `Status → Fulfilled` |
| `RefundApproved` | `Return(qty)` | — (already in terminal state after fulfillment) |
| `PaymentWindowTimeout` (Confirmed) | no-op | no-op |

Active rows: `Status IN (Guaranteed, Confirmed)` — queried by the Rezerwacje admin view.
Historical rows: `Status IN (Released, Fulfilled)` — queried by the Historia zmian audit view,
ordered `ReservedAt DESC`. `ExpiresAt` and `ReservedAt` provide temporal context without
additional columns.

### 3. `ProductSnapshot` read model (ACL from Catalog)

`ProductSnapshot` is an anti-corruption layer projection updated by Catalog lifecycle events.
It is the **sole guard** before `StockItem.Reserve()` is called: if `CanBeReserved` is false,
the reservation is rejected without touching the aggregate.

```csharp
namespace ECommerceApp.Domain.Inventory.Availability;

public class ProductSnapshot
{
    public int ProductId { get; private set; }
    public string ProductName { get; private set; }
    public bool IsDigital { get; private set; }
    public CatalogProductStatus CatalogStatus { get; private set; }

    public bool CanBeReserved => CatalogStatus == CatalogProductStatus.Orderable;

    private ProductSnapshot() { }
}

public enum CatalogProductStatus { Orderable, Suspended, Discontinued }
```

**Digital products**: `IsDigital = true` — `StockItem.Reserve()` is **skipped** entirely. A
`Reservation` row is still created for order tracing but the `StockItem` counter is not touched.

**Updated by**:
- `ProductPublished` → `CatalogStatus = Orderable`
- `ProductUnpublished` → `CatalogStatus = Suspended`
- `ProductDiscontinued` → `CatalogStatus = Discontinued`

### 4. Two-phase reservation

> Implementation detail extracted to [two-phase-reservation-flow.md](example-implementation/two-phase-reservation-flow.md).

Soft reservations live entirely in the Presale/Checkout BC ([ADR-0012](../0012/0012-presale-checkout-bc-design.md));
Inventory has no knowledge of them and never depends on `IMemoryCache`. The only reservation
concept in this BC is the hard `StockHold` (Guaranteed/Confirmed, DB-persisted), created when
`OrderPlaced` is received. Inventory does **not** schedule its own expiry job — the payment
window is owned end-to-end by Sales/Payments' `PaymentWindowExpiredJob` ([ADR-0015](../0015/0015-sales-payments-bc-design.md)),
which publishes `PaymentExpired` on expiry. Inventory's `PaymentExpiredHandler` reacts by
releasing every hold for that order through the same `StockService.ReleaseAsync` path used by
manual order cancellation, so both are idempotent by construction. See the companion for the
full flow and the exact release guard.

### 5. Domain events

```
Domain/Inventory/Availability/Events/
  StockReserved.cs   — Order Created: ReservedQuantity incremented
  StockReleased.cs   — Timeout/Cancelled: ReservedQuantity decremented
  StockFulfilled.cs  — OrderShipped: Quantity and ReservedQuantity decremented
  StockReturned.cs   — RefundApproved: Quantity incremented
  StockAdjusted.cs   — Admin adjustment: Quantity set to new absolute value
  StockDepleted.cs   — AvailableQuantity reached 0 (raised alongside StockReserved)
```

All events are `record` types containing `StockItemId`, `ProductId`, relevant quantity, and
`DateTime OccurredAt`.

### 6. Repository interfaces

```csharp
namespace ECommerceApp.Domain.Inventory.Availability;

public interface IStockItemRepository
{
    Task<StockItem?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task<StockItem> GetByIdAsync(StockItemId id, CancellationToken ct = default);
    Task AddAsync(StockItem stockItem, CancellationToken ct = default);
    Task UpdateAsync(StockItem stockItem, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetAllPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<int> GetAllCountAsync(CancellationToken ct = default);
}

public interface IReservationRepository
{
    Task<Reservation?> GetByOrderAndProductAsync(
        int orderId, int productId, CancellationToken ct = default);
    Task<IReadOnlyList<Reservation>> GetByOrderIdAsync(
        int orderId, CancellationToken ct = default);
    Task AddAsync(Reservation reservation, CancellationToken ct = default);
    Task UpdateAsync(Reservation reservation, CancellationToken ct = default);
    // DeleteAsync retained for compatibility; terminal transitions use UpdateAsync + MarkAsReleased/MarkAsFulfilled instead.
    Task DeleteAsync(Reservation reservation, CancellationToken ct = default);
}

public interface IProductSnapshotRepository
{
    Task<ProductSnapshot?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task UpsertAsync(ProductSnapshot snapshot, CancellationToken ct = default);
}

public interface IPendingStockAdjustmentRepository
{
    Task<PendingStockAdjustment?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task UpsertAsync(int productId, int newQuantity, CancellationToken ct = default);
    Task DeleteIfVersionMatchesAsync(int productId, Guid version, CancellationToken ct = default);
}
```

### 7.

```csharp
namespace ECommerceApp.Infrastructure.Inventory.Availability;

internal sealed class AvailabilityDbContext : DbContext
{
    public DbSet<StockItem> StockItems { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<ProductSnapshot> ProductSnapshots { get; set; }
    public DbSet<PendingStockAdjustment> PendingStockAdjustments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AvailabilityDbContext).Assembly,
            t => t.Namespace?.Contains("Inventory.Availability") == true);
    }
}
```

`StockItemConfiguration` maps `RowVersion` with `.IsRowVersion()` — SQL Server manages the
`rowversion` column automatically, no application code touches it.

**DB schema (`inventory.*`)**:

```
inventory.StockItems
  Id               int PK
  ProductId        int NOT NULL UNIQUE
  Quantity         int NOT NULL
  ReservedQuantity int NOT NULL DEFAULT 0
  RowVersion       rowversion NOT NULL        ← binary(8), auto-incremented by SQL Server

inventory.Reservations
  Id               int PK
  ProductId        int NOT NULL
  OrderId          int NOT NULL
  Quantity         int NOT NULL
  Status           tinyint NOT NULL           ← 0=Guaranteed, 1=Confirmed, 2=Released, 3=Fulfilled
  ReservedAt       datetime2 NOT NULL
  ExpiresAt        datetime2 NOT NULL         ← = OrderPlaced.ExpiresAt

inventory.ProductSnapshots
  ProductId        int PK
  ProductName      nvarchar(200) NOT NULL
  IsDigital        bit NOT NULL
  CatalogStatus    tinyint NOT NULL           ← 0=Orderable, 1=Suspended, 2=Discontinued

inventory.PendingStockAdjustments
  ProductId        int PK                     ← one row per product; upserted on every AdjustAsync
  NewQuantity      int NOT NULL               ← latest admin-submitted target (setpoint)
  Version          uniqueidentifier NOT NULL  ← reset on each upsert; job uses for race-safe delete
  SubmittedAt      datetime2 NOT NULL
```

### 8. Application service: `IStockService` / `StockService`

```csharp
namespace ECommerceApp.Application.Inventory.Availability.Services;

public interface IStockService
{
    Task<StockItemDto> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task InitializeStockAsync(int productId, int initialQuantity, CancellationToken ct = default);
    Task ReserveAsync(ReserveStockDto dto, CancellationToken ct = default);
    Task ReleaseAsync(int orderId, int productId, int quantity, CancellationToken ct = default);
    Task ConfirmAsync(int orderId, int productId, CancellationToken ct = default);
    Task FulfillAsync(int orderId, int productId, int quantity, CancellationToken ct = default);
    Task ReturnAsync(int productId, int quantity, CancellationToken ct = default);
    Task AdjustAsync(AdjustStockDto dto, CancellationToken ct = default);
}
```

`StockService` is `internal sealed`, registered in DI via
`Application/Inventory/Availability/Services/Extensions.cs`.

### 8a. `Adjust` - deferred write with command coalescing and exponential backoff

> Implementation detail extracted to [stock-adjustment-algorithm.md](example-implementation/stock-adjustment-algorithm.md).

`Adjust` is the only human-driven `StockItem` write. It is deferred through `StockAdjustmentJob`
to isolate order hot paths from admin corrections. The pending command is coalesced per product
using a last-write-wins setpoint. The job evaluates the current reservation guard at execution
time and uses version-match deletion to preserve a newer setpoint submitted while an earlier job
is running. The companion contains the retry levels, race behavior, conformance rules, and
example implementation.

### 9. Cross-BC integration via message broker (ADR-0010)

> Implementation detail extracted to [cross-bc-message-wiring.md](example-implementation/cross-bc-message-wiring.md).

All cross-BC triggers use `IMessageBroker`; Inventory subscribes to publisher-owned messages and
never exposes direct service calls to Sales, Payments, Fulfillment, or Catalog. The active
shipment path is `ShipmentDelivered`, `ShipmentFailed`, and `ShipmentPartiallyDelivered`, each
handled by its corresponding Fulfillment handler. The former `OrderShippedHandler` is retired and
must remain unregistered.

Inventory publishes `StockAvailabilityChanged` after stock mutations. Internal stock domain events
are not integration contracts. See the companion for the reconciled inbound table, registration
sketch, idempotency rules, and scope boundaries.

### 10. Data migration strategy

1. Generate EF migration `InitInventorySchema` on `AvailabilityDbContext` — creates
   `inventory.StockItems`, `inventory.Reservations`, `inventory.ProductSnapshots`.
2. Data migration: copy `Items.Id` → `StockItems.ProductId` and
   `Items.Quantity` → `StockItems.Quantity` (with `ReservedQuantity = 0`).
3. Verify row counts match; keep `Items.Quantity` column until switch is confirmed.
4. Populate `ProductSnapshots` from existing `Items`/`Products` data.
5. Only after full verification: remove `Quantity` from legacy `Item` model and `Items` table.

### 11. Folder structure

```
ECommerceApp.Domain/Inventory/Availability/
  StockItem.cs
  StockItemId.cs                    ← TypedId<int> (ADR-0006)
  Reservation.cs
  ReservationId.cs                  ← TypedId<int>
  ReservationStatus.cs
  ProductSnapshot.cs
  CatalogProductStatus.cs
  PendingStockAdjustment.cs         ← coalescing setpoint record; one row per product
  IStockItemRepository.cs
  IReservationRepository.cs
  IProductSnapshotRepository.cs
  IPendingStockAdjustmentRepository.cs
  ValueObjects/
    StockQuantity.cs                ← non-negative int quantity VO; guards value >= 0; stored as int via HasConversion
    StockProductId.cs               ← TypedId<int>; positive guard; Inventory-local wrapper for Catalog product PK
    ReservationOrderId.cs           ← TypedId<int>; positive guard; Inventory-local wrapper for Sales order PK
  Events/
    StockReserved.cs
    StockReleased.cs
    StockFulfilled.cs
    StockReturned.cs
    StockAdjusted.cs
    StockDepleted.cs

ECommerceApp.Application/Inventory/Availability/
  Services/
    IStockService.cs
    StockService.cs                       ← internal sealed; publishes AvailabilityChanged after every write
    Extensions.cs
  Handlers/
    OrderPlacedHandler.cs
    OrderCancelledHandler.cs
    PaymentConfirmedHandler.cs
    OrderShippedHandler.cs
    RefundApprovedHandler.cs
    ProductPublishedHandler.cs
    ProductUnpublishedHandler.cs
    ProductDiscontinuedHandler.cs
    StockAdjustmentJob.cs                 ← IScheduledTask, JobType.Deferred, command coalescing + exponential backoff
                                             (PaymentWindowTimeoutJob.cs retired 2026-08-17; superseded by PaymentExpiredHandler)
  DTOs/
    StockItemDto.cs
    ReservationDto.cs
    AdjustStockDto.cs
    ReserveStockDto.cs
  ViewModels/
    StockItemVm.cs

ECommerceApp.Infrastructure/Inventory/Availability/
  AvailabilityDbContext.cs
  AvailabilityDbContextFactory.cs
  AvailabilityConstants.cs
  Repositories/
    StockItemRepository.cs
    ReservationRepository.cs
    ProductSnapshotRepository.cs
    PendingStockAdjustmentRepository.cs  ← UpsertAsync + DeleteIfVersionMatchesAsync
  Configurations/
    StockItemConfiguration.cs       ← RowVersion → IsRowVersion()
    ReservationConfiguration.cs
    ProductSnapshotConfiguration.cs
    PendingStockAdjustmentConfiguration.cs
  Extensions.cs
  Migrations/
    (generated)
```

## Consequences

### Positive
- **Single owner for stock state** — `StockItem` aggregate encapsulates all stock invariants.
  No external code can mutate `Quantity` or `ReservedQuantity` directly.
- **Minimal lock surface** — `RowVersion` bumps only on `Reserve`, `Release`, `Fulfill`,
  `Return`, and `Adjust`. `PaymentConfirmed` touches only `Reservation.Status` — zero
  `StockItem` contention for the common payment path.
- **Command coalescing for admin adjustments** — `PendingStockAdjustment` upsert ensures N
  rapid admin submissions result in exactly one job execution with the latest setpoint. No stale
  intermediate values are ever applied; no `RowVersion` spike from back-to-back admin writes.
- **Optimistic concurrency** — `byte[]` `RowVersion` with `IsRowVersion()` is auto-managed
  by SQL Server. No application code can forget to increment it.
- **Payment gap eliminated** — `OrderPlaced.ExpiresAt` drives both `Reservation.ExpiresAt`
  and `PaymentWindowTimeout` job fire time. Users cannot pay after stock is released.
- **BC isolation** — Catalog owns product descriptions, Inventory owns stock levels.
- **No Catalog coupling** — `StockItem` and `Reservation` reference `ProductId` as plain
  `int`. `ProductSnapshot` is the only Catalog artifact in the Inventory schema.
- **Active-reservations-only table** — `Reservation` rows are deleted on terminal transitions;
  no accumulation of dead rows. Table stays small under normal operation.
- **Digital product support** — `IsDigital` flag on `ProductSnapshot` bypasses the counter
  for digital goods while still creating a `Reservation` for order tracing.
- **Idempotent timeout handling** — `StockService.ReleaseAsync` (reached via `PaymentExpiredHandler`)
  checks `IsGuaranteed` before releasing the counter; safe to fire even if payment was already
  confirmed. Inventory's own standalone timer (`PaymentWindowTimeoutJob`) was retired 2026-08-17
  as a redundant, always-racing duplicate of this event-driven path.
- **Domain events** — `StockReserved`, `StockReleased`, `StockFulfilled`, `StockReturned`,
  `StockAdjusted`, `StockDepleted` provide audit and extension points.

### Negative
- Four tables (`StockItems`, `Reservations`, `ProductSnapshots`, `PendingStockAdjustments`) and
  a separate `AvailabilityDbContext` add infrastructure complexity vs. the original single-table
  design. `PendingStockAdjustments` stays small: at most one row per product with a pending
  adjustment (row deleted after job completes).
- Soft reservations (UX-only TTL holds) live in Presale/Checkout BC — not here. This is by
  design: see ADR-0012. Inventory has no in-memory cache dependency.
- Asynchronous message-based integration is more complex to reason about than the legacy
  synchronous `ItemHandler` call. This is the intended direction per ADR-0010.

### Risks & mitigations
- **Risk**: Optimistic concurrency retries degrade throughput under high contention.
  **Mitigation**: Current traffic does not justify concern. If contention grows, introduce a
  per-product serialized queue.
- **Risk**: `PaymentExpiredHandler` not registered → stock never released on payment expiry.
  **Mitigation**: `BackgroundMessageDispatcher` logs handler resolution failures at `Critical`.
  Integration tests verify handler registration.
- **Risk**: Data migration loses or corrupts stock levels.
  **Mitigation**: Run in transaction; verify row counts; keep `Items.Quantity` until confirmed.
- **Risk**: `StockDepleted` event not consumed → out-of-stock products remain listed.
  **Mitigation**: Phase 1 uses synchronous availability queries. Event-driven listing removal
  is a future enhancement.
- **Risk**: Two orders pass the `AvailableQuantity` check concurrently before either commits.
  **Mitigation**: `RowVersion` on `StockItem` makes one of the two `Reserve()` calls fail with
  a concurrency exception. The losing request is retried or surfaced as `BusinessException`.
- **Risk**: Multi-instance soft holds are not shared across nodes.
  **Mitigation**: Deferred to `IDistributedCache` upgrade. Soft holds are UX-only; the DB
  `RowVersion` check is the real contention gate.

## Alternatives considered

- **`StockQuantity` value object as aggregate field type** — adopted. Initially considered and
  rejected in favour of plain `int` counters. Subsequently adopted for consistency with the
  ADR-0006 VO pattern established in AccountProfile and Catalog BCs. The `StockQuantity`
  constructor centralises the non-negative guard, removes duplicate `< 0` checks from every
  domain method, and makes the counter semantics explicit at the property level. `StockProductId`
  and `ReservationOrderId` typed IDs were added for the same reason — guarded positive-integer
  wrappers that prevent cross-BC primitive confusion. All three live under
  `Domain/Inventory/Availability/ValueObjects/`.
- **`Reservation` collection inside `StockItem`** — rejected; loading all reservations per
  product on every stock operation produces large aggregates and over-locks under concurrency.
  Counter pattern achieves the same invariants with a single-row lock.
- **`ReservationType` (Soft/Hard) enum on `Reservation`** — rejected; `ReservationStatus`
  encodes the same information. An extra type field would be redundant.
- **Soft holds in Inventory** — relocated; soft reservations (UX-only TTL holds) were initially
  designed as `ICheckoutSoftHoldService` in this BC and subsequently moved to Presale/Checkout BC
  (ADR-0012). Inventory is not the correct owner — soft reservations are a customer-intent
  concept, not a stock-commitment concept. Inventory owns hard reservations only.
- **Keep `Quantity` on `Item` / `Product`** — rejected; Catalog and Inventory have different
  lifecycles. Coupling them triggers unnecessary cache invalidation and EF change tracking.
- **Shared `Context` instead of `AvailabilityDbContext`** — rejected; per ADR-0003/0007
  pattern, new BCs get their own `DbContext` to enforce persistence-level isolation.
- **Synchronous `IStockService` injection from `OrderService`** — rejected; creates
  compile-time coupling between Sales and Inventory. ADR-0010 message broker is prescribed.
- **Inline `Adjust` with optimistic retry** — rejected; admin input can cause repeated RowVersion
  spikes that compete directly with hot-path `Reserve`/`Release`/`Fulfill` operations. A single
  admin with a typo can degrade order throughput. Deferred `StockAdjustmentJob` with exponential
  backoff removes admin writes from the hot-path contention surface entirely.
- **Multiple independent adjustment jobs (naive queue)** — rejected; each admin submission would
  queue an independent job carrying its own target value. A rapid sequence (4 → 10 → 9) could
  apply values out of order or apply a stale intermediate value if jobs race. The upsert-based
  coalescing pattern (cancel + reschedule + version-match delete) ensures only the latest
  setpoint is ever applied, regardless of submission rate.
- **Saga orchestrator for reservation flow** — deferred; current volume does not justify
  the complexity. ADR-0002 § 2 notes it as a future evolution.

## References

- Related ADRs:
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../0002/0002-post-event-storming-architectural-evolution-strategy.md) (§ 4 — Inventory concurrency model)
  - [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](../0003/0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 — Module Taxonomy and Bounded Context Grouping](../0004/0004-module-taxonomy-and-bounded-context-grouping.md) (`Inventory/Availability` greenfield)
  - [ADR-0006 — TypedId and Value Objects as Shared Domain Primitives](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md) (`StockItemId`, `StockHoldId`)
  - [ADR-0007 — Catalog BC — Product, Category, and Tag Aggregate Design](../0007/0007-catalog-bc-product-category-tag-aggregate-design.md) (Quantity removed from Product)
  - [ADR-0009 — Supporting TimeManagement BC Design](../0009/0009-supporting-timemanagement-bc-design.md) (deferred job scheduling)
  - [ADR-0010 — In-Memory Message Broker for Cross-BC Communication](../0010/0010-in-memory-message-broker-for-cross-bc-communication.md) (cross-BC integration pattern)
  - [ADR-0012 — Presale/Checkout BC Design](../0012/0012-presale-checkout-bc-design.md) (soft reservation, `StockAvailabilityChanged` consumer)
- Instruction files:
  - [`.github/instructions/dotnet.instructions.md`](../../../.github/instructions/dotnet.instructions.md)
  - [`.github/instructions/efcore.instructions.md`](../../../.github/instructions/efcore.instructions.md)
  - [`.github/instructions/testing.instructions.md`](../../../.github/instructions/testing.instructions.md)
  - [`.github/instructions/migration-policy.instructions.md`](../../../.github/instructions/migration-policy.instructions.md)
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
