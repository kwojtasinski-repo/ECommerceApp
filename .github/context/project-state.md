# Project State

> **Tactical snapshot for AI agents.** Updated after significant PRs or sprint boundaries.
> For full strategic BC map see [`docs/architecture/bounded-context-map.md`](../docs/architecture/bounded-context-map.md).
> For confirmed bugs see [`.github/context/known-issues.md`](./known-issues.md).
> For planned work see [`docs/roadmap/README.md`](../docs/roadmap/README.md).

*Last updated: 2026-06-07 (Current session: Communication BC implemented — 7 handlers (`OrderPlaced`, `OrderCancelled`, `OrderRequiresAttention`, `PaymentConfirmed`, `PaymentExpired`, `RefundApproved`, `RefundRejected`) + `INotificationService` + `LoggingNotificationService` stub + `IOrderUserResolver` port + `NullOrderUserResolver` + `Extensions.cs` + 14 unit tests + `App_Communication` architecture test. ADR-0018 Accepted. `DependencyInjection.cs` wired. Previous: `OrderProductSnapshot.ImageId` — renamed column `ImageUrl→ImageId`, retyped `nvarchar(2048)→int`, EF migration with data conversion. KI-009 added. 1024/1024 total tests passing.)*

---

## What is actively in progress right now

| Area | State | Key blocker |
|---|---|---|
| **Identity/IAM BC** | **✅ Switch complete** — All IAM features live. `Context` changed from `IdentityDbContext` → `DbContext` ✅. `Domain.Model.ApplicationUser` deleted ✅. Legacy controllers, services, repositories deleted. ADR-0019 Accepted. | None |
| **Sales/Orders BC** | **✅ Switch complete** — Legacy `OrderController`, `OrderItemController`, `OrderService`, `OrderItemService` deleted. Legacy views removed. | None |
| **Sales/Payments BC** | **✅ Switch complete** — Legacy `PaymentController`, `PaymentService`, `PaymentHandler` deleted. Legacy views removed. | None |
| **Presale/Checkout BC** | **✅ Slice 2 Switch live** — `ICheckoutService` + `CheckoutService` + `CheckoutResult` ✅, `ISoftReservationService.GetAllForUserAsync` + `GetPriceChangesAsync` ✅, `IOrderService.PlaceOrderFromPresaleAsync` ✅, `IOrderClient` ACL + `OrderClientAdapter` ✅, API endpoints `GET /price-changes` + `POST /confirm` ✅, unit tests ✅, integration tests ✅ (8 new: SoftReservationServiceTests ×8 + CheckoutServiceIntegrationTests ×4). EC-001 decision: Accept the race. | None — **switch is live** |

---

## Recently completed

