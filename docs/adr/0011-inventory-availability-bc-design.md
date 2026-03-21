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

    public bool IsGuaranteed => Status == ReservationStatus.Guaranteed; // pure predicate; guards PaymentWindowTimeoutJob

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

#### Phase 1 — Soft reservation (UX only, in-memory TTL)

Soft reservations live in the **Presale/Checkout BC** — see [ADR-0012](./0012-presale-checkout-bc-design.md).
`Inventory/Availability` has no knowledge of soft reservations. The only reservation concept
in this BC is the **hard reservation** (Guaranteed/Confirmed, DB-persisted).

The `ICheckoutSoftHoldService`, `CheckoutSoftHoldService`, and `SoftHold` DTO were removed from
this BC during the Presale/Checkout BC design phase. `IMemoryCache` is not a dependency of any
Inventory service.

#### Phase 2 — Guaranteed hold (DB, `RowVersion`, payment window)

At **Order Created** (`OrderPlaced` message received):

- Check `ProductSnapshot.CanBeReserved`, check `StockItem.AvailableQuantity >= qty`, then:
  create `Reservation(Guaranteed)`, call `StockItem.Reserve(qty)` (RowVersion bumped), schedule
  `PaymentWindowTimeout` deferred job at `OrderPlaced.ExpiresAt`.

`OrderPlaced.ExpiresAt` is the **single source of truth** for both the `Reservation.ExpiresAt`
field and the `PaymentWindowTimeout` job fire time. This eliminates the gap where a user could
pay after the reservation has expired.

```
StockService.ReserveAsync(ReserveStockDto dto):
  1. Load ProductSnapshot → guard CanBeReserved (throws BusinessException if not)
  2. If !IsDigital: load StockItem → Reserve(qty) → UpdateAsync (RowVersion checked by DB)
  3. AddAsync(Reservation.Create(productId, orderId, qty, dto.ExpiresAt))
  4. ScheduleAsync(PaymentWindowTimeoutJob.JobTaskName, dto.ProductId.ToString(), fireAt=dto.ExpiresAt)
```

#### Payment window timeout (idempotent)

`PaymentWindowTimeoutJob` implements `IScheduledTask`:
- `string TaskName => PaymentWindowTimeoutJob.JobTaskName` — the constant used in `ScheduleAsync`
- `EntityId` encodes `"{orderId}:{productId}:{quantity}"` (colon-delimited) because the timeout
  needs all three values and `JobExecutionContext.EntityId` is a single `string?`

```
PaymentWindowTimeoutJob.JobTaskName = "PaymentWindowTimeoutJob"  ← must match ScheduleAsync jobName

PaymentWindowTimeoutJob.ExecuteAsync(context, ct):
  1. If context.EntityId is null → context.ReportFailure("Missing EntityId"); return
  2. parts = context.EntityId.Split(':')
     If parts.Length != 3 or any parse fails → context.ReportFailure("Invalid EntityId format"); return
     orderId = parts[0], productId = parts[1], quantity = parts[2]
  3. Load Reservation by (orderId, productId)
  4. If not found OR Status != Guaranteed → context.ReportSuccess("No-op: not in Guaranteed state"); return
  5. StockItem.Release(quantity)
     reservation.MarkAsReleased()
     UpdateAsync(reservation)     ← soft-terminal; row preserved for audit
     context.ReportSuccess()
```

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
    Task<IReadOnlyList<StockItem>> GetAvailableAsync(
        int pageSize, int pageNo, string searchString, CancellationToken ct = default);
    Task<int> GetAvailableCountAsync(string searchString, CancellationToken ct = default);
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

### 8a. `Adjust` — deferred write with command coalescing and exponential backoff

`Adjust` is the only `StockItem` write driven by a human (admin). Two problems must be solved:

1. **Hot-path isolation** — inline admin writes compete with `Reserve`/`Release`/`Fulfill` for
   `RowVersion`, degrading order throughput. Solution: defer via `StockAdjustmentJob`.
