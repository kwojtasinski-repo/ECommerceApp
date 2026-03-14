# ADR-0012: Presale/Checkout BC - CartLine, SoftReservation, StockSnapshot, and ACL-Based Pre-Sale Design

## Status
Accepted

## Date
2026-03-06 (Slice 1)
2026-03-12 (Slice 2 design)
2026-03-14 (Slice 3: initiation endpoint, SoftReservationStatus, TOCTOU fix)

## Context

After the Inventory/Availability BC (ADR-0011) and Catalog BC (ADR-0007) were built, three
problems were identified that this ADR resolves:

**Problem 1 - Unified storefront endpoint for SPA/API clients.**
To display a product listing with availability, a client needs:
- Catalog data (name, price, image, category, tags) - from `Catalog/Products`
- Availability data (available quantity, out-of-stock) - from `Inventory/Availability`

An initial approach of a `StorefrontProduct` denormalized read model maintained via event
handlers was considered but rejected (see Alternatives). A BFF-style controller in the API
project that synchronously composes both BCs is simpler, avoids event-driven data duplication,
and requires no separate schema or projection maintenance.

**Problem 2 - Soft reservation in the wrong BC.**
`ICheckoutSoftHoldService` and `CheckoutSoftHoldService` were placed in `Inventory/Availability`
as a temporary measure. The name "Checkout" already signals the mismatch. Inventory's job is
physical stock with hard (DB-persisted) reservations. Soft reservations are a customer-intent
concept - they belong to the pre-sale flow, not the inventory commitment flow.

**Problem 3 - Price snapshot at checkout commitment.**
When a customer clicks "Checkout", the unit price must be captured and locked at that exact
moment. This price belongs to the `SoftReservation` (the pre-sale commitment), not to
`CartLine` (browsing intent). `CartLine` intentionally carries no `UnitPrice` field.

## Decision

We introduce **Presale/Checkout** as a bounded context with two slices:

- **Slice 1 (this ADR)** - `CartLine` write-through cache, `SoftReservation` (DB + cache),
  `StockSnapshot` (event-driven read model), `SoftReservationExpiredJob`, ACL interfaces.
  No dependency on Sales/Orders BC. Can be implemented now.
- **Slice 2 (future ADR)** - Checkout write flow: cart-to-order transition.
  Blocked by Sales/Orders BC.

### 1. `StorefrontController` - BFF endpoint in `ECommerceApp.API` (not a BC)

The unified product listing endpoint is a **Backend for Frontend** concern, not a Presale
domain concept. A controller in `ECommerceApp.API` composes `IProductService` +
`IStockService` synchronously and returns a merged view model to the caller. No event handlers,
no projection table, no propagation lag.

```csharp
// ECommerceApp.API/Controllers/Presale/
[ApiController]
[Route("api/storefront")]
public sealed class StorefrontController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IStockService _stock;

    // GET api/storefront/products - merges Catalog + Inventory for SPA/mobile clients
}
```

This same endpoint serves SPA, mobile, and service-to-service HTTP consumers. For in-process
cross-BC calls within the monolith, the ACL interfaces (section 3) are used directly without HTTP.

### 2. `CartLine` - write-through cache + flat DB backup

Cart is **not** a domain aggregate. A cart is a collection of flat `CartLine` rows - one row
per `(UserId, ProductId)` pair. There is no `CartId`, no `CartItem` entity, and no `UnitPrice`
in the cart. Price is captured later, at checkout initiation, when `SoftReservation` is created.

```csharp
// Domain/Presale/Checkout/
public class CartLine
{
    public string UserId { get; private set; }
    public int ProductId { get; private set; }
    public int Quantity { get; private set; }

    private CartLine() { }

    public static CartLine Create(string userId, int productId, int quantity)
        => new CartLine { UserId = userId, ProductId = productId, Quantity = quantity };

    public void UpdateQuantity(int quantity) => Quantity = quantity;
}
```

**Storage strategy - write-through cache:**
- `CartService` writes to `presale.CartLines` DB **first** (synchronous EF Core upsert).
- After a successful DB write, `IMemoryCache` is updated.
- On read: cache hit → return immediately; cache miss → load from DB, populate cache, return.
- On remove/clear: delete from DB, then evict from cache.

This ensures cart state survives process restarts with no risk of cache-only data loss. The DB
is the source of truth; the cache is a read-speed optimisation only.

### 3. ACL interfaces - `ICatalogClient` and `IStockClient`

Presale defines ACL interfaces to decouple its domain from concrete BC service types.
Infrastructure wires them to the existing in-process service implementations. Future
microservice extraction requires only swapping the adapter, not changing Presale domain code.

```csharp
// Application/Presale/Checkout/Contracts/

public interface ICatalogClient
{
    // Returns the current unit price for a product, or null if not found/not visible.
    Task<decimal?> GetUnitPriceAsync(int productId, CancellationToken ct = default);
}

public interface IStockClient
{
    // Hard reservation at order placement (Slice 2 only).
    Task<bool> TryReserveAsync(int productId, int quantity, CancellationToken ct = default);
    Task ReleaseAsync(int productId, int quantity, CancellationToken ct = default);
}
```