| Area | Summary | ADR |
|---|---|---|
| **Supporting/Communication BC** | `INotificationService` + `LoggingNotificationService` stub. `IOrderUserResolver` port + `NullOrderUserResolver` null-object stub. 7 handlers: `OrderPlaced/Cancelled/RequiresAttention` (Orders), `PaymentConfirmed/Expired` (Payments), `RefundApproved/Rejected` (Fulfillment). `AddCommunicationServices()` DI extension. `App_Communication` architecture boundary test. 871/871 unit tests ✅. ADR-0018 Accepted. | [ADR-0018](../docs/adr/0018-supporting-communication-bc-design.md) |
| **OrderProductSnapshot.ImageId rename**
| **IAM — Refresh Token (Steps 1–8)** | All steps complete — `RefreshToken` domain entity ✅, EF config + migration ✅, `IJwtManager` updated (Jti) ✅, `AuthenticationService.SignInAsync/RefreshAsync/RevokeAsync` ✅, `AuthController` (`POST /api/auth/refresh` + `POST /api/auth/revoke`) ✅, `auth.http` ✅, unit tests ✅, integration tests ✅, `RefreshTokenCleanupTask` ✅. | [ADR-0019](../docs/adr/0019-identity-iam-bc-design.md) |
| **IAM — Refresh Token Steps 5–8** |
| **Security / Route fixes (R-1, R-3, R-4, R-5, R-6)** | R-6: `DeleteUser` → `[HttpPost]` + `[ValidateAntiForgeryToken]` · R-1: `RefundController.Request` param renamed `orderId→id` + view/tag-helper fixes · R-5: `OrdersController.Details` → maintenance-bypass ownership check · R-4: `PaymentsController.Details` → ownership check + `UserId` added to `PaymentDetailsVm` · R-3: `PaymentsController.Create` → `GetPendingByOrderIdAsync(id, GetUserId())` (user-scope + Pending guard). Build ✅ · 21/21 unit tests ✅ | — |
| **Jobs — switch live** | `JobManagementController` migrated to `Areas/Jobs`. 2 views (Index, History). Legacy `Controllers/JobManagementController.cs` + `Views/JobManagement/` deleted. Zaplecze nav updated to `asp-area="Jobs"`. Pure structural move — already used new BC services (`IJobManagementService`, `IJobTrigger` from `Application.Supporting.TimeManagement`). 1361/1361 tests passing. | [ADR-0009](../docs/adr/0009-supporting-timemanagement-bc-design.md), [ADR-0024](../docs/adr/0024-controller-routing-strategy.md) |
| **Currencies — switch live** | `CurrencyController` migrated to `Areas/Currencies`. 4 views (Index, Create, Edit, Details). Legacy `CurrencyController` + 4 legacy `Views/Currency/` views deleted. Zaplecze nav updated to `asp-area="Currencies"`. Service swapped from legacy sync `ICurrencyService` (Application.Services.Currencies) to new async `ICurrencyService` (Application.Supporting.Currencies.Services). Action renames: `AddCurrency→Create`, `EditCurrency→Edit`, `ViewCurrency→Details`, `DeleteCurrency→Delete`. 1361/1361 tests passing. | [ADR-0008](../docs/adr/0008-supporting-currencies-bc-design.md), [ADR-0024](../docs/adr/0024-controller-routing-strategy.md) |
| **Inventory/Availability — switch live** | `StockController` migrated to `Areas/Inventory`.
| **Sales/Fulfillment Slice 2 — switch live** | ShipmentController added to `Areas/Sales`.
| **Sales/Coupons Slice 1 — switch live** |
| **Sales/Fulfillment Slice 1 — switch live** |
| **Presale/Checkout Slice 2 — switch live** |
| **Sales/Payments — switch live** | All acceptance criteria met. Web Area controller + views wired. Integration tests: PaymentServiceTests (8), OrderPlacedHandlerTests (3), OrderPaymentConfirmedHandlerTests (2), OrderPaymentExpiredHandlerTests (2). 430 integration tests passing. Legacy `PaymentHandler` retained for Step 5 cleanup. | [ADR-0015](../docs/adr/0015-sales-payments-bc-design.md), [ADR-0024](../docs/adr/0024-controller-routing-strategy.md) |
| **Sales/Orders — switch live** |
| **API tiered access — implemented** | Trusted purchase policy (`api:purchase` claim OR `Service` role), max 5 units per product per API order line (hardcoded now, backoffice-configurable later via in-memory cache), payment URL returned from checkout confirm (`WebOptions:BaseUrl` + fixed path). Web quantity cap: `AddToCartDtoValidator` (99 limit). | [ADR-0025](../docs/adr/0025-api-tiered-access-trusted-purchase-policy.md) |
| **DB migrations approved** | All per-BC DB migrations approved for all 10 BCs (Orders, Payments, Coupons, Fulfillment, Inventory, Presale, Catalog, AccountProfile, Currencies, TimeManagement). Universal blocker removed. | — |
| **BC integration tests** | 96 new integration tests across 12 test files: 86 per-BC service tests (9 BCs) + 10 cross-BC event chain tests (3 files). Test infrastructure: `BcWebApplicationFactory`, `BcBaseTest<T>`, `SynchronousMultiHandlerBroker`, `TypedIdAwareValueGeneratorSelector`, `NoOpDbContextMigrator`. All tests passing (423 total). | — |
| **Frontend error pipeline** | Phase 1 (ExceptionResponse + errors.js) ✅ Phase 2 (bug fixes: ajaxRequest FormData, modalService denyAction, buttonTemplate type, validations ReDoS) ✅ Phase 3 (fetch-first new-code policy) ✅ ongoing Phase 4 (BS5 modalService rewrite + AMD cleanup / `addObjectPropertiesToGlobal` removed + DOMInitialized event-data pattern) ✅ | [ADR-0021](../docs/adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) |
| **Bootstrap 5 upgrade** | All views migrated to BS5.3.3; TomSelect 2.4.1 installed; modalService rewritten for BS5 API; BS4 attributes and jQuery plugin calls removed | [ADR-0023](../docs/adr/0023-bootstrap-5-upgrade.md) |
| **Navbar two-tier redesign** | Top bar (search + category filter + cart badge + user menu) ✅ Secondary nav (Kategorie for guests; management bar for MaintenanceRole) ✅ IStockQueryService + 5 Inventory views + InventoryController ✅ `_LoginPartial.cshtml` retired ✅ | [ADR-0022](../docs/adr/0022-navbar-two-tier-redesign.md) |

