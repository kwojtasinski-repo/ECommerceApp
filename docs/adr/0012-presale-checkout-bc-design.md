# ADR-0012: Presale/Checkout BC - Cart, SoftReservation, and ACL-Based Pre-Sale Design

## Status
Accepted

## Date
2026-03-06

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

**Problem 3 - Cart price snapshot.**
When a customer adds a product to cart, the cart must capture the unit price at that exact
moment. Prices change in Catalog over time. The add-to-cart price is a Presale concern - it
requires an ACL interface to query the current Catalog price at add-to-cart time, after which
Presale owns that price snapshot independently.

## Decision

We introduce **Presale/Checkout** as a bounded context with two slices:

- **Slice 1 (this ADR)** - `Cart` aggregate + `SoftReservation` + ACL interfaces.
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

### 2. `Cart` aggregate

`Cart` is the primary Presale/Checkout aggregate. It captures unit prices at add-to-cart time
and manages the customer's pre-order intent.

```csharp
namespace ECommerceApp.Domain.Presale.Checkout;

public class Cart
{
    public CartId Id { get; private set; }
    public string UserId { get; private set; }

    private readonly List<CartItem> _items = new();
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    private Cart() { }

    public static Cart Create(CartId id, string userId)
        => new Cart { Id = id, UserId = userId };

    public void AddItem(int productId, int quantity, decimal unitPrice) { ... }
    public void UpdateQuantity(int productId, int quantity) { ... }
    public void RemoveItem(int productId) { ... }
    public void Clear() => _items.Clear();
}

public class CartItem
{
    public CartItemId Id { get; private set; }
    public int ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }  // snapshot from ICatalogClient at add time

    private CartItem() { }
}
```

`UnitPrice` is fetched from `ICatalogClient.GetUnitPriceAsync` at the moment `AddItem` is
called by `CartService`. It is never updated by subsequent Catalog price changes. This is the
authoritative price for the cart line item - it flows into `OrderItem.UnitPrice` at checkout.

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
    // Advisory soft hold. Returns false if insufficient available quantity.
    Task<bool> TryHoldAsync(int productId, int quantity, CancellationToken ct = default);

    Task ReleaseAsync(int productId, int quantity, CancellationToken ct = default);
}
```

Infrastructure adapters (`CatalogClientAdapter` -> `IProductService`,
`StockClientAdapter` -> `IStockService`) live in `Infrastructure/Presale/Checkout/Adapters/`.

### 4. `SoftReservation` - in-memory, TTL-driven (moved from Inventory)

Soft reservations are owned entirely by Presale. They are advisory - they do not modify
`StockItem` counters. The authoritative stock check is `IStockClient.TryHoldAsync`
(wrapping `IStockService.ReserveAsync`) at order placement (Slice 2).

```csharp
// Domain/Presale/Checkout/
public sealed record SoftReservation(
    int ProductId,
    string UserId,
    int Quantity,
    DateTime ExpiresAt);

// Application/Presale/Checkout/Services/
public interface ISoftReservationService
{
    Task HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default);
    Task<SoftReservation?> GetAsync(int productId, string userId, CancellationToken ct = default);
    Task RemoveAsync(int productId, string userId, CancellationToken ct = default);
    Task RemoveAllForProductAsync(int productId, CancellationToken ct = default);
}
```

`SoftReservationService` is `internal sealed`, backed by `IMemoryCache` with a secondary
`ConcurrentDictionary<int productId, HashSet<string> userIds>` index that enables
`RemoveAllForProductAsync` without scanning the entire cache. TTL is configured via
`PresaleOptions.SoftReservationTtl` (moved from `InventoryOptions.SoftHoldTtl`).

### 5. Predicted available quantity

Presale can compute an accurate "predicted available" for display by combining:

```
predictedAvailable = stockAvailable (via IStockClient / StorefrontController call)
                   - sum of active SoftReservations for product  (Presale-local, in-memory)
```

Computed on read - never persisted.

### 6. Cart and price capture (Slice 2 detail)

`CartItem.UnitPrice` is fetched from `ICatalogClient` at add-to-cart time. `StorefrontController`
exposes current prices for display purposes only - it is NOT the authoritative transaction price.
The authoritative price is `CartItem.UnitPrice` (Presale), which flows into `OrderItem.UnitPrice`
at order placement (Slice 2). A price-change warning should be shown at checkout if
`CartItem.UnitPrice != current Catalog price`.

### 7. DB schema (`presale.*`) and own DbContext

```
presale.Carts
  Id        int PK
  UserId    nvarchar(450) NOT NULL

presale.CartItems
  Id          int PK
  CartId      int FK -> presale.Carts
  ProductId   int NOT NULL
  Quantity    int NOT NULL
  UnitPrice   decimal(18,2) NOT NULL