2. **Command coalescing (setpoint update)** — admins frequently correct values in rapid succession
   (e.g., submit 4 → realise 10 → adjust to 9 for a broken item). Queuing N independent jobs
   risks applying a stale value or creating a burst of `RowVersion` contention. Solution:
   maintain exactly **one pending job per product**, always executing the **latest submitted value**.

This is a **last-write-wins queue** (control theory: *setpoint update*). N submissions always
produce one job execution against the current setpoint.

#### `PendingStockAdjustment` — the coalescing record

`PendingStockAdjustment` is a plain domain object (no domain methods needed): one row per
product, holding the latest admin-submitted target quantity and a `Version` GUID reset on every
upsert. It lives in `Domain/Inventory/Availability/PendingStockAdjustment.cs`.

```csharp
namespace ECommerceApp.Domain.Inventory.Availability;

public class PendingStockAdjustment
{
    public StockProductId ProductId { get; private set; }
    public StockQuantity NewQuantity { get; private set; }  // latest admin-submitted target (setpoint)
    public Guid Version { get; private set; }               // reset on each upsert
    public DateTime SubmittedAt { get; private set; }

    private PendingStockAdjustment() { }

    public static PendingStockAdjustment Create(StockProductId productId, StockQuantity newQuantity)
        => new PendingStockAdjustment
        {
            ProductId   = productId,
            NewQuantity = newQuantity,
            Version     = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow
        };
}
```

`IPendingStockAdjustmentRepository.UpsertAsync` is an INSERT-OR-UPDATE: it always overwrites the
existing row for that `ProductId` (including a fresh `Version` GUID). The upsert is the
coalescing write — whichever admin call arrives last wins.

#### `StockService.AdjustAsync` — upsert + cancel + reschedule

Because `IDeferredJobScheduler.ScheduleAsync` always inserts a new row (no deduplication on
`(jobName, entityId)`), `AdjustAsync` must explicitly cancel any existing pending job before
scheduling a fresh one. This keeps exactly one pending job per product at all times.

```
StockService.AdjustAsync(AdjustStockDto dto):
  1. UpsertAsync(dto.ProductId, dto.NewQuantity)                        ← setpoint update; always last-write-wins
  2. CancelAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString()) ← deletes Pending rows (no-op if Running)
  3. ScheduleAsync(StockAdjustmentJob.JobTaskName, dto.ProductId.ToString(), now) ← fresh job; reads upserted value
  4. return                                                              ← admin gets "adjustment queued" immediately
```

#### `StockAdjustmentJob.ExecuteAsync` — version-match delete for race safety

If the admin submits a new value while the job is already **Running** (Cancel in step 2 is a
no-op for Running rows), step 3 creates a new Pending job. The running job must not delete the
`PendingStockAdjustment` record in that case — a version-match delete handles this atomically.

`StockAdjustmentJob` implements `IScheduledTask`:
- `string TaskName => StockAdjustmentJob.JobTaskName` — the constant used in `ScheduleAsync` / `CancelAsync`
- `EntityId` encodes `productId.ToString()` — a single integer

**Two independent retry levels** (must not be confused):

| Level | Where | Default | Purpose |
|---|---|---|---|
| Infrastructure retry (`DeferredJobInstance.MaxRetries`) | Passed to `DeferredJobScheduler.ScheduleAsync`; controls DeadLetter escalation | **3** (framework default) | How many times the poller re-picks the job after an unhandled exception |
| Application-level concurrency retry | Loop inside `ExecuteAsync` on `DbUpdateConcurrencyException` | **5 attempts** | Handles `RowVersion` conflicts with exponential backoff — fully within a single job execution |

The application-level loop completes (or throws `BusinessException`) before the job execution
returns, so the infrastructure never sees a concurrency exception unless the loop exhausts all
5 attempts and rethrows.

