# ADR-0013: Per-BC DbContext Interfaces — Compiler-Enforced BC Persistence Boundaries

## Status
Accepted

## Date
2026-03-09

## Context

Every new bounded context already has its own physical `DbContext` class (e.g.,
`AvailabilityDbContext`, `CatalogDbContext`, `PresaleDbContext`), giving physical persistence
isolation. However, all repositories and background services inject the **concrete class**
directly:

```csharp
// current — concrete class injected
public StockItemRepository(AvailabilityDbContext context) { ... }
public CurrencyRepository(CurrencyDbContext context) { ... }
```

Without an interface layer, nothing at the **compiler level** prevents a developer from
injecting `CatalogDbContext` into an Inventory repository — the DI container would resolve it
if the registration is present. The only guard today is code review and convention.

A full audit of the six new BC DbContexts (see conformance checklist) reveals:

| BC | DbContext | Accessibility | Repositories | Extra direct usages |
|---|---|---|---|---|
| Inventory/Availability | `AvailabilityDbContext` | `public` | 4 | — |
| Presale/Checkout | `PresaleDbContext` | `internal sealed` ✅ | 3 | — |
| Catalog/Products | `CatalogDbContext` | `public` | 3 | — |
| Supporting/Currencies | `CurrencyDbContext` | `public` | 2 | — |
| AccountProfile | `UserProfileDbContext` | `public` | 1 | — |
| Supporting/TimeManagement | `TimeManagementDbContext` | `public` | 3 | `DeferredJobPollerService` + `JobDispatcherService` resolve via scope factory ⚠️ |
| Identity/IAM | `IamDbContext` | `public` | (framework-owned) | Excluded — ASP.NET Core Identity manages this internally |

`PresaleDbContext` is already `internal sealed`, which is partial protection. The other five
public contexts can be freely injected outside their BC. `TimeManagementDbContext` is also
resolved directly from `IServiceScope` in two background services, so those call-sites must be
updated alongside the repository constructors.

The bounded context map (`docs/architecture/bounded-context-map.md`) targets:

```
│ IAvailabilityDbCtx │   │ ICatalogDbContext │   │ ICurrencyDbCtx │
```

and states "each BC owns its DbContext interface — no cross-BC `DbSet` access". This ADR
formalises that target and provides the implementation checklist.

## Decision

Each new BC exposes a typed `internal` interface that declares only the `DbSet<T>` properties
owned by that BC and `SaveChangesAsync`. The interface lives **in the Infrastructure layer**,
co-located with the concrete `DbContext` it abstracts. Repositories and background services
that currently inject the concrete class are updated to depend on the interface. The concrete
class is made `internal` in all BCs where it is currently `public`.

**Interface placement rationale:** `DbSet<T>` is an EF Core type. Placing the interface in
Domain would add an EF Core dependency to the Domain project, breaking the clean-architecture
constraint. Infrastructure is the correct layer — repositories are already `internal sealed`
in Infrastructure, so the interface never needs to cross an assembly boundary.

### Interface contract (one per BC)

```csharp
// ECommerceApp.Infrastructure/Inventory/Availability/IAvailabilityDbContext.cs
internal interface IAvailabilityDbContext
{
    DbSet<StockItem> StockItems { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<ProductSnapshot> ProductSnapshots { get; }
    DbSet<PendingStockAdjustment> PendingStockAdjustments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

The concrete class implements the interface without changing its public constructor signature:

```csharp
internal sealed class AvailabilityDbContext : DbContext, IAvailabilityDbContext { ... }
```

### Repository injection update

```csharp
// before
public StockItemRepository(AvailabilityDbContext context) { ... }

// after
public StockItemRepository(IAvailabilityDbContext context) { ... }
```

### Background service scope resolution update (TimeManagement only)

```csharp
// before
var context = scope.ServiceProvider.GetRequiredService<TimeManagementDbContext>();

