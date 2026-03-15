# ADR-0022: Navbar Two-Tier Redesign

## Status
Proposed

## Date
2026-03-15

## Context

The existing single-tier navbar grew organically alongside new bounded contexts and V2
controllers. By the time the navbar redesign was triggered, several structural problems had
accumulated:

**Problem 1 — Flat, role-unaware navigation.**
All role-conditional links live in a single `<ul>` row. Guests, regular users, and back-office
staff (Service / Manager / Administrator) share the same horizontal bar, making the UI noisy
for customers and undersized for operations staff who need access to six distinct management
areas.

**Problem 2 — Missing customer-oriented features.**
There is no search bar with category filter anywhere in the UI. The "Kategorie" browse
(category → product list) is only reachable by typing a URL; no customer-facing dropdown
exists. The shopping cart badge is present but buried in the role-gated section.

**Problem 3 — No clear home for new Inventory views.**
Five new Inventory management views are planned (Przegląd stanu, Rezerwacje, Korekta stanu,
Oczekujące korekty, Historia zmian). The existing navbar has no logical anchor for them.
`IStockService` today exposes only operation methods; it has no list/query surface that
Inventory web views could call.

**Problem 4 — `_LoginPartial.cshtml` is a detached partial.**
The Identity partial is rendered as a free-floating `<ul>` and is not integrated with the
user-menu dropdown. Anonymous users see "Rejestracja / Logowanie" links with no visual
connection to the signed-in user avatar or the "Moje konto" concept.

**Problem 5 — Brand and CouponType management are unreachable from the navbar.**
`BrandController` (`[Authorize(Roles = ManagingRole)]`) and `CouponTypeController` exist and
have working CRUD views but are not linked from any navbar item. They are effectively hidden
from managers who need them.

**Existing codebase constraints:**
- Bootstrap 4 in use; `modalService.js` is BS4 hard-wired (ADR-0021 § 4 defers BS5 upgrade).
- All nav links must use legacy MVC controllers (`Item/`, `Type/`, `Refund/`, `Order/`,
  `Coupon/`, `Currency/`, `UserManagement/`, `JobManagement/`, `Brand/`). No V2-prefix
  controllers appear in the navbar.
- `IStockService` (Inventory BC) is operation-only — query methods must be added before any
  Inventory web view can be built.
- Role constants: `ManagingRole = "Administrator, Manager"`;
  `MaintenanceRole = "Administrator, Manager, Service"` (defined in `BaseController`).

## Decision

### 1. Two-tier layout

Replace the single-tier navbar with a two-tier structure:

```
TOP BAR  (everyone — logged in or not)
┌──────────────────────────────────────────────────────────────────────────────┐
│  ECommerceApp    [ 🔍 Szukaj produktów...   [Kategoria ▾] ]    🛒²   [👤▾] │
└──────────────────────────────────────────────────────────────────────────────┘

SECONDARY NAV — Guest / User  (below top bar, always visible)
┌──────────────────────────────────────────────────────────────────────────────┐
│  Kategorie ▾                                                                 │
└──────────────────────────────────────────────────────────────────────────────┘

SECONDARY NAV — Service / Manager / Admin  (replaces above when authorized)
┌──────────────────────────────────────────────────────────────────────────────┐
│  Kupony  │  Katalog ▾  │  Magazyn ▾  │  Realizacja ▾  │  Zaplecze ▾        │
└──────────────────────────────────────────────────────────────────────────────┘
```

The top bar is rendered for all visitors regardless of authentication state.
The secondary nav switches entirely based on role — it never shows both rows simultaneously.

### 2. Top-bar elements

| Element | Behaviour |
|---|---|
| Logo `ECommerceApp` | `Home/Index` |
| Search input `Szukaj produktów...` | On submit → `Item/Index?searchString=<q>&typeId=<id>` |
| `[Kategoria ▾]` filter | Dropdown populated from `ICategoryService.GetAllTypes()` (injected into layout); selecting a category sets `typeId` in the search form |
| 🛒 badge | `Order/ShowMyCart` — count fetched via `ajaxRequest.send("/OrderItem/OrderItemCount", ...)` (existing mechanism preserved); hidden for anonymous users |
| `[👤▾]` user menu | See § 3 below |