```
StockAdjustmentJob.JobTaskName = "StockAdjustmentJob"  ← must match AdjustAsync ScheduleAsync/CancelAsync calls

StockAdjustmentJob.ExecuteAsync(context, ct):
  1. If context.EntityId is null →
       context.ReportFailure("Missing EntityId"); return
  2. If !int.TryParse(context.EntityId, out var productId) →
       context.ReportFailure($"Invalid EntityId: {context.EntityId}"); return
  3. pending = GetByProductIdAsync(productId)
  4. If pending is null →
       context.ReportSuccess("No pending adjustment — already handled"); return
  5. version = pending.Version    ← capture before any write
  6. const int maxAttempts = 5;   ← application-level concurrency retry; independent of DeferredJobInstance.MaxRetries
     for attempt in [0..maxAttempts):
       stock = GetByProductIdAsync(productId)
       stock.Adjust(pending.NewQuantity)              ← validates >= ReservedQuantity
       try:
         UpdateAsync(stock)                            ← RowVersion checked by DB
         break
       catch DbUpdateConcurrencyException when attempt < maxAttempts - 1:
         await Task.Delay(100ms * 2^attempt, ct)      ← 200ms, 400ms, 800ms…
     else: throw BusinessException("Adjustment failed after max retries.")
  7. DeleteIfVersionMatchesAsync(productId, version)
     → 0 rows deleted: admin submitted a new value while job was Running;
                        a fresh Pending job already exists to handle it — done
     → 1 row deleted:  latest setpoint cleanly applied — done
  8. context.ReportSuccess($"Stock adjusted to {pending.NewQuantity} for product {productId}")
```

**Properties of this design:**

| Property | Result |
|---|---|
| Admin submits N values rapidly | 1 job runs with the latest value — N−1 jobs cancelled before executing |
| Admin submits while job is Running | Running job applies its captured value; version-match delete leaves the record; fresh Pending job applies the new setpoint |
| Hot-path `Reserve`/`Release`/`Fulfill` | Never compete with `Adjust` — adjustment is background-only |
| Admin submits incorrect value, then corrects | Only the correction is ever applied — previous pending job was cancelled |
| Admin visibility | Job status queryable via `IJobStatusMonitor` |

The invariant (`newQuantity < ReservedQuantity → throw`) is evaluated on freshly loaded data at
job execution time. If reservations increase between admin submission and job execution, the
guard is evaluated against the current state — the correction may be rejected and the admin
notified via the job's `DeadLetter` status.

#### Execution flows

**Flow A — happy path** (N rapid submissions, all arrive before any job fires)

```
Admin         StockService       inventory.           timemanagement.      DeferredJob-       StockAdjustment-    inventory.
              .AdjustAsync       PendingStockAdj      DeferredJobQueue     PollerService      Job.ExecuteAsync    StockItems
  |               |                   |                    |                    |                   |                  |
  |─Submit 4─────►|                   |                    |                    |                   |                  |
  |               |─UPSERT(qty=4,     |                    |                    |                   |                  |
  |               |   ver=A)─────────►|                    |                    |                   |                  |
  |               |─CancelAsync──────────────────────────►(0 rows)             |                   |                  |
  |               |─ScheduleAsync────────────────────────►INSERT Job-1─────────────────────────────────────────────── |
  |◄─"queued"─────|                   |                    |                    |                   |                  |
  |               |                   |                    |                    |                   |                  |
  |─Submit 10────►|                   |                    |                    |                   |                  |
  |               |─UPSERT(qty=10,    |                    |                    |                   |                  |
  |               |   ver=B)─────────►| (A→B)              |                    |                   |                  |
  |               |─CancelAsync──────────────────────────►DELETE Job-1(Pending)|                   |                  |
  |               |─ScheduleAsync────────────────────────►INSERT Job-2─────────────────────────────────────────────── |
  |◄─"queued"─────|                   |                    |                    |                   |                  |
  |               |                   |                    |                    |                   |                  |
  |─Submit 9─────►|                   |                    |                    |                   |                  |
  |               |─UPSERT(qty=9,     |                    |                    |                   |                  |
  |               |   ver=C)─────────►| (B→C)              |                    |                   |                  |
  |               |─CancelAsync──────────────────────────►DELETE Job-2(Pending)|                   |                  |
  |               |─ScheduleAsync────────────────────────►INSERT Job-3─────────────────────────────────────────────── |
  |◄─"queued"─────|                   |                    |                    |                   |                  |
  |               |                   |                    |                    |                   |                  |
  |               |                   |                    |◄───Poll────────────|                   |                  |
  |               |                   |                    |Job-3 ready────────►|                   |                  |
  |               |                   |                    |Mark Running───────►|                   |                  |
  |               |                   |                    |                    |─ExecuteAsync──────►                  |
  |               |                   |◄────────────────────────────────────────GET(productId)─────|                  |
  |               |                   |─qty=9, ver=C────────────────────────────────────────────────►                 |
  |               |                   |                    |                    |                   |─GET StockItem──►|
  |               |                   |                    |                    |                   |  .Adjust(9)      |
  |               |                   |                    |                    |                   |─UPDATE(RowVer)──►|
  |               |                   |                    |                    |                   |◄─OK (Qty=9)───── |
  |               |                   |◄─────────────────────────────── DeleteIfVersionMatches(C)──|                  |
  |               |                   | 1 row deleted ✓    |                    |                   |                  |
  |               |                   |                    |Mark Complete──────►|                   |                  |

Result: Quantity = 9 ✓  Only Job-3 executed — Job-1 and Job-2 were cancelled before firing.
```

