# ADR-0020: Backoffice BC — Admin Aggregation and Reporting Design

## Status
Proposed

## Date
2026-03-12

## Context

ECommerceApp needs an administrative interface for cross-BC aggregated views: sales dashboards,
order summaries, payment reports, refund overviews, user management tables, and catalog management
aggregations. These views span multiple BCs and are read-only from an admin perspective.

Currently:
- Admin queries are scattered across legacy services (`ItemService`, `OrderService`,
  `PaymentService`, `CustomerService`, etc.)
- No dedicated admin reporting strategy or architectural home exists for cross-BC read models
- Existing controllers (`UserManagementController`, `ItemController`, `OrderController`) mix
  command and query concerns with admin-specific aggregations
- The BC classification table in the architecture map has always listed Backoffice as
  `Application-only | no domain model` but no ADR has ever defined its scope or patterns

### Key observations

- Backoffice views aggregate data across BCs (e.g., order + payment + customer in one view)
- Admin operations are almost exclusively reads — mutations go through the respective BC services
- No domain logic lives in Backoffice — it is a thin query-assembly layer
- Cross-BC queries via EF Core navigation properties are the current pattern — this creates
  implicit coupling that must be replaced as BC DbContexts are separated (ADR-0013)

## Decision

We will define **Backoffice** as an **application-layer-only BC** with no domain model. It
assembles admin-facing read models by querying individual BC services or read interfaces. It
issues no commands of its own — all mutations are delegated to the owning BC's service.

### § 1 BC classification

| Property | Value |
|---|---|
| Type | Application-only (no domain model) |
| Layer ownership | `Application.Backoffice` |
| Domain model | None |
| Own DbContext | None — queries BC service interfaces or read contexts |
| Pattern | CQRS read-side query assembly |
| Commands | None — delegates to owning BC services |

### § 2 Scope

| Area | Admin view | Source BC |
|---|---|---|
| Orders | Order list, order detail, orders by customer | Orders BC (ADR-0014) |
| Payments | Payment list, payment detail, unpaid orders | Payments BC (ADR-0015) |
| Refunds | Refund list, refund detail, refunds by order | Fulfillment BC (ADR-0017) |
| Catalog | Product list, product detail, tag/category management | Catalog BC (ADR-0007) |
| Customers | Customer list, customer detail, customer orders | AccountProfile BC (ADR-0005) |
| Users | User list, user detail, role management | Identity/IAM BC (ADR-0019) |
| Coupons | Coupon list, coupon usage | Coupons BC (ADR-0016) |
| Currencies | Currency list, rate history | Currencies BC (ADR-0008) |
| Jobs | Scheduled/deferred job status | TimeManagement BC (ADR-0009) |

### § 3 Query assembly pattern

Backoffice services inject BC-specific **read interfaces** (not write services) to assemble views:

```csharp
// Application/Backoffice/Services/IBackofficeOrderService.cs
public interface IBackofficeOrderService
{
    Task<PagedResult<BackofficeOrderListVm>> GetOrdersAsync(BackofficeOrderQuery query, CancellationToken ct);
    Task<BackofficeOrderDetailVm> GetOrderDetailAsync(int orderId, CancellationToken ct);
    Task<PagedResult<BackofficeOrderListVm>> GetOrdersByCustomerAsync(int customerId, CancellationToken ct);
}
```

Each Backoffice service aggregates from one or two BC services only. If a view requires data
from more than two BCs, it is split into separate services with the view assembled at the
controller/UI layer.

### § 4 Folder structure

```
Application/Backoffice/
  Services/
    IBackofficeOrderService.cs + BackofficeOrderService.cs
    IBackofficePaymentService.cs + BackofficePaymentService.cs
    IBackofficeCatalogService.cs + BackofficeCatalogService.cs
    IBackofficeCustomerService.cs + BackofficeCustomerService.cs
    ... (one pair per domain area)
  ViewModels/
    Orders/
    Payments/
    Catalog/
    ...
  Extensions.cs    ← AddBackoffice(IServiceCollection)
```

No `Domain/Backoffice/` — there is no domain model.  
No `Infrastructure/Backoffice/` — no own DbContext. Queries go through BC service abstractions.

### § 5 Cross-BC query isolation rule