---

These BCs are fully implemented alongside legacy code. DB migrations are approved and integration tests are done.
Only the atomic switch (controller migration + remove legacy code) remains.

| BC | Pending | ADR |
|---|---|---|
| **AccountProfile** | ✅ **Switch live** — `ProfileController` migrated to `Areas/AccountProfile`, 8 views (Index, All, Create, Details, Edit, EditContactInfo, AddAddress, EditAddress), legacy `CustomerController`/`AddressController`/`ContactDetailController` + views deleted, nav updated. Legacy `ICustomerService` DI retained (legacy `OrderService` dependency). | [ADR-0005](../docs/adr/0005-accountprofile-bc-userprofile-aggregate-design.md) |
| **Catalog** | ✅ **Switch live** — `ProductController` + `CategoryController` + `TagController` + `ImageController` migrated to `Areas/Catalog`, 11 views (Product: Index/All/Details/Create/Edit; Category: Index/Create/Edit; Tag: Index/Create/Edit). Legacy `ItemController`/`ImageController`/`TagController`/`TypeController` + 14 views deleted. Nav updated. Legacy `IItemService` DI retained (legacy `OrderService` dependency). `IImageService` retained (Catalog `ImageController` still uses it). | [ADR-0007](../docs/adr/0007-catalog-bc-product-category-tag-aggregate-design.md) |
| **Currencies** | ✅ **Switch live** — `CurrencyController` in `Areas/Currencies`, new async BC service, legacy controller + views deleted, nav updated. | [ADR-0008](../docs/adr/0008-supporting-currencies-bc-design.md) |
| **TimeManagement** | ✅ **Switch complete** — `CurrencyRateSyncTask` already uses `ICurrencyRateService` from new BC; legacy `CurrencyRateDto` deleted. | [ADR-0009](../docs/adr/0009-supporting-timemanagement-bc-design.md) |
| **Inventory/Availability** | ✅ **Switch live** — `StockController` migrated to `Areas/Inventory`, 5 views, legacy controller + legacy views deleted, nav updated. | [ADR-0011](../docs/adr/0011-inventory-availability-bc-design.md) |
| **Presale/Checkout Slice 1** | Ready for production — no controller migration needed (Slice 1 is new BFF endpoints only) | [ADR-0012](../docs/adr/0012-presale-checkout-bc-design.md) |
| **Sales/Coupons Slice 1** | ✅ **Switch live** — `CouponController` migrated, legacy UI controllers removed, nav updated. Legacy service DI retained for Step 8 (OrderService dependency). **Slice 2 fully complete**: rule pipeline wired into `ApplyCouponAsync`, multi-coupon guard, audit trail (`CouponApplicationRecord`), `OrderPriceAdjusted` publication, `NoDiscountProduced` guard, handler rewrite, admin UI switched to `CreateCouponAsync`. Stacking strategy (Rule A + Rule B). Dead `AddCouponAsync` removed. DB migration wired via `IDbContextMigrator<CouponsDbContext>`. 44 unit tests passing. | [ADR-0016](../docs/adr/0016-sales-coupons-bc-design.md) |
| **Sales/Fulfillment Slice 1** | ✅ **Switch live** — `RefundController` migrated, legacy service DI removed, `InventoryRefundApprovedHandler` switched to Fulfillment messages. Legacy class files retained for Step 8 cleanup. | [ADR-0017](../docs/adr/0017-sales-fulfillment-bc-design.md) |

---

## What is NOT started yet (in priority order)