**Flow B — race case** (admin submits while job is already **Running**)

```
Admin         StockService       inventory.           timemanagement.      DeferredJob-       StockAdjustment-    inventory.
              .AdjustAsync       PendingStockAdj      DeferredJobQueue     PollerService      Job.ExecuteAsync    StockItems
  |               |                   |                    |                    |                   |                  |
  |               |                   |                    |Job-1 picked up────►|                   |                  |
  |               |                   |                    |Mark Running───────►|                   |                  |
  |               |                   |                    |                    |─ExecuteAsync──────►                  |
  |               |                   |◄────────────────────────────────────────GET(productId)─────|                  |
  |               |                   |─qty=4, ver=A────────────────────────────────────────────────► captures ver=A  |
  |               |                   |                    |                    |                   | (processing...)  |
  |               |                   |                    |                    |                   |                  |
  |─Submit 9─────►|                   |                    |                    |                   |                  |
  |               |─UPSERT(qty=9,     |                    |                    |                   |                  |
  |               |   ver=C)─────────►| (A→C)              |                    |                   |                  |
  |               |─CancelAsync──────────────────────────►Job-1 is Running     |                   |                  |
  |               |                   |                    |(not Pending)        |                   |                  |
  |               |                   |                    |0 rows deleted       |                   |                  |
  |               |─ScheduleAsync────────────────────────►INSERT Job-2─────────────────────────────────────────────── |
  |◄─"queued"─────|                   |                    |                    |                   |                  |
  |               |                   |                    |                    |                   |                  |
  |               |                   |                    |                    |                   |─GET StockItem──►|
  |               |                   |                    |                    |                   |  .Adjust(4)      |
  |               |                   |                    |                    |                   |─UPDATE(RowVer)──►|
  |               |                   |                    |                    |                   |◄─OK──────────── |
  |               |                   |◄─────────────────────────────── DeleteIfVersionMatches(A)──|                  |
  |               |                   | ver is now C ≠ A   |                    |                   |                  |
  |               |                   | 0 rows deleted     |                    |                   |                  |
  |               |                   | PendingAdj KEPT ✓  |                    |                   |                  |
  |               |                   |                    |Mark Complete──────►|                   |                  |
  |               |                   |                    |                    |                   |                  |
  |               |                   |                    |◄───Poll────────────|                   |                  |
  |               |                   |                    |Job-2 ready────────►|                   |                  |
  |               |                   |                    |Mark Running───────►|                   |                  |
  |               |                   |                    |                    |─ExecuteAsync──────►                  |
  |               |                   |◄────────────────────────────────────────GET(productId)─────|                  |
  |               |                   |─qty=9, ver=C────────────────────────────────────────────────►                 |
  |               |                   |                    |                    |                   |─GET StockItem──►|
  |               |                   |                    |                    |                   |  .Adjust(9)      |
  |               |                   |                    |                    |                   |─UPDATE(RowVer)──►|
  |               |                   |◄─────────────────────────────── DeleteIfVersionMatches(C)──|                  |
  |               |                   | 1 row deleted ✓    |                    |                   |                  |
  |               |                   |                    |Mark Complete──────►|                   |                  |

Result: Quantity = 9 ✓  Job-1 applied 4 transiently; Job-2 corrected to 9.
        Final value is always the last submitted setpoint regardless of timing.
```