```

`SoftReservation` is NOT persisted - `IMemoryCache` only. No `presale.StorefrontProducts` table.

```csharp
internal sealed class PresaleDbContext : DbContext
{
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("presale");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PresaleDbContext).Assembly,
            t => t.Namespace?.Contains("Presale.Checkout") == true);
    }
}
```

### 8. Folder structure

```
ECommerceApp.Domain/Presale/Checkout/
  Cart.cs
  CartItem.cs
  CartId.cs
  CartItemId.cs
  SoftReservation.cs          <- sealed record; advisory-only; not persisted
  ICartRepository.cs

ECommerceApp.Application/Presale/Checkout/
  Contracts/
    ICatalogClient.cs         <- ACL: query current unit price from Catalog
    IStockClient.cs           <- ACL: advisory hold against Inventory
  Services/
    ICartService.cs
    CartService.cs            <- add/update/remove; calls ICatalogClient + ISoftReservationService
    ISoftReservationService.cs
    SoftReservationService.cs <- internal sealed; IMemoryCache + ConcurrentDictionary index
    Extensions.cs
  DTOs/
    CartDto.cs
    CartItemDto.cs
    AddToCartDto.cs
  ViewModels/
    CartVm.cs
    CartItemVm.cs
  PresaleOptions.cs           <- SoftReservationTtl (moved from InventoryOptions)

ECommerceApp.Infrastructure/Presale/Checkout/
  PresaleDbContext.cs
  PresaleDbContextFactory.cs
  PresaleConstants.cs
  Repositories/
    CartRepository.cs
  Configurations/
    CartConfiguration.cs
    CartItemConfiguration.cs
  Adapters/
    CatalogClientAdapter.cs   <- ICatalogClient -> IProductService (Catalog BC)
    StockClientAdapter.cs     <- IStockClient -> IStockService (Inventory BC)
  Extensions.cs
  Migrations/
    (generated)

ECommerceApp.API/Controllers/Presale/
  StorefrontController.cs     <- BFF; not a BC; composes Catalog + Inventory per-request
