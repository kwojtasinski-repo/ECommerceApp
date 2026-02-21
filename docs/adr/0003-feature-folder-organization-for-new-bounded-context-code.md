# ADR-0003: Adopt Feature-Folder Organization for New Bounded Context Code

## Status
Accepted

## Date
2026-02-21

## Context

The current codebase is organized **horizontally by layer type** across all bounded contexts:

```
ECommerceApp.Domain/
  Model/          ← all aggregates flat: Order.cs, Payment.cs, Item.cs, Brand.cs, ...
  Interface/      ← all repository interfaces flat

ECommerceApp.Application/
  Services/
    Orders/       ← OrderService.cs, OrderItemService.cs
    Payments/     ← PaymentService.cs, PaymentHandler.cs
    Items/        ← ItemService.cs, ItemHandler.cs, ImageService.cs
    ...
  ViewModels/
    Order/        ← OrderVm.cs, NewOrderVm.cs, OrderDetailsVm.cs, ...
    Payment/      ← PaymentVm.cs, PaymentDetailsVm.cs, ...
    Item/         ← ItemVm.cs, EditItemVm.cs, NewItemVm.cs, ...
  DTO/            ← all DTOs flat: OrderDto.cs, PaymentDto.cs, ItemDto.cs, ...

ECommerceApp.Infrastructure/
  Repositories/   ← all repositories flat: OrderRepository.cs, PaymentRepository.cs, ...
  Database/
    Configurations/ ← all EF configs flat: OrderConfiguration.cs, PaymentConfiguration.cs, ...
```

This structure has the following consequences in the context of ADR-0002's BC evolution strategy:
- **BC boundaries are invisible** — all aggregates from all BCs live in the same `Model/` folder.
  It is impossible to tell, at a glance, which files belong to which BC.
- **Cross-BC coupling is hard to detect** — a service in `Services/Orders/` can freely import
  from `Services/Payments/` with no structural signal that a BC boundary is being crossed.
- **Big bang refactoring is too risky** — the codebase has ~200 files organized this way.
  Reorganizing all existing files simultaneously risks merge conflicts, broken tests, and
  extended development downtime with no business value delivered.

The goal is to make BC boundaries visible in the folder structure without disrupting
the existing, working implementation.

## Decision

We adopt a **Strangler Fig** approach to folder organization:

1. **Existing code stays in its current horizontal structure** — no file is moved for structural
   reasons alone. Existing code is only reorganized when a specific BC is actively refactored
   under a dedicated follow-up ADR (per ADR-0002 migration plan).

2. **All new code uses feature-folder (vertical slice) organization** — grouped by BC/feature
   first, then by artifact type within the BC.

### New folder convention

```
ECommerceApp.Domain/
  <BcName>/
    <Aggregate>.cs
    I<Aggregate>Repository.cs

ECommerceApp.Application/
  <BcName>/
    Services/
      <Service>.cs
      I<Service>.cs
    ViewModels/
      <Name>Vm.cs
    DTOs/
      <Name>Dto.cs

ECommerceApp.Infrastructure/
  <BcName>/
    Repositories/
      <Aggregate>Repository.cs
    Configurations/
      <Aggregate>Configuration.cs
```

### Example — new Availability BC

```
ECommerceApp.Domain/
  Availability/
    ResourceAvailability.cs
    IResourceAvailabilityRepository.cs

ECommerceApp.Application/
  Availability/
    Services/
      AvailabilityService.cs
      IAvailabilityService.cs
    DTOs/
      ResourceAvailabilityDto.cs

ECommerceApp.Infrastructure/
  Availability/
    Repositories/
      ResourceAvailabilityRepository.cs
    Configurations/
      ResourceAvailabilityConfiguration.cs
```

### Coexistence rule

During the transition period, both structures coexist in the same projects:
- Files under `Domain/Model/`, `Application/Services/`, `Application/ViewModels/`, `Application/DTO/`,
  `Infrastructure/Repositories/`, `Infrastructure/Database/Configurations/` — **existing code, do not move**.
- Files under `Domain/<BcName>/`, `Application/<BcName>/`, `Infrastructure/<BcName>/`
  — **new code, always use this structure**.

When a BC is explicitly refactored under a follow-up ADR, its files are moved from the old
horizontal structure to the new feature-folder structure as part of that ADR's migration plan.

### Namespace convention

Namespaces must match the folder structure:
```csharp
// New code
namespace ECommerceApp.Domain.Availability;
namespace ECommerceApp.Application.Availability.Services;
namespace ECommerceApp.Infrastructure.Availability.Repositories;

// Existing code (unchanged)
namespace ECommerceApp.Domain.Model;
namespace ECommerceApp.Application.Services.Orders;
namespace ECommerceApp.Infrastructure.Repositories;
```

## Consequences

### Positive
- New BC code is immediately visible and isolated by folder — no structural pollution of existing code.
- Cross-BC imports become obvious: if `Application/Orders/Services/` imports from
  `Application/Payments/Services/`, it is a visible BC boundary crossing.
- Easy to extract a BC into a separate project later — all BC files are already co-located.
- No risk to existing tests or functionality — existing files are not touched.
- Aligns with module-per-folder convention.

### Negative
- Two organizational styles coexist during the transition — developers must know which applies.
- `Infrastructure/Database/Context.cs` still references all entities — structural isolation
  is folder-level only, not enforced at the compiler or runtime level until BC DbContext
  interfaces are introduced (planned in ADR-0009).
- AutoMapper profiles and DI registration in `DependencyInjection.cs` must be updated
  for each new BC added under the new structure.

### Risks & mitigations
- **Developer confusion about which style to use**: mitigated by this ADR + `dotnet-instructions.md` § 17
  rule — new code always uses feature folders, old code is never moved without an explicit ADR.
- **Namespace collisions**: mitigated by the namespace convention above — new namespaces use
  `ECommerceApp.<Layer>.<BcName>` which does not overlap with existing `ECommerceApp.<Layer>.Model`
  or `ECommerceApp.<Layer>.Services`.

## Alternatives considered

- **Big bang reorganization of all files** — rejected because ~200 files across all layers would
  need to move simultaneously, risking merge conflicts, broken tests, and zero business value
  delivered during the refactoring window.
- **Keep horizontal structure for all new code** — rejected because it perpetuates the problem
  of invisible BC boundaries and makes the ADR-0002 evolution strategy unenforceable in practice.
- **Separate project per BC now** (e.g. `ECommerceApp.Orders`, `ECommerceApp.Payments`) — rejected
  because the project split is premature before BC boundaries are stabilized; deferred to
  the long-term evolution phase per ADR-0002 § 12.

## Migration plan

The migration is incremental and follows the Strangler Fig pattern:

1. All **new BCs** (Availability, Fulfillment, Communication, etc.) are created directly in the
   new feature-folder structure — no migration needed.
2. **Existing BCs** are migrated one at a time, only when a dedicated follow-up ADR is accepted
   for that BC's refactoring (per ADR-0002 refactoring progress tracker in
   `docs/architecture/bounded-context-map.md`).
3. The `Context.cs` `DbSet` registrations and `DependencyInjection.cs` registrations are updated
   incrementally as each BC migrates — no single large change.

## References

- Related ADRs:
  - [ADR-0001 — ECommerceApp Project Overview and Technology Stack](./0001-project-overview-and-technology-stack.md)
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - Planned: ADR-0009 — Bounded context autonomy enforcement policy
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 17
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Issues / PRs: <!-- link to PR when raised -->
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