The `Version` GUID on `PendingStockAdjustment` plays the same role as `RowVersion` on `StockItem`:
it is the handshake that makes the terminal delete race-safe without any additional locking.

### 9. Cross-BC integration via message broker (ADR-0010)

All cross-BC triggers use `IMessageBroker`. Dependency direction: Inventory subscribes to
Sales, Payments, and Catalog messages. The reverse (Sales → Inventory direct call) is
**forbidden**.

**Inbound (Inventory subscribes)**:

| Message | Publisher BC | Inventory Handler | Action |
|---|---|---|---|
| `OrderPlaced` | Sales/Orders | `OrderPlacedHandler` | Create Reservation + Reserve / schedule timeout |
| `OrderCancelled` | Sales/Orders | `OrderCancelledHandler` | Release + DELETE Reservation |
| `PaymentConfirmed` | Payments | `PaymentConfirmedHandler` | Reservation.Confirm() only |
| `OrderShipped` | Sales/Orders | `OrderShippedHandler` | Fulfill + DELETE Reservation |
| `RefundApproved` | Payments | `RefundApprovedHandler` | Return (no Reservation row exists) |
| `ProductPublished` | Catalog | `ProductPublishedHandler` | Upsert ProductSnapshot (Orderable) |
| `ProductUnpublished` | Catalog | `ProductUnpublishedHandler` | ProductSnapshot → Suspended |
| `ProductDiscontinued` | Catalog | `ProductDiscontinuedHandler` | ProductSnapshot → Discontinued |

**Outbound (Inventory publishes)**:

| Message | Published after | Subscribers | Payload |
|---|---|---|---|
| `AvailabilityChanged` | Every `Reserve`, `Release`, `Fulfill`, `Return`, `Adjust` | Presale/Checkout | `ProductId, AvailableQuantity, IsOutOfStock, OccurredAt` |

`AvailabilityChanged` carries `StockItem.AvailableQuantity` computed at the moment of the write.
It is the single integration message downstream consumers use to track availability. Internal
domain events (`StockReserved`, `StockReleased`, `StockFulfilled`, etc.) are Inventory-internal
audit events and are **never exposed** as integration messages.

Message contracts (`OrderPlaced`, `OrderCancelled`, `PaymentConfirmed`, `OrderShipped`,
`RefundApproved`) live in the **publisher's** `Messages/` folder. Each contract must implement
the `IMessage` marker interface (`ECommerceApp.Application.Messaging.IMessage`). Handlers live
in `Application/Inventory/Availability/Handlers/` and implement
`IMessageHandler<TMessage>` (`Task HandleAsync(TMessage message, CancellationToken ct)`).
Handler registration is performed via the standard `AddMessageHandlers()` DI extension in
`Application/Inventory/Availability/Services/Extensions.cs`.

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
    PaymentWindowTimeoutJob.cs            ← IScheduledTask, JobType.Deferred
    StockAdjustmentJob.cs                 ← IScheduledTask, JobType.Deferred, command coalescing + exponential backoff
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
- **Idempotent timeout handling** — `PaymentWindowTimeoutJob` checks status before acting;
  safe to fire even if payment was already confirmed.
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
- **Risk**: `PaymentWindowTimeoutJob` not registered → stock never released.
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

