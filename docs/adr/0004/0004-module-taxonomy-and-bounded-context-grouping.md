# ADR-0004: Module Taxonomy and Bounded Context Grouping

## Status
Accepted

## Date
2026-02-21

## Context

ADR-0003 established that new code uses feature-folder organization grouped by bounded context (BC).
It did not define the canonical names, groupings, or hierarchy of those BCs.

Without a shared taxonomy:
- Different developers may create folders using different names for the same concept
  (e.g., `Products/` vs `Items/` vs `Catalog/`).
- BCs created independently may not reflect the domain groupings identified during event storming.
- The relationship between BCs (which ones belong to the same domain area) remains implicit.

This ADR records the canonical module hierarchy agreed upon as the target folder structure for
all new and migrated bounded context code across the solution.

## Decision

We adopt the following **module taxonomy** as the canonical BC grouping. Each top-level group maps
to a folder namespace within each project layer (`Domain`, `Application`, `Infrastructure`).
Leaf nodes are individual bounded contexts — each becomes a sub-folder within its group.

```
CORE BUSINESS
├── Sales
│   ├── Orders
│   ├── Payments
│   ├── Coupons
│   └── Fulfillment
│
├── Inventory
│   └── Availability
│
├── Presale
│   └── Checkout
│
├── Catalog
│   └── Products
│
├── Customer
│   └── CustomerProfile
│
├── Identity
│   └── IAM
│
├── Supporting
│   ├── Currencies
│   ├── TimeManagement
│   └── Communication
│
└── Backoffice
```

### Folder structure per project layer

```
ECommerceApp.Domain/
  Sales/Orders/        Sales/Payments/       Sales/Coupons/        Sales/Fulfillment/
  Inventory/Availability/
  Presale/Checkout/
  Catalog/Products/
  Customer/CustomerProfile/
  Identity/IAM/
  Supporting/Currencies/    Supporting/TimeManagement/    Supporting/Communication/
  Backoffice/

ECommerceApp.Application/
  Sales/Orders/Services/    Sales/Orders/ViewModels/    Sales/Orders/DTOs/
  Sales/Payments/Services/  ...
  Inventory/Availability/Services/  ...
  (same pattern for all BCs)

ECommerceApp.Infrastructure/
  Sales/Orders/Repositories/     Sales/Orders/Configurations/
  Sales/Payments/Repositories/   ...
  (same pattern for all BCs)
```

### Namespace convention

```csharp
namespace ECommerceApp.Domain.Sales.Orders;
namespace ECommerceApp.Application.Sales.Orders.Services;
namespace ECommerceApp.Infrastructure.Sales.Orders.Repositories;

namespace ECommerceApp.Domain.Inventory.Availability;
namespace ECommerceApp.Application.Supporting.Currencies.Services;
```

### Mapping of existing domain models to the new taxonomy

| Existing code (horizontal) | New taxonomy location | Status |
|---|---|---|
| `Domain/Model/Order.cs`, `OrderItem.cs` | `Domain/Sales/Orders/` | 🔴 Not migrated |
| `Domain/Model/Payment.cs`, `PaymentState.cs` | `Domain/Sales/Payments/` | 🔴 Not migrated |
| `Domain/Model/Coupon.cs`, `CouponType.cs`, `CouponUsed.cs` | `Domain/Sales/Coupons/` | 🔴 Not migrated |
| `Domain/Model/Item.cs`, `Image.cs`, `Tag.cs`, `Type.cs`, `Brand.cs`, `ItemTag.cs` | `Domain/Catalog/Products/` | 🔴 Not migrated |
| `Domain/Model/Customer.cs`, `Address.cs`, `ContactDetail.cs`, `ContactDetailType.cs` | `Domain/Customer/CustomerProfile/` | 🔴 Not migrated |
| `Domain/Model/Currency.cs`, `CurrencyRate.cs` | `Domain/Supporting/Currencies/` | 🔴 Not migrated |
| `Domain/Model/Refund.cs` | `Domain/Sales/Orders/` (refund is part of Orders lifecycle) | 🔴 Not migrated |
| `Domain/Model/ApplicationUser.cs` | `Domain/Identity/IAM/` | 🔴 Not migrated |
| *(none yet)* | `Domain/Inventory/Availability/` | ⬜ Greenfield |
| *(none yet)* | `Domain/Sales/Fulfillment/` | ⬜ Greenfield |
| *(none yet)* | `Domain/Presale/Checkout/` | ⬜ Greenfield |
| *(none yet)* | `Domain/Supporting/TimeManagement/` | ⬜ Greenfield |
| *(none yet)* | `Domain/Supporting/Communication/` | ⬜ Greenfield |
| Admin / UserManagement controllers | `Backoffice/` (Application layer only — no domain model) | 🔴 Not migrated |

### Rules

- New BCs are always created under their canonical group folder — never at the root of `Domain/`,
  `Application/`, or `Infrastructure/`.
- Existing code is migrated to the new taxonomy only when a dedicated follow-up ADR triggers
  the migration for that BC (per ADR-0003 Strangler Fig policy).
- The group name is part of the namespace — `ECommerceApp.Domain.Sales.Orders`, not
  `ECommerceApp.Domain.Orders`.
- `Backoffice` has no domain model of its own — it orchestrates existing BCs via application
  services. Its folder lives in `Application/Backoffice/` only.

## Consequences

### Positive
- Canonical names for all BCs are agreed — no naming ambiguity when creating new folders.
- Group-level structure (`Sales`, `Inventory`, `Supporting`) reflects the domain areas identified
  in event storming — readable as documentation.
- Cross-group imports are immediately visible and reviewable: if `Sales/Orders` imports
  from `Supporting/Currencies`, a BC boundary crossing is explicit in the namespace.
- Greenfield BCs (Availability, Fulfillment, etc.) have a clear home before any code is written.

### Negative
- Three-level namespace depth (`Domain.Sales.Orders`) is slightly more verbose than flat.
- Migration of existing code requires touching many files — mitigated by ADR-0003's incremental
  policy (only migrate when a dedicated ADR triggers it).

### Risks & mitigations
- **Taxonomy drift** — developers add folders outside the canonical groups:
  mitigated by this ADR as the authoritative reference + code review enforcement.
- **Ambiguous BC placement** — e.g., `Refund` in `Orders` vs its own BC:
  mitigated by the mapping table above; re-evaluate if Refund grows its own lifecycle complexity.

## Alternatives considered

- **Flat BC folders** (no group level, e.g. `Domain/Orders/`, `Domain/Payments/`) — rejected because
  at 12+ BCs the root folder becomes as unreadable as the current horizontal structure; grouping
  preserves domain legibility.
- **Group by technical concern** (e.g. `Core/`, `Infrastructure/`, `Support/`) — rejected because
  it mirrors the existing horizontal layer structure rather than the domain; event storming revealed
  domain-first grouping is more meaningful for this codebase.
- **Microservice-style naming** (one folder = one deployable unit) — rejected per ADR-0002; physical
  split is deferred; logical grouping is sufficient at this stage.

## References

- Related ADRs:
  - [ADR-0001 — Project Overview and Technology Stack](./0001-project-overview-and-technology-stack.md)
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 17
- Issues / PRs: <!-- link to PR when raised -->
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