Backoffice services **must not** access a BC's `DbContext` directly. They go through:
1. The BC's own application service interface (for simple reads already exposed), or
2. A dedicated BC read interface added specifically for Backoffice use (prefixed `IBackoffice*`
   or added as an overload on the existing service interface).

This rule ensures BC schemas can evolve independently. It is a prerequisite for ADR-0013
(per-BC DbContext interfaces) to be enforceable at the Backoffice layer.

### § 6 Existing legacy controllers

`UserManagementController`, `ItemController` (admin portions), `OrderController` (admin portions),
`PaymentController` (admin portions), `RefundController`, and `CurrencyController` contain
admin-view logic today. Migration to Backoffice services happens as part of each BC's atomic
switch, not as a separate refactor pass.

### § 7 Blocking dependencies

Full Backoffice implementation is gated by:
- ADR-0013 (per-BC DbContext interfaces) — needed before Backoffice can safely query across BCs without creating new coupling
- Each BC's atomic switch — until a BC is switched, Backoffice uses the legacy service

## Consequences

### Positive
- Admin query logic has a clear home — not scattered across BC services
- No domain model means no risk of Backoffice state leaking into BC aggregates
- Read-only contract prevents Backoffice from accidentally mutating BC state
- Pattern is simple to test — query services with mock BC interfaces

### Negative
- Adds an indirection layer for admin views — each admin page may require one additional service
- Until ADR-0013 is fully implemented, cross-BC queries still risk implicit coupling at the DB level

### Risks & mitigations
- **N+1 queries in aggregated views**: mitigated by injecting BC read interfaces that support batch queries; reviewed per view during implementation
- **Backoffice bypassing BC services to query DB directly**: mitigated by the cross-BC query isolation rule (§ 5) enforced in code review

## Alternatives considered

- **Dedicated read database / materialized views** — rejected because current read traffic does not justify the operational cost; threshold-based adoption per ADR-0002 §3
- **GraphQL admin API** — rejected because admin views are server-rendered MVC; GraphQL adds tooling overhead with no clear benefit at current scale
- **Embed admin aggregations directly in each BC service** — rejected because it forces BC services to know about admin requirements and creates cross-BC coupling in BC application services

## Migration plan

Migration is incremental — no big-bang refactor:

1. Create `Application/Backoffice/` folder structure and `Extensions.cs`
2. For each BC atomic switch, move admin-specific query logic from legacy service/controller into the corresponding Backoffice service
3. After ADR-0013 is fully implemented, audit Backoffice services to confirm no direct DbContext access
4. Remove legacy admin query methods from BC services once all Backoffice services are migrated

## Conformance checklist

- [ ] No `Domain/Backoffice/` folder exists
- [ ] No `Infrastructure/Backoffice/DbContext` — Backoffice has no own DbContext
- [ ] All Backoffice services inject only BC service interfaces — no direct `DbContext` usage
- [ ] Backoffice services issue no commands — all mutations delegate to owning BC service
- [ ] Each Backoffice service aggregates from at most two BC sources; wider views assembled at controller layer
- [ ] `Extensions.cs` registers all Backoffice services via `AddBackoffice(IServiceCollection)`
- [ ] All view models live under `Application/Backoffice/ViewModels/`

## References

- [ADR-0002 §3 — CQRS read model separation threshold](./0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0013 — Per-BC DbContext Interfaces](./0013-per-bc-dbcontext-interfaces.md)
- [ADR-0003 — Feature-Folder Organization](./0003-feature-folder-organization-for-new-bounded-context-code.md)
- [ADR-0004 — Module Taxonomy and BC Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0005 — AccountProfile BC](./0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0007 — Catalog BC](./0007-catalog-bc-product-category-tag-aggregate-design.md)
- [ADR-0014 — Sales/Orders BC](./0014-sales-orders-bc-design.md)
- [ADR-0015 — Sales/Payments BC](./0015-sales-payments-bc-design.md)
- [ADR-0016 — Sales/Coupons BC](./0016-sales-coupons-bc-design.md)
- [ADR-0017 — Sales/Fulfillment BC](./0017-sales-fulfillment-bc-design.md)
- [ADR-0019 — Identity/IAM BC](./0019-identity-iam-bc-design.md)