## Conformance checklist

- [ ] `StockItem` aggregate lives under `Domain/Inventory/Availability/`
- [ ] All `StockItem` properties use `private set`
- [ ] Static `Create(...)` factory method present, returns `(StockItem, StockAdjusted)`
- [ ] `StockItem` has a `private` parameterless constructor for EF Core
- [ ] `StockItem.Quantity` and `StockItem.ReservedQuantity` use `StockQuantity` VO — non-negative invariant enforced by constructor; stored as `int` via EF `HasConversion`
- [ ] `StockItem.ProductId` uses `StockProductId` typed ID (positive guard) — Inventory-local wrapper for the Catalog product PK
- [ ] `Reservation.ProductId` uses `StockProductId` typed ID
- [ ] `Reservation.OrderId` uses `ReservationOrderId` typed ID (positive guard) — Inventory-local wrapper for the Sales order PK
- [ ] `PendingStockAdjustment.ProductId` uses `StockProductId` typed ID
- [ ] `PendingStockAdjustment.NewQuantity` uses `StockQuantity` VO
- [ ] `StockItem.CanRelease(int qty)` and `StockItem.CanFulfill(int qty)` are pure predicates — no side effects; call before `Release`/`Fulfill` to guard intent
- [ ] `Reservation.IsGuaranteed` is a computed property (`Status == ReservationStatus.Guaranteed`) — pure predicate used by `PaymentWindowTimeoutJob`
- [ ] `StockQuantity`, `StockProductId`, and `ReservationOrderId` live under `Domain/Inventory/Availability/ValueObjects/`
- [ ] `Reserve`, `Release`, `Fulfill`, `Return`, `Adjust` are domain methods on `StockItem` — not in service
- [ ] `Adjust` throws `DomainException` if `newQuantity < ReservedQuantity`
- [ ] `StockService.AdjustAsync` queues `StockAdjustmentJob` via `IDeferredJobScheduler` — does NOT write inline
- [ ] `StockService.AdjustAsync` upserts `PendingStockAdjustment` before scheduling — last-write-wins setpoint
- [ ] `StockService.AdjustAsync` cancels any existing pending `StockAdjustmentJob` before scheduling a fresh one — at most one pending job per product
- [ ] `StockAdjustmentJob` declares `string TaskName => StockAdjustmentJob.JobTaskName` — constant used in `ScheduleAsync` / `CancelAsync`
- [ ] `PaymentWindowTimeoutJob` declares `string TaskName => PaymentWindowTimeoutJob.JobTaskName` — constant used in `ScheduleAsync`
- [ ] `PaymentWindowTimeoutJob.EntityId` encodes `"{orderId}:{productId}:{quantity}"` (colon-delimited)
- [ ] Both jobs guard `context.EntityId` for null and parse failure, calling `context.ReportFailure(...)` and returning early
- [ ] Both jobs call `context.ReportSuccess(...)` on the happy path — required for `IJobStatusMonitor` to show a non-null `Outcome`
- [ ] `StockAdjustmentJob` reads `PendingStockAdjustment` at execution time — no payload in the job record itself
- [ ] `StockAdjustmentJob` uses `DeleteIfVersionMatchesAsync` after successful write — race-safe against concurrent admin submissions
- [ ] `StockAdjustmentJob` application-level concurrency retry loop uses max **5 attempts** with `100ms * 2^attempt` backoff — this is independent of `DeferredJobInstance.MaxRetries` (infrastructure dead-letter threshold, default 3)
- [ ] `StockItemId` and `ReservationId` extend `TypedId<int>` (per ADR-0006) — declared as `sealed record StockItemId(int Value) : TypedId<int>(Value)`
- [ ] No cross-BC navigation properties — `ProductId` and `OrderId` are Inventory-local typed IDs (`StockProductId`, `ReservationOrderId`) wrapping the foreign BCs' PKs; no EF navigation properties between BCs
- [ ] `Reservation` is never loaded as a collection inside `StockItem`
- [ ] `ReservationStatus` has exactly two values: `Guaranteed`, `Confirmed`
- [ ] `Reservation` table rows are deleted (not updated to a terminal status) on timeout, cancellation, and fulfillment
- [ ] `ProductSnapshot.CanBeReserved` is checked in `StockService` before calling `StockItem.Reserve()`
- [ ] Digital products (`IsDigital = true`) skip `StockItem.Reserve()` — Reservation row created for tracing only
- [ ] `AvailabilityDbContext` uses schema `"inventory"`
- [ ] `StockItemConfiguration` maps `RowVersion` with `.IsRowVersion()` (not `IsConcurrencyToken()` alone)
- [ ] `StockService` implementation is `internal sealed`
- [ ] No `IMemoryCache` dependency in any Inventory service — soft reservations belong in Presale/Checkout BC (ADR-0012)
- [ ] `StockService` publishes `AvailabilityChanged` after every `Reserve`, `Release`, `Fulfill`, `Return`, and `Adjust` operation
- [ ] `OrderPlaced.ExpiresAt` is used as both `Reservation.ExpiresAt` and `PaymentWindowTimeout` job fire time
- [ ] `PaymentWindowTimeoutJob` is idempotent — checks `Reservation.Status` before acting
- [ ] No direct `IStockService` injection from `OrderService` — cross-BC via `IMessageBroker` only
- [ ] Message contracts implement `IMessage` marker interface (`ECommerceApp.Application.Messaging.IMessage`)
- [ ] Message contracts live in the publisher's `Messages/` folder
- [ ] Message handlers implement `IMessageHandler<TMessage>` (`Task HandleAsync(TMessage, CancellationToken)`)
- [ ] Message handlers (`OrderPlacedHandler`, etc.) live in `Application/Inventory/Availability/Handlers/`
- [ ] `AvailabilityChanged` message is defined in `Application/Inventory/Availability/Messages/` — properties: `ProductId`, `AvailableQuantity`, `IsOutOfStock`, `OccurredAt`; implements `IMessage`
- [ ] Domain events live under `Domain/Inventory/Availability/Events/`