// after
var context = scope.ServiceProvider.GetRequiredService<ITimeManagementDbContext>();
```

### DI registration

The concrete `DbContext` is registered via `AddDbContext<TContext>` as usual (required for
EF Core design-time tooling and migrations). The interface is registered as a scoped alias:

```csharp
// in each BC's Extensions.cs  AddXxxInfrastructure(...)
services.AddDbContext<AvailabilityDbContext>(opts => ...);
services.AddScoped<IAvailabilityDbContext>(
    sp => sp.GetRequiredService<AvailabilityDbContext>());
```

### Accessibility hardening

All `public` BC `DbContext` classes are changed to `internal`. `PresaleDbContext` is already
`internal sealed` and requires no accessibility change. `IamDbContext` is excluded — the
ASP.NET Core Identity framework requires it to be resolvable by its own stores and cannot be
hidden behind a custom interface.

### BCs in scope

| BC | Interface | Notes |
|---|---|---|
| Inventory/Availability | `IAvailabilityDbContext` | Make concrete `internal` |
| Presale/Checkout | `IPresaleDbContext` | Concrete already `internal sealed`; add interface + DI alias |
| Catalog/Products | `ICatalogDbContext` | Make concrete `internal` |
| Supporting/Currencies | `ICurrencyDbContext` | Make concrete `internal` |
| AccountProfile | `IUserProfileDbContext` | Make concrete `internal` |
| Supporting/TimeManagement | `ITimeManagementDbContext` | Make concrete `internal`; update 2 background service scope call-sites |
| Identity/IAM | — | Excluded |

## Consequences

### Positive
- Compiler rejects any repository that tries to inject a DbContext belonging to a different BC.
- Cross-BC persistence coupling becomes structurally impossible, not just conventionally forbidden.
- Interfaces are `internal` — no accidental leakage to Application or Domain.
- Unit-testing repositories that currently require `DbContext` can now use lightweight mocks or
  in-memory substitutes via the interface.
- Establishes a consistent, reviewable pattern for future BCs (Sales/Orders, Sales/Payments, etc.).

### Negative
- Additional boilerplate per BC (one interface file + one DI alias registration).
- DI registration for each BC grows by one line.
- Design-time tooling (`dotnet ef migrations add`) continues to target the concrete class —
  no impact, but the two registrations must be kept in sync.

### Risks & mitigations
- **Risk:** DI registration of the alias is forgotten, causing a runtime `InvalidOperationException`
  on first request. **Mitigation:** Integration test smoke-test (existing `CustomWebApplicationFactory`
  resolves all registered services on startup).
- **Risk:** EF Core `DbContext` pooling conflicts with the alias registration. **Mitigation:**
  Do not use `AddDbContextPool` for BC contexts — `AddDbContext` (scoped lifetime) is sufficient
  and is already used across all BCs.

## Alternatives considered

- **Interfaces in Domain** — rejected because `DbSet<T>` is an EF Core type; placing it in
  Domain would couple the Domain project to EF Core.
- **Interfaces in Application** — rejected for the same reason; Application layer should not
  reference EF Core directly.
- **No interfaces; rely on `internal` accessibility alone** — `internal` prevents access from
  other assemblies, but all BC code lives in the same `ECommerceApp.Infrastructure` assembly
  today. The interface provides intra-assembly enforcement that `internal` alone cannot.
- **Separate Infrastructure assemblies per BC** — would also enforce the boundary at the
  compiler level but requires a significant project restructuring that is out of scope for
  this change. This remains an option for a future ADR.

## References

- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy §8](0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0004 — Module Taxonomy and Bounded Context Grouping](0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0007 — Catalog BC Design](0007-catalog-bc-product-category-tag-aggregate-design.md)
- [ADR-0008 — Currencies BC Design](0008-supporting-currencies-bc-design.md)
- [ADR-0009 — TimeManagement BC Design](0009-supporting-timemanagement-bc-design.md)
- [ADR-0011 — Inventory/Availability BC Design](0011-inventory-availability-bc-design.md)
- [ADR-0012 — Presale/Checkout BC Design](0012-presale-checkout-bc-design.md)
- [Bounded Context Map](../architecture/bounded-context-map.md)
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