### 3. User menu `[👤▾]`

**Anonymous:**
```
[👤▾]
  Logowanie      → /Identity/Account/Login
  Rejestracja    → /Identity/Account/Register
```

**Signed-in (any role):**
```
[👤▾]
  Moje zamówienia   → Order/ShowMyOrders
  Moje płatności    → Payment/ViewMyPayments
  Dane kontaktowe   → Customer/Index
  ─────────────────
  Wyloguj           → /Identity/Account/Logout  (POST form)
```

`_LoginPartial.cshtml` is retired; its identity links are folded into this dropdown.

### 4. Secondary nav — Guest / User

```
Kategorie ▾
  <dynamic list from ICategoryService.GetAllTypes()>
  Each item → Item/Index?typeId=<id>
```

Both the search-bar `[Kategoria ▾]` filter and the secondary `Kategorie ▾` dropdown share the
same data source injected once into `_Layout.cshtml` via `@inject ICategoryService`.

### 5. Secondary nav — Service / Manager / Admin (`MaintenanceRole`)

```
Kupony            → Coupon/Index          (ManagingRole — Admin + Manager only)

Katalog ▾
  Produkty        → Item/Index
  Kategorie       → Type/Index            (admin CRUD, not the customer browse)
  Tagi            → Tag/Index
  Marki           → Brand/Index           (ManagingRole — previously unreachable)

Magazyn ▾                                 (all MaintenanceRole)
  Przegląd stanu       → Inventory/Index          [NEW view]
  Rezerwacje           → Inventory/Reservations   [NEW view]
  Korekta stanu        → Inventory/AdjustStock    [NEW view]
  Oczekujące korekty   → Inventory/PendingAdjustments [NEW view]
  Historia zmian       → Inventory/Audit          [NEW view]

Realizacja ▾                              (all MaintenanceRole)
  Zamówienia      → Order/Index
  Wydania         → Order/ShowOrdersPaid
  Zwroty          → Refund/Index

Zaplecze ▾                                (mixed roles — see below)
  Użytkownicy     → UserManagement/Index  (ManagingRole)
  Kupony - typy   → CouponType/Index      (ManagingRole — previously unreachable)
  Waluty          → Currency/Index        (MaintenanceRole)
  Zadania         → JobManagement/Index   (MaintenanceRole)
```

**Role visibility rules for secondary nav items:**
- Entire secondary nav bar (all five dropdowns) rendered only for `MaintenanceRole`.
- `Kupony` top-level link: rendered only for `ManagingRole` (Admin + Manager); Service users
  do not see it (they cannot access `CouponController` which is `ManagingRole`-gated).
- `Katalog ▾ → Marki`: rendered only for `ManagingRole`.
- `Zaplecze ▾ → Użytkownicy` and `Zaplecze ▾ → Kupony - typy`: rendered only for
  `ManagingRole`.

### 6. `IStockService` query extension

Before any Inventory web views can be built, `IStockService` must be extended with list/query
methods. The following signatures will be added to the interface and implemented in
`StockService`:

```csharp
Task<IReadOnlyList<StockItemDto>> GetAllAsync(CancellationToken ct = default);
Task<IReadOnlyList<ReservationDto>> GetReservationsAsync(CancellationToken ct = default);
Task<IReadOnlyList<PendingStockAdjustmentDto>> GetPendingAdjustmentsAsync(CancellationToken ct = default);
Task<IReadOnlyList<StockAuditEntryDto>> GetAuditAsync(CancellationToken ct = default);
```

DTOs `PendingStockAdjustmentDto` and `StockAuditEntryDto` are new; `StockItemDto` and
`ReservationDto` already exist in `Application/Inventory/Availability/DTOs/`.

### 7. New `InventoryController` (Web)

A new `ECommerceApp.Web/Controllers/InventoryController.cs` wraps the query surface of
`IStockService` and exposes five read-only actions:

```
[Authorize(Roles = MaintenanceRole)]
InventoryController
  GET Index()                → Przegląd stanu   (all stock items)
  GET Reservations()         → Rezerwacje        (active reservations)
  GET AdjustStock()          → Korekta stanu     (form + POST)
  GET PendingAdjustments()   → Oczekujące korekty
  GET Audit()                → Historia zmian
```