`ICatalogClient.GetUnitPriceAsync` is called **once per checkout initiation** when creating a
`SoftReservation`. It is not called at add-to-cart time - `CartLine` carries no price.

`IStockClient` is reserved for **Slice 2** (hard reservation at order placement). In Slice 1,
availability is read from Presale's own `StockSnapshot` read model (section 6).

Infrastructure adapters (`CatalogClientAdapter` -> `IProductService`,
`StockClientAdapter` -> `IStockService`) live in `Infrastructure/Presale/Checkout/Adapters/`.

### 4. `SoftReservation` - DB entity + IMemoryCache dual storage

Soft reservations are owned entirely by Presale. They are advisory - they do not modify
`StockItem` hard counters. They represent the customer's checkout commitment, capturing price
and blocking perceived availability for up to `SoftReservationTtl` minutes.

```csharp
// Domain/Presale/Checkout/
public class SoftReservation
{
    public SoftReservationId Id { get; private set; }
    public int ProductId { get; private set; }
    public string UserId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }  // captured via ICatalogClient at checkout initiation
    public DateTime ExpiresAt { get; private set; }

    private SoftReservation() { }

    public static SoftReservation Create(
        int productId, string userId, int quantity, decimal unitPrice, DateTime expiresAt)
        => new SoftReservation
        {
            ProductId = productId,
            UserId = userId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            ExpiresAt = expiresAt
        };
}

// Application/Presale/Checkout/Services/
public interface ISoftReservationService
{
    Task<bool> HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default);
    Task<SoftReservation?> GetAsync(int productId, string userId, CancellationToken ct = default);
    Task RemoveAsync(int productId, string userId, CancellationToken ct = default);
    Task RemoveAllForProductAsync(int productId, CancellationToken ct = default);
}
```

**Storage strategy - dual storage (DB primary, cache speed layer):**
1. `HoldAsync` fetches `UnitPrice` from `ICatalogClient.GetUnitPriceAsync`.
2. Checks predicted availability: `StockSnapshot.AvailableQty - activeSoftReservationsForProduct >= quantity`.
   Returns `false` if insufficient.
3. Creates `SoftReservation` entity; writes to `presale.SoftReservations` via `ISoftReservationRepository`.
4. Schedules expiry cleanup: `IDeferredJobScheduler.ScheduleAsync(SoftReservationExpiredJob.JobTaskName, reservation.Id.Value.ToString(), reservation.ExpiresAt, ct)`.
5. Stores in `IMemoryCache` (speed layer for subsequent reads).
6. On cache miss during `GetAsync`: loads from DB, repopulates cache.
7. `RemoveAsync` / `RemoveAllForProductAsync` delete from DB and evict from cache.
   Also calls `IDeferredJobScheduler.CancelAsync` to discard the scheduled expiry job.

`SoftReservationService` is `internal sealed`. TTL is configured via
`PresaleOptions.SoftReservationTtl` (moved from `InventoryOptions.SoftHoldTtl`).

### 5. `SoftReservationExpiredJob` - TimeManagement-driven expiry cleanup

When a `SoftReservation` is created, its expiry is scheduled via `IDeferredJobScheduler`. The
TimeManagement BC fires the job at `ExpiresAt`, ensuring cleanup even after a process restart.

```csharp
// Application/Presale/Checkout/Handlers/
internal sealed class SoftReservationExpiredJob : IScheduledTask
{
    public const string JobTaskName = "SoftReservationExpiredJob";
    public string TaskName => JobTaskName;

    private readonly ISoftReservationRepository _reservations;
    private readonly IMemoryCache _cache;

    public SoftReservationExpiredJob(ISoftReservationRepository reservations, IMemoryCache cache)
    {
        _reservations = reservations;
        _cache = cache;
    }

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.EntityId is null || !int.TryParse(context.EntityId, out var id))
        {
            context.ReportFailure($"Invalid EntityId: '{context.EntityId}'.");
            return;
        }

        var reservation = await _reservations.GetByIdAsync(id, cancellationToken);
        if (reservation is null)
        {
            context.ReportSuccess("No-op: reservation already removed.");
            return;
        }

        await _reservations.DeleteAsync(reservation, cancellationToken);
        _cache.Remove(CacheKey(reservation.ProductId, reservation.UserId));
        context.ReportSuccess($"Expired SoftReservation {id} for product {reservation.ProductId}.");
    }

    private static string CacheKey(int productId, string userId) => $"sr:{productId}:{userId}";
}
```

The job is registered in TimeManagement at DI startup alongside `PaymentWindowTimeoutJob`.
`EntityId` is the `SoftReservationId.Value` serialised as a plain decimal string.

### 6. `StockAvailabilityChanged` + `StockSnapshot` - event-driven local read model

Presale needs to know the current available quantity per product to gate `SoftReservation`
creation (section 4, step 2) and to compute `predictedAvailable` for display. A 30-second
polling loop was rejected (see Alternatives). Instead, Inventory publishes a
`StockAvailabilityChanged` integration message every time its available quantity changes, and
Presale maintains a local `StockSnapshot` read model.

**Integration message (Inventory → Presale):**