## Implementation Status

| Layer | Status |
|---|---|
| Domain (`StockItem`, `Reservation`, `ProductSnapshot`, `PendingStockAdjustment`, typed IDs, repository interfaces, domain events) | ✅ Done |
| Infrastructure (`AvailabilityDbContext`, `inventory.*` schema, four configurations, four repositories, DI) | ✅ Done |
| Application (`IStockService`, `StockService`, message handlers, `PaymentWindowTimeoutJob`, `StockAdjustmentJob` with coalescing, DTOs, DI) | ✅ Done |
| Remove `ICheckoutSoftHoldService`, `CheckoutSoftHoldService`, `SoftHold` from Inventory codebase | ✅ Done — Presale/Checkout Slice 1 implemented (ADR-0012 Step 8) |
| `AvailabilityChanged` integration message + publishing in `StockService` | ✅ Done — `StockAvailabilityChangedHandler` in Presale/Checkout Slice 1 subscribed (ADR-0012) |
| Message contracts (`OrderPlaced`, `OrderCancelled`, `PaymentConfirmed`, `OrderShipped`, `RefundApproved`) | ✅ Done |
| Unit tests (`StockItem` aggregate, `StockService`, soft-hold service) | ✅ Done |
| DB migration (`InitInventorySchema` — four tables) | ✅ Done — runs automatically on startup via `RunMigrationsOnStart` |
| Data migration (`Items.Quantity` → `inventory.StockItems`, product data → `inventory.ProductSnapshots`) | ⬜ Not started |
| Integration tests | ⬜ Not started |
| Switch (replace `ItemHandler` calls with `IMessageBroker.PublishAsync`) | ⬜ Not started |
| Legacy cleanup (`Item.Quantity`, `ItemHandler` stock logic, availability queries from `ItemRepository`) | ⬜ Not started |
| **§ Amendment: Fulfillment message handlers (see below)** | ⬜ Agreed — not yet implemented |

