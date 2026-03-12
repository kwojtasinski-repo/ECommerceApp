# Project State

> **Tactical snapshot for AI agents.** Updated after significant PRs or sprint boundaries.
> For full strategic BC map see [`docs/architecture/bounded-context-map.md`](../docs/architecture/bounded-context-map.md).
> For confirmed bugs see [`.github/context/known-issues.md`](./known-issues.md).
> For planned work see [`docs/roadmap/README.md`](../docs/roadmap/README.md).

*Last updated: 2026-03-12*

---

## What is actively in progress right now

| Area | State | Key blocker |
|---|---|---|
| **Sales/Orders BC** | Domain ✅ Application ✅ Infrastructure ✅ Unit tests ✅ — **DB migration pending approval**; integration tests + atomic switch not started | DB migration approval (migration-policy.md) |
| **Sales/Payments BC** | Domain ✅ Application ✅ Infrastructure ✅ Unit tests ✅ — **DB migrations pending approval**; integration tests + atomic switch not started | Orders atomic switch must complete first |
| **Frontend error pipeline** | ADR-0021 accepted — Phase 1 (error pipeline fix) and Phase 2 (bug fixes) **not yet started** | None — can start now |

---

## Parallel implementations ready to switch (waiting for migration approval)

These BCs are fully implemented alongside legacy code. Only migration approval, integration tests,
and the atomic switch (remove legacy code) remain.

| BC | Pending | ADR |
|---|---|---|
| **AccountProfile** | DB migration approval → integration tests → migrate `CustomerController` / `AddressController` / `ContactDetailController` → remove legacy `CustomerService` | [ADR-0005](../docs/adr/0005-accountprofile-bc-userprofile-aggregate-design.md) |
| **Catalog** | DB migration approval → integration tests → migrate `ItemController` / `ImageController` / `TagController` → atomic switch | [ADR-0007](../docs/adr/0007-catalog-bc-product-category-tag-aggregate-design.md) |
| **Currencies** | DB migration approval → integration tests → migrate `CurrencyController` (async) → coordinate with Catalog switch → atomic switch | [ADR-0008](../docs/adr/0008-supporting-currencies-bc-design.md) |
| **TimeManagement** | Two coordinated DB migrations approval → integration tests → `CurrencyRateSyncTask` atomic switch | [ADR-0009](../docs/adr/0009-supporting-timemanagement-bc-design.md) |
| **Inventory/Availability** | `InitInventorySchema` migration approval + data migration (`Items.Quantity` → `inventory.StockItems`) → integration tests → replace `ItemHandler` calls with `IMessageBroker` → atomic switch | [ADR-0011](../docs/adr/0011-inventory-availability-bc-design.md) |
| **Presale/Checkout Slice 1** | `InitPresaleSchema` migration approval → integration tests | [ADR-0012](../docs/adr/0012-presale-checkout-bc-design.md) |

---

## What is NOT started yet (in priority order)

1. **Presale/Checkout Slice 2** (steps 11–14 in ADR-0012) — blocked by Sales/Orders atomic switch
2. **Sales/Coupons BC** — blocked by Orders + Payments
3. **Sales/Fulfillment BC** — blocked by Orders + Payments
4. **Supporting/Communication BC** — blocked by Fulfillment Slice 1 + Coupons Slice 1
5. **Backoffice BC** — blocked by ADR-0013 (per-BC DbContext interfaces); gated by ~80% BC completion
6. **Per-BC DbContext interfaces** (ADR-0013) — gate: ~80–100% BC implementations complete

---

## Key architectural invariants (do not violate)

- `BusinessException` + `ExceptionMiddleware` pipeline — no raw try/catch in controllers
- All cross-BC integration via `IMessageBroker` (in-memory) — no direct service-to-service calls across BC boundaries
- `ApplicationUser` must not appear as a navigation property in any domain model — only `string UserId`
- Cart CRUD lives in `ICartService`; checkout coordination lives in `ICheckoutService` (Presale BC)
- `SoftReservation.UnitPrice` is immutable after creation — price captured at checkout initiation, not at add-to-cart
- `StockSnapshot` is updated via `StockAvailabilityChanged` event — never polled directly from Inventory

---

## Legacy code that co-exists with new BCs (do not extend)

These legacy classes exist in parallel with the new BC implementations.
**Do not add new features or fix bugs in these.** Direct future work to the new BC equivalents.

| Legacy | Replaced by | Switch pending |
|---|---|---|
| `Application/Services/Orders/OrderService.cs` | `Application/Sales/Orders/Services/OrderService.cs` | Orders atomic switch |
| `Application/Services/Payments/PaymentService.cs` + `PaymentHandler.cs` | `Application/Sales/Payments/Services/PaymentService.cs` | Payments atomic switch |
| `Application/Services/Customers/CustomerService.cs` | `Application/AccountProfile/Services/UserProfileService.cs` | AccountProfile atomic switch |
| `Application/Services/Currencies/CurrencyService.cs` | `Application/Supporting/Currencies/Services/CurrencyService.cs` | Currencies atomic switch |
| `Domain/Model/` (anemic models) | BC-specific rich aggregates under `Domain/<BC>/` | Per-BC atomic switches |