```csharp
// Application/Inventory/Availability/Messages/
public record StockAvailabilityChanged(
    int ProductId,
    int AvailableQuantity,
    DateTime OccurredAt) : IMessage;
```

Published by `StockService` (Inventory BC) after every operation that changes available
quantity: `InitializeAsync`, `ReserveAsync`, `ReleaseAsync`, `ReturnAsync`, `AdjustAsync`.

**Handler (Presale):**

```csharp
// Application/Presale/Checkout/Handlers/
internal sealed class StockAvailabilityChangedHandler : IMessageHandler<StockAvailabilityChanged>
{
    private readonly IStockSnapshotRepository _snapshots;

    public async Task HandleAsync(StockAvailabilityChanged message, CancellationToken ct = default)
    {
        var snapshot = await _snapshots.FindByProductIdAsync(message.ProductId, ct);
        if (snapshot is null)
            await _snapshots.AddAsync(
                StockSnapshot.Create(message.ProductId, message.AvailableQuantity, message.OccurredAt), ct);
        else
        {
            snapshot.Update(message.AvailableQuantity, message.OccurredAt);
            await _snapshots.UpdateAsync(snapshot, ct);
        }
    }
}
```

**`StockSnapshot` domain type:**

```csharp
// Domain/Presale/Checkout/
public class StockSnapshot
{
    public int ProductId { get; private set; }          // PK — not IDENTITY
    public int AvailableQuantity { get; private set; }
    public DateTime LastSyncedAt { get; private set; }

    private StockSnapshot() { }

    public static StockSnapshot Create(int productId, int availableQty, DateTime lastSyncedAt)
        => new StockSnapshot { ProductId = productId, AvailableQuantity = availableQty, LastSyncedAt = lastSyncedAt };

    public void Update(int availableQty, DateTime lastSyncedAt)
    {
        AvailableQuantity = availableQty;
        LastSyncedAt = lastSyncedAt;
    }
}
```

`presale.StockSnapshots.ProductId` is the PK with no IDENTITY (`ValueGeneratedNever()`).
The in-memory broker delivers messages synchronously so propagation delay is sub-millisecond.

Presale has **exactly one** `IMessageHandler<T>` in Slice 1: `StockAvailabilityChangedHandler`.

### 7. Predicted available quantity

Presale computes an accurate "predicted available" for display by combining its local read model
with its own active soft reservations:

```
predictedAvailable = StockSnapshot.AvailableQuantity
                   - sum of active SoftReservations.Quantity for the product
```

Computed on read - never persisted. `StockSnapshot.AvailableQuantity` mirrors
`StockItem.AvailableQuantity` (i.e., `Quantity - ReservedQuantity`) from Inventory, kept
current via `StockAvailabilityChanged` events.

### 8. Price capture at checkout initiation

`SoftReservation.UnitPrice` is fetched from `ICatalogClient.GetUnitPriceAsync` at the moment
the customer initiates checkout (i.e., when `SoftReservationService.HoldAsync` is called). It
is never updated after creation. This is the authoritative transaction price - it flows into
`OrderItem.UnitPrice` at order placement (Slice 2).

`StorefrontController` exposes current Catalog prices for **display purposes only** - they are
not the authoritative transaction price. The checkout summary (Slice 2) must display a
price-change warning if `SoftReservation.UnitPrice != current Catalog price` at order
placement time.

### 9. DB schema (`presale.*`) and own DbContext

```
presale.CartLines
  UserId      nvarchar(450) NOT NULL  (composite PK — no IDENTITY)
  ProductId   int           NOT NULL  (composite PK — no IDENTITY)
  Quantity    int           NOT NULL

presale.SoftReservations
  Id          int           PK IDENTITY
  ProductId   int           NOT NULL
  UserId      nvarchar(450) NOT NULL
  Quantity    int           NOT NULL
  UnitPrice   decimal(18,2) NOT NULL
  ExpiresAt   datetime2     NOT NULL
  Status      int           NOT NULL  DEFAULT 0  (0=Active, 1=Committed)

presale.StockSnapshots
  ProductId          int       PK (NOT IDENTITY — ValueGeneratedNever)
  AvailableQuantity  int       NOT NULL
  LastSyncedAt       datetime2 NOT NULL
```

No `presale.StorefrontProducts` table. No `presale.Carts` or `presale.CartItems` tables.

```csharp
internal sealed class PresaleDbContext : DbContext
{
    public DbSet<CartLine> CartLines { get; set; }
    public DbSet<SoftReservation> SoftReservations { get; set; }
    public DbSet<StockSnapshot> StockSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("presale");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PresaleDbContext).Assembly,
            t => t.Namespace?.Contains("Presale.Checkout") == true);
    }
}
```

### 10. Folder structure