```

## Consequences

### Positive
- **No projection maintenance** - `StorefrontController` composes Catalog + Inventory on every
  request. No event handlers, no eventual consistency lag, no extra schema to migrate when
  product fields change.
- **Clear BC boundary** - Inventory owns hard stock; Presale owns customer intent (Cart, soft
  hold). `StorefrontController` is an API surface concern, not a domain concern.
- **Soft reservation correctly owned** - `IMemoryCache` and TTL logic leaves Inventory.
  Inventory is purely DB-based after this change.
- **ACL interfaces make dependencies explicit** - `ICatalogClient` and `IStockClient` document
  exactly what Presale needs from other BCs. Switching to HTTP adapters for microservice
  extraction requires changing only `Infrastructure/Presale/Checkout/Adapters/`.
- **Price captured at add-to-cart** - `CartItem.UnitPrice` is immutable after add. Downstream
  order lines use this price regardless of later Catalog price changes.

### Negative
- `StorefrontController` makes two in-process service calls per listing request (Catalog +
  Inventory). Under high load this is two synchronous DB queries. Acceptable for a monolith;
  optimize with a materialized view or response cache only if profiling confirms a bottleneck.
- Soft reservations are node-local (`IMemoryCache`). Multi-instance deployments require upgrade
  to `IDistributedCache` (deferred - same situation as pre-ADR-0012).

### Risks and mitigations
- **Risk**: `StorefrontController` stock query adds latency to product listing.
  **Mitigation**: Inventory queries a single index-backed counter per product - O(1) lookup.
  Introduce caching only after profiling confirms an issue.
- **Risk**: Soft reservation TTL misconfiguration expires holds too early or holds resources
  for too long.
  **Mitigation**: Configuration validation at startup via `IValidateOptions<PresaleOptions>`.
- **Risk**: Cart price snapshot drifts from Catalog price if a product is repriced before
  checkout.
  **Mitigation**: This is intentional. The cart shows the locked-in price. The checkout
  summary (Slice 2) must display a price-change warning if
  `CartItem.UnitPrice != current Catalog price` at checkout time.

## Alternatives considered

- **`StorefrontProduct` denormalized read model (projection) in Presale** - rejected.
  Requires 6 Catalog event handlers + 1 Inventory event handler + DB schema + EF migration.
  Introduces eventual consistency (propagation delay after every Catalog/Inventory write).
  Duplicates Catalog domain data (name, price, tags, category) in a second schema, causing all
  Catalog field additions to require a Presale schema migration as well. The BFF approach at
  the API layer achieves the same single-endpoint goal with zero duplication, zero propagation
  delay, and no additional schema.
- **Application-layer `ProductListingService` composing Catalog + Inventory** - equivalent to
  the BFF approach but placed in the Application layer rather than the API controller. Rejected
  in favour of the API controller approach since composition of BCs for API consumers is an API
  surface concern, not a domain service concern. In a future microservices split it stays in the
  API gateway, not in the domain.
- **Soft hold in Inventory** - rejected; see ADR-0011 Alternatives. Soft reservations are a
  customer-intent concept, not a stock-commitment concept.

## Migration plan

**Slice 1 (this ADR) - no Orders dependency, can start now:**

1. Add `Discontinued` to `ProductStatus`, add `UnpublishReason` enum to Catalog domain. Update
   `Unpublish(reason)`, add `Discontinue()`, update `UpdateDetails()` to raise
   `ProductDetailsUpdated` domain event, add `ProductMainImageUpdated` domain event (ADR-0007).
2. Add new Catalog integration messages: `ProductAdded` (enriched with `Cost`, `Description`,
   `TagIds`), `ProductUpdated`, `ProductMainImageUpdated`. Update `ProductUnpublished` to carry
   `UnpublishReason`. Update `ProductService` to publish all messages via `IMessageBroker`.
3. Create `Domain/Presale/Checkout/` with `Cart`, `CartItem`, `CartId`, `CartItemId`,
   `SoftReservation`, `ICartRepository`.
4. Create `Application/Presale/Checkout/` with `ICatalogClient`, `IStockClient`, `ICartService`,
   `CartService`, `ISoftReservationService`, `SoftReservationService`, DTOs, `PresaleOptions`.
5. Create `Infrastructure/Presale/Checkout/` with `PresaleDbContext`, `presale.*` schema,
   `CartRepository`, `CatalogClientAdapter`, `StockClientAdapter`, DI registration.
6. Generate EF migration `InitPresaleSchema` targeting `PresaleDbContext`.
7. Move `SoftHoldTtl` from `InventoryOptions` to `PresaleOptions.SoftReservationTtl`.
8. Remove `ICheckoutSoftHoldService`, `CheckoutSoftHoldService`, `SoftHold` from Inventory
   codebase. Update Inventory unit tests accordingly.
9. Create `StorefrontController` in `ECommerceApp.API/Controllers/Presale/` (BFF endpoint).
10. Write unit tests for `SoftReservationService` and `CartService`.

**Slice 2 (future) - blocked by Sales/Orders BC:**

11. Implement checkout flow: cart -> `OrderPlaced` event -> hard reservation via `IStockClient`.
12. Add price-change warning logic at checkout: compare `CartItem.UnitPrice` vs current
    Catalog price at checkout time.

## Conformance checklist

- [ ] `StorefrontController` lives in `ECommerceApp.API/Controllers/Presale/` - not in any BC
- [ ] `Cart` and `CartItem` live under `Domain/Presale/Checkout/`
- [ ] `CartItem.UnitPrice` is set once at add-to-cart time - no public setter
- [ ] `ICatalogClient` and `IStockClient` interfaces live in `Application/Presale/Checkout/Contracts/`
- [ ] `CatalogClientAdapter` and `StockClientAdapter` live in `Infrastructure/Presale/Checkout/Adapters/`
- [ ] `SoftReservation` is a `sealed record` - not an entity, no `DbSet`
- [ ] `ISoftReservationService` has `RemoveAllForProductAsync(int productId, CancellationToken ct)` method
- [ ] `SoftReservationService` maintains a secondary `ConcurrentDictionary<int, HashSet<string>>` index
- [ ] `SoftReservationService` implementation is `internal sealed`
- [ ] `PresaleDbContext` uses schema `"presale"`
- [ ] No cross-BC navigation properties - `ProductId`, `UserId` are plain references
- [ ] `SoftReservationTtl` is in `PresaleOptions` - not in `InventoryOptions`
- [ ] Presale has zero `IMessageHandler<T>` registrations in Slice 1 - no event subscriptions

## References

- Related ADRs:
  - [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 - Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 - Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md) (`Presale/Checkout` greenfield)
  - [ADR-0007 - Catalog BC Design](./0007-catalog-bc-product-category-tag-aggregate-design.md) (source of `ProductAdded`, `ProductUpdated`, `ProductMainImageUpdated`; enriched messages)
  - [ADR-0010 - In-Memory Message Broker](./0010-in-memory-message-broker-for-cross-bc-communication.md) (cross-BC integration pattern; Presale Slice 1 has zero message subscriptions)
  - [ADR-0011 - Inventory/Availability BC Design](./0011-inventory-availability-bc-design.md) (`IStockService` -> `IStockClient` adapter; soft hold removed from Inventory)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
