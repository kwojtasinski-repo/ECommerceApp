# Project State

> **Tactical snapshot for AI agents.** Updated after significant PRs or sprint boundaries.
> For full strategic BC map see [`docs/architecture/bounded-context-map.md`](../../docs/architecture/bounded-context-map.md).
> For confirmed bugs see [`.github/context/known-issues.md`](./known-issues.md).
> For planned work see [`docs/roadmap/README.md`](../../docs/roadmap/README.md).

_Last updated: 2026-08-26 — full BC migration is complete (all atomic switches live, all legacy
controllers/services/repos for migrated BCs removed). F1 (ADR slimming), F3 (saga engine + E2E
coverage), and F4 (event handler chain refactor) roadmap items are closed. Only F2 (shared event
contract location) and F5 Phase 3 (remaining per-BC `IModuleClient` adapters) remain, both
deferred by choice with no trigger fired — see `docs/roadmap/README.md`._

---

## What is actively in progress right now

Nothing. Every BC's atomic switch is live and legacy code for migrated BCs is deleted — see
[`bounded-context-map.md`](../../docs/architecture/bounded-context-map.md) for the authoritative
per-BC status table. If a new initiative starts, add its row here.

---

## Recently completed (high-level; see roadmap for detail)

| Area | Summary | Tracking |
| --- | --- | --- |
| **All BC atomic switches** | Every BC (Identity/IAM, Sales/Orders, Sales/Payments, Sales/Coupons, Sales/Fulfillment, Presale/Checkout, AccountProfile, Catalog, Currencies, TimeManagement, Inventory/Availability, Backoffice) is switched to its new Area-based controller with legacy controllers/services/views deleted. | [`bounded-context-map.md`](../../docs/architecture/bounded-context-map.md) |
| **Sales/Coupons rule authoring** | ✅ Complete — generated Create/Edit builder, shared canonical mapping/validation, scope-target reconciliation, and optimistic concurrency handling are live and covered by unit/E2E tests. | [`../../docs/adr/0016/rule-authoring-flow.md`](../../docs/adr/0016/rule-authoring-flow.md) |
| **F1 — ADR slimming** | Implementation-heavy sections extracted to companion docs for 9 ADRs (0007, 0009, 0011, 0012, 0014, 0015, 0016, 0017, 0030); Bucket B evaluated and left unchanged by explicit decision. Closed 2026-08-26. | [ADR-0011](../../docs/adr/0011/README.md) pilot + roadmap F1 |
| **F3 — Saga/Orchestrator engine** | Generic saga engine (Success/Failure/Notify propagation) with Refund, CartRecovery, and Inventory→Checkout reservation saga definitions; all 3 have `ECommerceApp.E2E.Backend` coverage through real production entry points. Closed 2026-08-18; E2E gate closed 2026-08-24. | [ADR-0032](../../docs/adr/0032/README.md), roadmap F3 |
| **F4 — Event handler chain refactoring** | `PaymentExpired`→`OrderCancelled` chain flattened (ADR-0026); duplicated shipment-outcome handlers deduplicated via `ShipmentStockOutcomeHandlerBase`. Closed. | roadmap F4 |
| **Guest checkout** | `GuestAccess` auth scheme + unified `OrderAccess` policy live; rate limiting closed; full E2E persona-ladder coverage. Only real email delivery remains a deliberate no-op (`IEmailSender`). | [ADR-0030](../../docs/adr/0030/README.md) |
| **Context-mode sandbox** | Retired 2026-08-10 — didn't pay for itself once upstream context handling improved. Implementation files removed; only the ADR remains as historical record. | [ADR-0029](../../docs/adr/0029/README.md) |

---

## What is NOT started yet

Nothing blocked. Open leftovers (KG CI gate, ADR-0026 dead-code decision, real email delivery,
`Context.cs` removal, minor checks) are listed in `docs/roadmap/README.md` "Open items" (verified
2026-10-15). F2 and F5 Phase 3 stay deferred by choice with no trigger condition met — see
"Future architectural considerations" there.

---

## Key architectural invariants (do not violate)

- `BusinessException` + `ExceptionMiddleware` pipeline — no raw try/catch in controllers
- All cross-BC integration via `IMessageBroker` (in-memory) — no direct service-to-service calls across BC boundaries
- `ApplicationUser` must not appear as a navigation property in any domain model — only `string UserId`
- Cart CRUD lives in `ICartService`; checkout coordination lives in `ICheckoutService` (Presale BC)
- `SoftReservation.UnitPrice` is immutable after creation — price captured at checkout initiation, not at add-to-cart
- `StockSnapshot` is updated via `StockAvailabilityChanged` event — never polled directly from Inventory
- **Controller routing strategy** ([ADR-0024](../../docs/adr/0024/README.md)): Web = Area-based routes (`/Sales/Orders/*`, `/Presale/Checkout/*`); API = in-place swap, same routes; Identity = unchanged

---

## Legacy code that co-exists with new BCs (do not extend)

These legacy classes existed in parallel with the new BC implementations during the migration.
**Do not add new features or fix bugs in anything still marked non-deleted below.**

| Legacy                                                                  | Status                                                                                                                                                         |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Application/Services/Orders/OrderService.cs`                           | ✅ **Deleted** — replaced by `Application/Sales/Orders/Services/OrderService.cs`                                                                               |
| `Application/Services/Payments/PaymentService.cs` + `PaymentHandler.cs` | ✅ **Deleted** — replaced by `Application/Sales/Payments/Services/PaymentService.cs`                                                                           |
| `Application/Services/Customers/CustomerService.cs`                     | ✅ **Deleted** — replaced by `Application/AccountProfile/Services/UserProfileService.cs`                                                                       |
| `Application/Services/Currencies/CurrencyService.cs`                    | ✅ **Deleted** — replaced by `Application/Supporting/Currencies/Services/CurrencyService.cs`                                                                   |
| `Application/Services/Refunds/RefundService.cs`                         | ✅ **Deleted** — replaced by `Application/Sales/Fulfillment/Services/RefundService.cs`                                                                         |
| `Application/Services/Coupons/CouponHandler.cs`                         | ✅ **Deleted** — replaced by `Application/Sales/Coupons/Services/CouponService.cs`                                                                             |
| `Application/Services/Brands/BrandService.cs`                           | ✅ **Deleted** — `BrandController.cs` also deleted; `Brand` entity remains as a legacy domain model attribute (no dedicated BC)                                |
| `Application/Services/Items/ImageService.cs`                            | ✅ **Moved to Catalog BC** — now at `Application.Catalog.Images.Services.ImageService`; used by Catalog `ImageController` (Web Area) and API `ImageController` |
| `Domain/Model/` (anemic models)                                         | ✅ **Deleted** — the whole legacy `Domain/Model/` folder is gone; verified empty/absent 2026-08-26                                                              |
| API `Controllers/V2/` namespace                                         | ✅ **Moved** — all 12 controllers moved to `Controllers/` namespace                                                                                            |
| Inventory `PaymentWindowTimeoutJob.cs`                                  | ✅ **Deleted 2026-08-17** — redundant standalone reservation-timeout timer, superseded by event-driven `PaymentExpiredHandler` reacting to Payments' `PaymentExpired` |