```
ECommerceApp.Domain/Presale/Checkout/
  CartLine.cs                     <- flat entity; composite PK (UserId, ProductId); no UnitPrice
  SoftReservation.cs              <- DB entity; UnitPrice captured at checkout initiation
  SoftReservationId.cs            <- typed ID (TypedId<int> pattern per ADR-0006)
  StockSnapshot.cs                <- local read model; ProductId PK (not IDENTITY)
  ICartLineRepository.cs
  ISoftReservationRepository.cs
  IStockSnapshotRepository.cs

ECommerceApp.Application/Presale/Checkout/
  Contracts/
    ICatalogClient.cs             <- ACL: query current unit price from Catalog
    IStockClient.cs               <- ACL: hard reserve/release against Inventory (Slice 2)
  Services/
    ICartService.cs
    CartService.cs                <- write-through: DB first, then IMemoryCache
    ISoftReservationService.cs
    SoftReservationService.cs     <- internal sealed; DB + IMemoryCache + IDeferredJobScheduler
    ICheckoutService.cs           <- Slice 2: cart-to-order coordination
    CheckoutService.cs            <- internal sealed; Slice 2: coordinates SoftReservations → IOrderService
    Extensions.cs
  Results/
    CheckoutResult.cs             <- Slice 2: success/failure result for PlaceOrderAsync
  Handlers/
    StockAvailabilityChangedHandler.cs  <- IMessageHandler<StockAvailabilityChanged>; upserts StockSnapshot
    SoftReservationExpiredJob.cs        <- IScheduledTask; deletes DB row + evicts cache entry
  DTOs/
    CartLineDto.cs
    AddToCartDto.cs
  ViewModels/
    CartVm.cs
    CartLineVm.cs
    SoftReservationPriceChangeVm.cs  <- Slice 2: price-change warning per checkout line
  PresaleOptions.cs               <- SoftReservationTtl (moved from InventoryOptions)

ECommerceApp.Application/Inventory/Availability/Messages/
  StockAvailabilityChanged.cs     <- integration message published by StockService

ECommerceApp.Infrastructure/Presale/Checkout/
  PresaleDbContext.cs
  PresaleDbContextFactory.cs
  PresaleConstants.cs
  Repositories/
    CartLineRepository.cs
    SoftReservationRepository.cs
    StockSnapshotRepository.cs
  Configurations/
    CartLineConfiguration.cs      <- composite PK (UserId, ProductId); no IDENTITY
    SoftReservationConfiguration.cs
    StockSnapshotConfiguration.cs <- ValueGeneratedNever for ProductId PK
  Adapters/
    CatalogClientAdapter.cs       <- ICatalogClient -> IProductService (Catalog BC)
    StockClientAdapter.cs         <- IStockClient -> IStockService (Inventory BC)
  Extensions.cs
  Migrations/
    (generated)

ECommerceApp.API/Controllers/Presale/
  StorefrontController.cs         <- BFF; not a BC; composes Catalog + Inventory per-request

ECommerceApp.Application/Sales/Orders/DTOs/
  PlaceOrderFromPresaleDto.cs     <- Slice 2: explicit price-per-line for presale checkout
  PlaceOrderLineDto.cs            <- Slice 2: (ProductId, Quantity, UnitPrice) per line
```

### 11. `ICheckoutService` — cart-to-order coordination

`ICheckoutService` is the Slice 2 entry point. It coordinates the cart-to-order transition.
Cart CRUD (`AddOrUpdateAsync`, `RemoveAsync`, `GetCartAsync`) stays in `ICartService`;
`ICheckoutService` owns only order placement.

```csharp
// Application/Presale/Checkout/Services/
public interface ICheckoutService
{
    // Slice 3 — locks prices for all cart items into SoftReservations (TTL = PresaleOptions.SoftReservationTtl).
    // Re-calling refreshes prices and resets TTL. Removes existing Active reservations first.
    Task<InitiateCheckoutResult> InitiateAsync(PresaleUserId userId, CancellationToken ct = default);

    Task<CheckoutResult> PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId,
        CheckoutCustomer customer, CancellationToken ct = default);
}
```

`CheckoutResult` is a result type with factory methods:

```csharp
// Application/Presale/Checkout/Results/
public sealed record CheckoutResult
{
    public bool IsSuccess { get; }
    public int? OrderId { get; }
    public string? FailureReason { get; }

    private CheckoutResult(bool isSuccess, int? orderId, string? failureReason)
    {
        IsSuccess = isSuccess;
        OrderId = orderId;
        FailureReason = failureReason;
    }

    public static CheckoutResult Success(int orderId) => new(true, orderId, null);
    public static CheckoutResult NoSoftReservations() => new(false, null, "Checkout not initiated. Call ISoftReservationService.HoldAsync first.");
    public static CheckoutResult StockUnavailable(int productId) => new(false, null, $"Product {productId} is no longer available in the requested quantity.");
    public static CheckoutResult OrderFailed(string reason) => new(false, null, reason);
}
```

`CheckoutService` is `internal sealed`. It is registered in
`Application/Presale/Checkout/Services/Extensions.cs`.

### 12. Checkout coordination flow

`CheckoutService.PlaceOrderAsync` executes a **commit / revert** pattern to guard against
the `SoftReservationExpiredJob` TOCTOU race:

1. **Load active soft reservations**: `ISoftReservationService.GetAllForUserAsync(userId, ct)`
   returns only `Active` reservations. Empty list → `CheckoutResult.NoSoftReservations()`.

2. **Commit** (TOCTOU guard): `ISoftReservationService.CommitAllForUserAsync(userId, ct)` marks
   all fetched reservations as `Committed` in the DB. `SoftReservationExpiredJob` checks
   `Status == Active` before deleting — `Committed` rows are skipped entirely, so the expiry
   job cannot race the confirmation.