### 8. Retire `_LoginPartial.cshtml`

`_LoginPartial.cshtml` is removed after the `[👤▾]` user menu is wired into `_Layout.cshtml`.
The `<partial name="_LoginPartial" />` call in `_Layout.cshtml` is replaced by the inline
dropdown from § 3.

### 9. `Order/OrderRealization` — no nav link

`Views/Order/OrderRealization.cshtml` exists but has no direct nav entry. It remains
accessible via its existing in-flow link from order detail pages. Not added to the nav.

## Consequences

### Positive
- Customer-facing top bar is clean and role-agnostic; search + category browse are always
  visible.
- Back-office navigation is richer and structured — all management areas reachable from the
  navbar for the first time (Brand, CouponType, all Inventory views).
- `_LoginPartial.cshtml` is retired — Identity links move into the unified user menu.
- The `IStockService` query extension is an explicit, documented prerequisite — no Inventory
  view can be silently built without it.
- `Kategorie ▾` data source is a single injection point; search bar filter and secondary nav
  dropdown stay in sync automatically.

### Negative
- `_Layout.cshtml` grows significantly in complexity (two nav tiers, role guards, category
  injection, search form).
- `IStockService` interface change requires updating the implementation, DI registration, and
  any existing mocks in tests.
- Five new Inventory views + one new controller must be built before `Magazyn ▾` is usable.
- `_LoginPartial.cshtml` removal is a breaking change for any external or area layout that
  renders the partial by name.

### Risks & mitigations
- **Risk**: `ICategoryService.GetAllTypes()` is called on every page load (injected into
  layout). If the category list is large or the query is slow, every page incurs the overhead.
  **Mitigation**: `ICategoryService` already uses EF Core with a small `Type` table; add an
  in-memory cache if profiling shows impact.
- **Risk**: `IStockService` query methods added without proper authorization in
  `InventoryController` expose sensitive stock data.
  **Mitigation**: All five Inventory actions are gated with `[Authorize(Roles = MaintenanceRole)]`
  at the controller level.
- **Risk**: `Kupony` secondary-nav link is visible to Service role but the controller returns
  403.
  **Mitigation**: `Kupony` top-level link rendered only under `ManagingRole` guard (§ 5).
- **Risk**: `_LoginPartial.cshtml` is referenced in area layouts other than `_Layout.cshtml`.
  **Mitigation**: Search for all `<partial name="_LoginPartial"` usages before deletion;
  replace each with the inline dropdown or a dedicated `_UserMenuPartial.cshtml`.

## Alternatives considered

- **Single-tier navbar with a wider collapse menu** — rejected. Does not solve the
  role-audience mismatch; customer-facing items and back-office items remain interleaved.
- **Separate customer layout and back-office layout** — rejected. Doubles the layout
  maintenance surface. Two-tier with role-conditional secondary nav achieves the same
  separation in one file.
- **Inject `ICategoryService` into a `ViewComponent`** — viable but deferred. A
  `CategoryMenuViewComponent` would be cleaner for reuse, but the layout injection approach
  is simpler for a single layout file. The `ViewComponent` refactor can be done independently
  without changing nav behaviour.
- **Add Inventory query surface to a new `IStockQueryService`** — considered. Rejected in
  favour of extending `IStockService` to avoid a second DI registration and interface split
  for what is still the same aggregate. If the query surface grows significantly, extraction
  to `IStockQueryService` is the natural next step.
- **Keep `_LoginPartial.cshtml` and embed it inside the dropdown** — rejected. Partial renders
  its own `<ul>` and cannot be cleanly nested inside a Bootstrap dropdown `<ul>`.

## Migration plan

**Phase 1 — `IStockService` query extension (prerequisite for Phase 3):**
1. Add `PendingStockAdjustmentDto` and `StockAuditEntryDto` to
   `Application/Inventory/Availability/DTOs/`.
2. Add four query method signatures to `IStockService`.
3. Implement them in `StockService`; add repository queries as needed.
4. Update mocks in unit/integration tests that implement `IStockService`.