## Design Amendment — Fulfillment Message Consumption (2025-06-27)

> **Status**: Agreed — not yet implemented. See ADR-0017 §13.3 for the full parallel fan-out decision.

### Rationale

Inventory currently learns about shipment outcomes indirectly via `OrderShipped` (published by
Orders BC after `ShipmentDelivered`). This has two problems:

1. **`ShipmentFailed` and `ShipmentPartiallyDelivered` don't trigger stock release** — Inventory
   never sees these events. Failed shipment items remain in `StockHold.Confirmed` status
   indefinitely, causing a **stock leak**.
2. **`OrderShipped` lacks line-level detail** — it carries `Items[]` but these are order items,
   not shipment lines. For partial delivery, Inventory cannot distinguish delivered vs failed items.

### Changes

**3 new handlers** (consume Fulfillment messages directly):

| Handler | Message | Action |
|---|---|---|
| `ShipmentDeliveredHandler` | `ShipmentDelivered` | For each item: find `StockHold` by orderId + productId → `StockItem.Fulfill()` |
| `ShipmentFailedHandler` | `ShipmentFailed` | For each item: find `StockHold` by orderId + productId → `StockItem.Release()` |
| `ShipmentPartiallyDeliveredHandler` | `ShipmentPartiallyDelivered` | Delivered items → `Fulfill()`, Failed items → `Release()` |

All three publish `StockAvailabilityChanged` for each affected product.

**`OrderShippedHandler` retirement**:
- `OrderShippedHandler` is **unregistered** from `Application/Inventory/Availability/Services/Extensions.cs`
- The handler class file is kept during the parallel-change window for rollback safety
- Deleted at the atomic switch once Fulfillment handlers are verified green

**DI registration** (in `Extensions.cs`):
```csharp
// Remove:
// services.AddTransient<IMessageHandler<OrderShipped>, OrderShippedHandler>();

// Add:
services.AddTransient<IMessageHandler<ShipmentDelivered>, ShipmentDeliveredHandler>();
services.AddTransient<IMessageHandler<ShipmentFailed>, ShipmentFailedHandler>();
services.AddTransient<IMessageHandler<ShipmentPartiallyDelivered>, ShipmentPartiallyDeliveredHandler>();
```

**Architecture test update**: `App_Inventory` test must add `FulfillmentMessages` to its allowed
dependency list in `BoundedContextDependencyTests.cs`.

**Idempotency**: `StockHold` status provides natural guard — `Fulfilled` and `Released` are
terminal states. Replaying a `ShipmentDelivered` for an already-fulfilled hold is a no-op
(`StockItem.Fulfill()` guards `hold.Status == Confirmed`).

## References

- Related ADRs:
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md) (§ 4 — Inventory concurrency model)
  - [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 — Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md) (`Inventory/Availability` greenfield)
  - [ADR-0006 — TypedId and Value Objects as Shared Domain Primitives](./0006-typedid-and-value-objects-as-shared-domain-primitives.md) (`StockItemId`, `ReservationId`)
  - [ADR-0007 — Catalog BC — Product, Category, and Tag Aggregate Design](./0007-catalog-bc-product-category-tag-aggregate-design.md) (Quantity removed from Product)
  - [ADR-0009 — Supporting TimeManagement BC Design](./0009-supporting-timemanagement-bc-design.md) (`PaymentWindowTimeoutJob`, `JobType.Deferred`)
  - [ADR-0010 — In-Memory Message Broker for Cross-BC Communication](./0010-in-memory-message-broker-for-cross-bc-communication.md) (cross-BC integration pattern)
  - [ADR-0012 — Presale/Checkout BC Design](./0012-presale-checkout-bc-design.md) (soft reservation, StorefrontProduct, `AvailabilityChanged` consumer)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
  - [`.github/instructions/migration-policy.md`](../../.github/instructions/migration-policy.md)
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