3. **Place order**: Build lines from the already-loaded reservations using `SoftReservation.UnitPrice`
   (the locked price). Call `IOrderClient.PlaceOrderAsync(...)`. On failure:
   `ISoftReservationService.RevertAllForUserAsync(userId, ct)` flips status back to `Active`,
   restoring them to the normal expiry flow. Return `CheckoutResult.Failed(...)`.

4. **Return success**: `CheckoutResult.Succeeded(orderId)`. No explicit cleanup in the service.
   `OrderPlaced` is published by `OrderService`; `Presale.OrderPlacedHandler` reacts to it:
   - Removes ordered cart items via `ICartService.RemoveRangeAsync(userId, productIds)` (preserves
     any items the user added after checkout initiation).
   - Removes `Committed` reservations via `ISoftReservationService.RemoveCommittedForUserAsync`
     (cancels their deferred expiry jobs).

`CheckoutService.InitiateAsync`:
1. Load cart via `ICartService.GetCartAsync`. Empty cart → `InitiateCheckoutResult.EmptyCart()`.
2. Remove existing `Active` reservations for user (`RemoveActiveForUserAsync`) — clean slate for
   price/TTL refresh on re-initiation.
3. For each cart line call `ISoftReservationService.HoldAsync`. Track succeeded/unavailable product IDs.
4. Return `InitiateCheckoutResult.Reserved(count, unavailableIds)` or `AllUnavailable` if all failed.

### 13. `PlaceOrderFromPresaleAsync` — Sales/Orders BC extension

`IOrderService` gains one new method:

```csharp
Task<PlaceOrderResult> PlaceOrderFromPresaleAsync(PlaceOrderFromPresaleDto dto, CancellationToken ct = default);
```

Two new DTOs added to `Application/Sales/Orders/DTOs/`:

```csharp
public sealed record PlaceOrderLineDto(int ProductId, int Quantity, decimal UnitPrice);

public sealed record PlaceOrderFromPresaleDto(
    int CustomerId,
    int CurrencyId,
    string UserId,
    IReadOnlyList<PlaceOrderLineDto> Lines);
```

`OrderService.PlaceOrderFromPresaleAsync` follows the same flow as `PlaceOrderAsync` except it
uses the supplied `PlaceOrderLineDto.UnitPrice` per line instead of calling
`IOrderProductResolver` for a live Catalog price lookup. `PlaceOrderAsync` (legacy `CartItemIds`
path) is left unchanged — both methods co-exist on `IOrderService`.

The `OrderPlaced` message published by either method includes `Items: IReadOnlyList<OrderPlacedItem>`
which triggers `Inventory.OrderPlacedHandler` → `StockService.ReserveAsync` (the actual
Inventory hard reservation). The presale stock gate in §12 step 2 is a read-only pre-check;
the definitive hard reservation is owned by the Inventory BC.

### 14. Price-change warning at order placement

Between checkout initiation (`SoftReservationService.HoldAsync`) and confirmation
(`CheckoutService.PlaceOrderAsync`) the Catalog price may change. The order confirmation UI
must warn the customer before they confirm placement.

`ISoftReservationService` gains a second new method:

```csharp
Task<IReadOnlyList<SoftReservationPriceChangeVm>> GetPriceChangesAsync(PresaleUserId userId, CancellationToken ct = default);
```

```csharp
// Application/Presale/Checkout/ViewModels/
public sealed record SoftReservationPriceChangeVm(
    int ProductId,
    decimal LockedPrice,    // SoftReservation.UnitPrice — the authoritative transaction price
    decimal CurrentPrice);  // ICatalogClient.GetUnitPriceAsync — live Catalog price
```

`GetPriceChangesAsync` loads all active `SoftReservation`s for the user, calls
`ICatalogClient.GetUnitPriceAsync` for each product, and returns only lines where
`LockedPrice != CurrentPrice`. An empty list means prices have not changed since checkout
initiation.

**Policy**: `CheckoutService.PlaceOrderAsync` always uses `SoftReservation.UnitPrice` as the
authoritative transaction price (§8). The warning is advisory only — the customer may confirm
even if prices changed. `StorefrontController` (or the consuming controller) is responsible
for calling `GetPriceChangesAsync` before rendering the order confirmation view and displaying
a warning for each changed line.

### 15. `SoftReservationStatus` — TOCTOU guard (Slice 3)

Added to prevent `SoftReservationExpiredJob` from deleting a reservation mid-confirmation:

```csharp
// Domain/Presale/Checkout/
public enum SoftReservationStatus { Active = 0, Committed = 1 }
```

`SoftReservation` gains two domain methods:

```csharp
public void Commit() => Status = SoftReservationStatus.Committed;
public void Revert() => Status = SoftReservationStatus.Active;
```

`SoftReservationExpiredJob.ExecuteAsync` now checks `reservation.Status == Committed`
and reports success no-op if true — the reservation is being confirmed, expiry is irrelevant.

### 16. `BackgroundMessageDispatcher` — multi-handler fan-out (Slice 3)