> **Strategy — Parallel Change, build-first:** All BCs are built alongside legacy code before any atomic switch is executed.
> Atomic switches are deferred until ~80–95% of backend BC implementations are complete.
> Two blocker types: **`implementation blocked`** (true stop — hard dependency missing) vs **`atomic switch blocked`** (implementation proceeds in parallel now).

1. **Presale/Checkout Slice 2** (steps 11–14 in ADR-0012) — ✅ **Switch live** — implementation complete, integration tests done, EC-001 decision documented
2. **Sales/Coupons Slice 2** (ADR-0016 §9) — ✅ **Switch complete**; Domain ✅ Application ✅ (rules engine, 15 evaluators + auto-injected CouponOversizeGuard = 16 total, contracts, workflow builder) Infrastructure ✅ (5 adapters/repos: StockAvailabilityChecker, CompletedOrderCounter, SpecialEventCache, CouponApplicationRecordRepository, NullRuntimeCouponSource); **design amendments completed** (§10): CouponOversizeGuard — always-on constraint rule with per-coupon `BypassOversizeGuard` override (no global toggle), Catalog→Coupons name sync (3 messages: ProductNameChanged, CategoryNameChanged, TagNameChanged + 3 handlers + IScopeTargetRepository); **stacking strategy implemented** in `ApplyCouponAsync` (Rule A: fixed-value guard vs `OriginalTotal`, Rule B: `effectivePrice` fail-fast + reduction cap, `OrderPriceAdjusted.NewPrice` multi-coupon fix); **dead code cleanup**: `AddCouponAsync` removed from `ICouponService`/`CouponService`; **atomic switch done** — controller in `Areas/Sales`, DI wired, `IDbContextMigrator<CouponsDbContext>` registered, DB migration auto-applies, no legacy code remains. 44 unit tests passing.
3. **Sales/Fulfillment Slice 2** (ADR-0017 §11) — ✅ **Switch live** — ShipmentController deployed to Areas/Sales, full shipment lifecycle UI (create/dispatch/deliver/fail). Domain ✅ Application ✅ Infrastructure ✅ Web ✅
4. **Supporting/Communication BC** — ✅ unblocked (Fulfillment Slice 1 + Coupons Slice 1 both live)
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
- **Controller routing strategy** ([ADR-0024](../../docs/adr/0024-controller-routing-strategy.md)): Web = new Area-based routes (`/Sales/Orders/*`, `/Presale/Checkout/*`), legacy controllers stay active; API = in-place swap, same routes; Identity = unchanged

---

## Legacy code that co-exists with new BCs (do not extend)

These legacy classes exist in parallel with the new BC implementations.
**Do not add new features or fix bugs in these.** Direct future work to the new BC equivalents.

| Legacy | Status |
|---|---|
| `Application/Services/Orders/OrderService.cs` | ✅ **Deleted** — replaced by `Application/Sales/Orders/Services/OrderService.cs` |
| `Application/Services/Payments/PaymentService.cs` + `PaymentHandler.cs` | ✅ **Deleted** — replaced by `Application/Sales/Payments/Services/PaymentService.cs` |
| `Application/Services/Customers/CustomerService.cs` | ✅ **Deleted** — replaced by `Application/AccountProfile/Services/UserProfileService.cs` |
| `Application/Services/Currencies/CurrencyService.cs` | ✅ **Deleted** — replaced by `Application/Supporting/Currencies/Services/CurrencyService.cs` |
| `Application/Services/Refunds/RefundService.cs` | ✅ **Deleted** — replaced by `Application/Sales/Fulfillment/Services/RefundService.cs` |
| `Application/Services/Coupons/CouponHandler.cs` | ✅ **Deleted** — replaced by `Application/Sales/Coupons/Services/CouponService.cs` |
| `Application/Services/Brands/BrandService.cs` | 🟡 **Retained** — no BC replacement yet; used by `Web/Controllers/BrandController.cs` |
| `Application/Services/Items/ImageService.cs` | 🟡 **Retained** — used by Catalog `ImageController` (Web Area) and API `ImageController` |
| `Domain/Model/` (anemic models) | 🟡 **Retained** — still used by `Context` DbSets and EF configurations; `ApplicationUser.cs` deleted |
| API `Controllers/V2/` namespace | ✅ **Moved** — all 12 controllers moved to `Controllers/` namespace |