**Phase 2 — `_Layout.cshtml` two-tier navbar:**
5. Replace the single `<nav>` block with top bar + secondary nav structure.
6. Add search form (`GET Item/Index`) with category filter dropdown.
7. Move cart badge into top bar; preserve existing `ajaxRequest` cart-count mechanism.
8. Inline the `[👤▾]` user menu (anonymous + signed-in states); remove
   `<partial name="_LoginPartial" />`.
9. Wire secondary nav: `Kategorie ▾` for guests/users; management bar for `MaintenanceRole`.
10. Inject `@inject ICategoryService CategoryService` at top of `_Layout.cshtml`; populate
    both `[Kategoria ▾]` and `Kategorie ▾` from `CategoryService.GetAllTypes()`.

**Phase 3 — InventoryController + 5 views:**
11. Create `ECommerceApp.Web/Controllers/InventoryController.cs`.
12. Create views: `Views/Inventory/Index.cshtml`, `Reservations.cshtml`,
    `AdjustStock.cshtml`, `PendingAdjustments.cshtml`, `Audit.cshtml`.

**Phase 4 — Cleanup:**
13. Verify no other layout or area partial still renders `<partial name="_LoginPartial" />`.
14. Delete `_LoginPartial.cshtml`.
15. Update `project-state.md` and `known-issues.md`.

## Conformance checklist

- [x] Top bar rendered for all visitors (no `@if (SignInManager.IsSignedIn(...))` wrapper)
- [x] 🛒 badge hidden for anonymous users; cart-count `ajaxRequest` call preserved for signed-in users
- [x] `[👤▾]` anonymous state shows Logowanie + Rejestracja only
- [x] `[👤▾]` signed-in state shows Moje zamówienia, Moje płatności, Dane kontaktowe, Wyloguj
- [x] Secondary nav `Kategorie ▾` rendered for guests and `User` role only (not MaintenanceRole)
- [x] Secondary management bar rendered only for `MaintenanceRole`
- [x] `Kupony` top-level link guarded by `ManagingRole` (not visible to Service)
- [x] `Katalog ▾ → Marki` guarded by `ManagingRole`
- [x] `Zaplecze ▾ → Użytkownicy` and `Kupony - typy` guarded by `ManagingRole`
- [x] `[Kategoria ▾]` filter and `Kategorie ▾` dropdown both populated from single `ICategoryService` injection
- [ ] `IStockService` has `GetAllAsync`, `GetReservationsAsync`, `GetPendingAdjustmentsAsync`, `GetAuditAsync`
- [ ] `InventoryController` class-level `[Authorize(Roles = MaintenanceRole)]`
- [ ] All five Inventory views exist under `Views/Inventory/`
- [x] No `<partial name="_LoginPartial" />` call remains in `_Layout.cshtml`
- [ ] `_LoginPartial.cshtml` deleted after Phase 4 verification
- [x] No new views in this ADR introduce `ajaxRequest.js` (ADR-0021 § 3)

## References

- [ADR-0001 — Technology Stack](./0001-project-overview-and-technology-stack.md)
- [ADR-0021 — Frontend Error Pipeline and JS Migration Strategy](./0021-frontend-error-pipeline-and-js-migration-strategy.md)
  *(This navbar redesign is the first large new-code surface governed by the fetch-first
  standard from ADR-0021 § 3)*
- [ADR-0011 — Inventory Availability BC Design](./0011-inventory-availability-bc-design.md)
- [`ECommerceApp.Web/Views/Shared/_Layout.cshtml`](../../ECommerceApp.Web/Views/Shared/_Layout.cshtml)
- [`ECommerceApp.Web/Views/Shared/_LoginPartial.cshtml`](../../ECommerceApp.Web/Views/Shared/_LoginPartial.cshtml)
- [`ECommerceApp.Application/Inventory/Availability/Services/IStockService.cs`](../../ECommerceApp.Application/Inventory/Availability/Services/IStockService.cs)
- [`ECommerceApp.Web/Controllers/BaseController.cs`](../../ECommerceApp.Web/Controllers/BaseController.cs)
- [`ECommerceApp.Application/Catalog/Products/Services/ICategoryService.cs`](../../ECommerceApp.Application/Catalog/Products/Services/ICategoryService.cs)