The dispatcher previously resolved a single `IMessageHandler<T>` via `GetService` (singular),
causing all but the last-registered handler to be silently dropped. The fix:

- Resolved `GetMethod` once before the loop.
- Iterated all handlers via `GetServices` + `foreach`.
- Used `Task.WhenAll` for parallel fan-out.
- Replaced the `GetService`/null-check guard with a `tasks.Count == 0` check.

This unblocks all four `IMessageHandler<OrderPlaced>` registrations:
`OrderPlacedSnapshotHandler` (Orders), `Presale.OrderPlacedHandler`,
`Payments.OrderPlacedHandler`, `Inventory.OrderPlacedHandler`.

### 17. Checkout API endpoints (Slice 3)

```
POST /api/v2/checkout/initiate      → ICheckoutService.InitiateAsync
GET  /api/v2/checkout/price-changes → ISoftReservationService.GetPriceChangesAsync
POST /api/v2/checkout/confirm       → ICheckoutService.PlaceOrderAsync
```

All endpoints are `[Authorize]`. `Initiate` is idempotent — re-calling removes existing `Active`
reservations and re-creates them with fresh prices and TTL.

## Consequences

### Positive
- **No projection maintenance** - `StorefrontController` composes Catalog + Inventory on every
  request. No event handlers, no eventual consistency lag, no extra schema to migrate when
  product fields change.
- **Clear BC boundary** - Inventory owns hard stock; Presale owns customer intent (CartLine,
  SoftReservation). `StorefrontController` is an API surface concern, not a domain concern.
- **Soft reservation correctly owned and persisted** - SoftReservation leaves Inventory and is
  now DB-backed in Presale, surviving process restarts. The TimeManagement BC owns expiry
  scheduling, keeping Presale free of timer infrastructure.
- **ACL interfaces make dependencies explicit** - `ICatalogClient` and `IStockClient` document
  exactly what Presale needs from other BCs. Switching to HTTP adapters for microservice
  extraction requires changing only `Infrastructure/Presale/Checkout/Adapters/`.
- **Price captured at checkout initiation** - `SoftReservation.UnitPrice` is immutable after
  creation. Downstream order lines use this price regardless of later Catalog price changes.
- **Event-driven StockSnapshot** - sub-millisecond propagation via the in-memory broker. Presale
  never polls Inventory; Inventory publishes after every state change automatically.
- **Expected failures use result types** - `CartService` and `SoftReservationService` return
  `bool` or `null` for predictable outcomes (product not found, insufficient stock). 
  `BusinessException` is reserved for true domain invariant violations only.

### Negative
- `StorefrontController` makes two in-process service calls per listing request (Catalog +
  Inventory). Under high load this is two synchronous DB queries. Acceptable for a monolith;
  optimize with a materialized view or response cache only if profiling confirms a bottleneck.
- `StockSnapshot` adds eventual consistency between Inventory and Presale. The in-memory broker
  makes lag sub-millisecond in the monolith, but the design must be documented so future
  microservice extraction sets correct SLA expectations.
- Cart is node-local for reads (`IMemoryCache`). Multi-instance deployments should be aware that
  a cache miss causes a DB read; cart write-through means consistency is maintained regardless.

### Risks and mitigations
- **Risk**: `StorefrontController` stock query adds latency to product listing.
  **Mitigation**: Inventory queries a single index-backed counter per product - O(1) lookup.
  Introduce caching only after profiling confirms an issue.
- **Risk**: Soft reservation TTL misconfiguration expires holds too early or holds resources
  for too long.
  **Mitigation**: Configuration validation at startup via `IValidateOptions<PresaleOptions>`.
- **Risk**: `SoftReservationExpiredJob` fires after a manual early release, finding the row
  already deleted.
  **Mitigation**: Job guards with a null check and reports success as a no-op, matching the
  pattern established by `PaymentWindowTimeoutJob`.
- **Risk**: `StockSnapshot` is stale if `StockAvailabilityChanged` is never published for a
  product (e.g., product initialised before the handler was registered).
  **Mitigation**: `SoftReservationService.HoldAsync` returns `false` if no snapshot exists,
  preventing a hold against an unknown stock level.

## Alternatives considered

- **`StorefrontProduct` denormalized read model (projection) in Presale** - rejected.
  Requires 6+ Catalog event handlers + DB schema + EF migration. Introduces eventual consistency
  lag after every Catalog write. Duplicates Catalog data in a second schema, causing all Catalog
  field additions to require a Presale schema migration as well. The BFF approach achieves the
  same goal with zero duplication, zero propagation delay, and no additional schema.
- **Application-layer `ProductListingService` composing Catalog + Inventory** - equivalent to
  the BFF approach but placed in the Application layer. Rejected because composition of BCs for
  API consumers is an API surface concern. In a future microservices split it stays in the API
  gateway, not in the domain.
- **Soft hold in Inventory** - rejected; see ADR-0011 Alternatives. Soft reservations are a
  customer-intent concept, not a stock-commitment concept.
- **UnitPrice in CartLine** - rejected. The cart represents browsing intent; price changes
  between add-to-cart and checkout are normal. Capturing price at checkout initiation
  (SoftReservation) is the authoritative moment of commitment.
- **SoftReservation as IMemoryCache only** - rejected. A process restart would silently drop all
  active soft reservations, allowing double-booking at the hard-reservation step (Slice 2). DB
  persistence is required for correctness.
- **30-second polling for StockSnapshot** - rejected. Introduces a TOCTOU race: a reservation
  checked at T+0 may be stale by T+30. Event-driven updates from Inventory close this window to
  sub-millisecond and eliminate the polling background thread.
- **`ProductAdded` / `ProductUpdated` / `ProductMainImageUpdated` integration messages from
  Catalog** - rejected. Presale has no consumer for these messages in Slice 1, making them dead
  code. The Catalog BC should not publish messages nobody subscribes to. If a future BC requires
  them, they can be added then.

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

## Conformance checklist

- [ ] `StorefrontController` lives in `ECommerceApp.API/Controllers/Presale/` — not in any BC
- [ ] `CartLine` has no `UnitPrice` field — price belongs to `SoftReservation`
- [ ] `CartLine` composite PK is `(UserId, ProductId)` — no IDENTITY, no `CartLineId`
- [ ] `SoftReservation` is a DB entity — not a `sealed record`, has a `DbSet`
- [ ] `SoftReservation.UnitPrice` is set once at checkout initiation — no public setter
- [ ] `SoftReservationId` follows the `TypedId<int>` pattern (ADR-0006)
- [ ] `StockSnapshot.ProductId` is the PK with `ValueGeneratedNever()` — not IDENTITY
- [ ] `ICatalogClient` and `IStockClient` interfaces live in `Application/Presale/Checkout/Contracts/`
- [ ] `CatalogClientAdapter` and `StockClientAdapter` live in `Infrastructure/Presale/Checkout/Adapters/`
- [ ] `ISoftReservationService` has `RemoveAllForProductAsync(int productId, CancellationToken ct)` method
- [ ] `SoftReservationService` calls `IDeferredJobScheduler.ScheduleAsync` after every successful `HoldAsync`
- [ ] `SoftReservationService` calls `IDeferredJobScheduler.CancelAsync` on early `RemoveAsync`
- [ ] `SoftReservationService` implementation is `internal sealed`
- [ ] `SoftReservationExpiredJob` has `public const string JobTaskName = "SoftReservationExpiredJob"`
- [ ] `SoftReservationExpiredJob.ExecuteAsync` is a no-op (ReportSuccess) when reservation not found
- [ ] `StockAvailabilityChanged` is published by `StockService` after every quantity-changing operation
- [ ] `StockAvailabilityChangedHandler` is `internal sealed` and registered via `IMessageHandler<StockAvailabilityChanged>`
- [ ] `PresaleDbContext` uses schema `"presale"` — DbSets: `CartLines`, `SoftReservations`, `StockSnapshots`
- [ ] No cross-BC navigation properties — `ProductId`, `UserId` are plain value references
- [ ] `SoftReservationTtl` is in `PresaleOptions` — not in `InventoryOptions`
- [ ] Presale has exactly **one** `IMessageHandler<T>` in Slice 1: `StockAvailabilityChangedHandler`
- [ ] `CartService` and `SoftReservationService` return `bool` or `null` for expected business outcomes — no `BusinessException`
- [ ] `IProductService` has `GetUnitPriceAsync(int id)` returning `Task<decimal?>` before `CatalogClientAdapter` is implemented
- [ ] `StockService.ReserveAsync` no longer calls `_softHoldService.RemoveAsync` after atomic switch
- [ ] `ICheckoutService` is a separate interface from `ICartService` — cart CRUD ≠ checkout coordination
- [ ] `CheckoutService` is `internal sealed` and registered via `ICheckoutService` in `Extensions.cs`
- [x] `CheckoutResult` uses factory methods: `Succeeded`, `NoReservations`, `StockNotAvailable`, `Failed`
- [x] `CheckoutService.PlaceOrderAsync` does **not** call `IStockClient.TryReserveAsync` — the TOCTOU guard is
  `SoftReservationStatus.Committed` (commit/revert pattern); definitive hard reservation is made by
  `Inventory.OrderPlacedHandler` reacting to `OrderPlaced`
- [ ] The actual Inventory reservation is created by `Inventory.OrderPlacedHandler` reacting to `OrderPlaced` — `CheckoutService` never calls `IStockClient.ReleaseAsync`
- [ ] `CheckoutService.PlaceOrderAsync` maps `SoftReservation.UnitPrice` into `PlaceOrderLineDto.UnitPrice` — no fresh `ICatalogClient` call at placement time
- [ ] On order placement failure (step 3), soft reservations are NOT removed — they expire naturally via `SoftReservationExpiredJob`
- [ ] `ISoftReservationService` has `GetAllForUserAsync(PresaleUserId, CancellationToken)` returning `Task<IReadOnlyList<SoftReservation>>`
- [ ] `ISoftReservationService` has `GetPriceChangesAsync(PresaleUserId, CancellationToken)` returning `Task<IReadOnlyList<SoftReservationPriceChangeVm>>`
- [ ] `SoftReservationPriceChangeVm` has `ProductId`, `LockedPrice`, `CurrentPrice` — lives in `Application/Presale/Checkout/ViewModels/`
- [ ] `PlaceOrderFromPresaleDto` and `PlaceOrderLineDto` live in `Application/Sales/Orders/DTOs/`
- [ ] `IOrderService.PlaceOrderFromPresaleAsync` bypasses `IOrderProductResolver` — uses `PlaceOrderLineDto.UnitPrice` directly
- [ ] `PlaceOrderAsync` (legacy `CartItemIds` path) is left unchanged after introducing `PlaceOrderFromPresaleAsync`
- [ ] Price-change warning is advisory — `CheckoutService.PlaceOrderAsync` accepts the locked price regardless of current Catalog price
- [ ] `StorefrontController` (or consuming controller) calls `GetPriceChangesAsync` before rendering the order confirmation view

## Implementation Status

| Step | Description | Status |
|------|-------------|--------|
| 1 | Catalog domain: `Discontinued`, `UnpublishReason`, `Unpublish(reason)`, `Discontinue()`, `GetUnitPriceAsync` | ✅ Done |
| 2 | `StockAvailabilityChanged` message; `StockService` publishes after every quantity change | ✅ Done |
| 3 | `Domain/Presale/Checkout/`: `CartLine`, `SoftReservation`, `SoftReservationId`, `StockSnapshot`, repository interfaces | ✅ Done |
| 4 | `Application/Presale/Checkout/`: services, handlers (`StockAvailabilityChangedHandler`, `SoftReservationExpiredJob`), contracts, DTOs, `PresaleOptions` | ✅ Done |
| 5 | `Infrastructure/Presale/Checkout/`: `PresaleDbContext`, EF configs, repositories, adapters, DI; register `SoftReservationExpiredJob` in TimeManagement | ✅ Done |
| 6 | EF migration `InitPresaleSchema` | ✅ Done |
| 7 | `PresaleOptions.SoftReservationTtl` moved from `InventoryOptions.SoftHoldTtl`; startup validation | ✅ Done |
| 8 | Atomic switch: remove Inventory soft-hold artifacts; decouple `StockService.ReserveAsync` | ✅ Done |
| 9 | `StorefrontController` BFF endpoint | ✅ Done |
| 10 | Unit tests: `CartService`, `SoftReservationService`, `StockAvailabilityChangedHandler`, `SoftReservationExpiredJob` | ✅ Done |
| 11 | `ICheckoutService`, `CheckoutService`, `CheckoutResult` in `Application/Presale/Checkout/` | ✅ Done |
| 12 | `ISoftReservationService.GetAllForUserAsync` + `GetPriceChangesAsync`; `SoftReservationPriceChangeVm`; repository extension | ✅ Done |
| 13 | `IOrderClient` ACL + `OrderClientAdapter`; `CheckoutCustomer` inline customer data flowing end-to-end | ✅ Done |
| 14 | `CheckoutService.PlaceOrderAsync` + DI registration + `CheckoutController` (price-changes + confirm) + unit tests | ✅ Done |
| 15 | `SoftReservationStatus` enum + `Commit()`/`Revert()` domain methods + EF migration `AddSoftReservationStatus` | ✅ Done |
| 16 | `BackgroundMessageDispatcher` multi-handler fan-out fix (`GetService` → `GetServices` + `Task.WhenAll`) | ✅ Done |
| 17 | `ICheckoutService.InitiateAsync` + `POST /api/v2/checkout/initiate` endpoint + new service/repo methods | ✅ Done |
| 18 | `OrderPlacedHandler` scoped to `Committed` reservations only; `SoftReservationExpiredJob` skips `Committed` | ✅ Done |
| 19 | Unit tests updated: `CheckoutServiceTests`, `OrderPlacedHandlerTests`, `SoftReservationExpiredJobTests` | ✅ Done |
| 20 | `ICartService.RemoveRangeAsync` + `ICartLineRepository.DeleteRangeAsync`: batch cart-item removal; `OrderPlacedHandler` uses single call (preserves post-initiation cart items); `CartServiceTests` updated | ✅ Done |

## References

- Related ADRs:
  - [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 - Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 - Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md) (`Presale/Checkout` greenfield)
  - [ADR-0006 - TypedId and Value Objects as Shared Domain Primitives](./0006-typedid-and-value-objects-as-shared-domain-primitives.md) (`SoftReservationId` typed ID)
  - [ADR-0009 - Supporting TimeManagement BC Design](./0009-supporting-timemanagement-bc-design.md) (`IDeferredJobScheduler`, `IScheduledTask`; `SoftReservationExpiredJob` registered here)
  - [ADR-0010 - In-Memory Message Broker](./0010-in-memory-message-broker-for-cross-bc-communication.md) (`StockAvailabilityChanged` cross-BC integration; Presale has one handler in Slice 1)
  - [ADR-0011 - Inventory/Availability BC Design](./0011-inventory-availability-bc-design.md) (`StockAvailabilityChanged` published by `StockService`; soft hold removed from Inventory)
  - [ADR-0014 - Sales/Orders BC Design](./0014-sales-orders-bc-design.md) (`IOrderService.PlaceOrderFromPresaleAsync` extension; `OrderPlaced` triggers `Inventory.OrderPlacedHandler`)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
