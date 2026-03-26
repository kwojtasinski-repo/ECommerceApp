# Remaining BC Migration — Work & Effort Report

> Generated: 2026-05-27
> Source of truth: `bc-migration-status.md` (rev 3), `iam-atomic-switch.md`, `iam-refresh-token.md`, `orders-atomic-switch.md`, `payments-atomic-switch.md`, `project-state.md`
>
> **T-shirt sizes**: XS < 30 min · S = 30 min–2 h · M = 2–4 h · L = 4 h–1 day · XL = multi-day

---

## Summary

| Priority | Work stream | Size | Blocker? |
|----------|-------------|------|---------|
| 1 | Security / route quick-fixes (R-1, R-4, R-5, R-6) | 4 × XS | None — do immediately |
| 2 | IAM Refresh Token — Steps 5–8 (API controller, `.http`, integration tests) | S + XS + S | `AddRefreshTokensTable` migration approval |
| 3 | IAM Atomic Switch — Steps 1–9 | M | `InitIamSchema` migration approval + refresh token Step 5–8 done first |
| 4 | Legacy view cleanup (Refund, Coupon, CouponType, CouponUsed) | XS | IAM controller must be live first (else Coupon views are still reachable via legacy) |
| 5 | Payments cleanup — Steps 5–6 (PaymentHandler + PaymentWindowTimeoutJob) | S | At least one payment window cycle in production confirmed |
| 6 | Sales Atomic Switch — Orders + Payments + OrderItems Step 8 | L | R-3, R-4, R-5 fixed; Payments Step 5 done first |
| 7 | Feature gaps (R-2, R-3, R-7, R-8) | S–M each | R-3 gates Sales switch; R-2/R-7/R-8 are independent |

Total remaining: **~2–3 focused engineering days** (excluding migration approval wait time).

---

## Priority 1 — Security / Route Quick-Fixes

> No dependencies. Fix these in one PR before anything else.

### R-6 · `DeleteUser` missing HTTP verb attribute — **XS**

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/IAM/Controllers/UserManagementController.cs` |
| **Defect** | `DeleteUser(string id)` has no `[HttpPost]` / `[HttpDelete]` — responds to GET by default; delete-on-GET is a CSRF / safety bug |
| **Fix** | Add `[HttpPost]` (or `[HttpDelete]`) + `[ValidateAntiForgeryToken]` above the action |
| **Effort** | XS — 1 line + 1 line |
| **Test** | Existing IAM integration tests should still pass; add a "GET must return 405" assertion if feasible |

---

### R-1 · `RefundController.Request` param name mismatch — **XS**

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/Sales/Controllers/RefundController.cs` |
| **Defect** | Action is `Request(int orderId)` — parameter `orderId` does not bind to `{id?}` path segment; actual route resolves to `?orderId=x` (query string) instead of `/Sales/Refund/Request/{x}` |
| **Fix** | Rename parameter to `id`: `Request(int id)` (update all internal usages) |
| **Effort** | XS — 1-line rename, update any `asp-route-orderId` tag helpers in `Request.cshtml` |
| **Test** | Smoke-test: navigate to `/Sales/Refund/Request/1` returns correct view |

---

### R-5 · `OrdersController.Details` — no ownership scope check — **XS**

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/Sales/Controllers/OrdersController.cs` |
| **Defect** | `Details(int id)` returns order data to any authenticated user regardless of whether it belongs to them |
| **Fix** | After fetching the order, compare `order.UserId` to `User.FindFirstValue(ClaimTypes.NameIdentifier)`; throw `BusinessException` / return `Forbid()` if mismatch (follow `RefundController.Details` pattern) |
| **Effort** | XS — ~5 lines, identical pattern already used in other controllers |
| **Test** | Unit test: other-user's order → 403 / exception |

---

### R-4 · `PaymentsController.Details` — no ownership scope check — **XS**

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/Sales/Controllers/PaymentsController.cs` |
| **Defect** | `Details(int id)` has no `payment.UserId ≠ caller` check |
| **Fix** | Same pattern as R-5 above |
| **Effort** | XS — ~5 lines |
| **Test** | Unit test: other-user's payment → 403 / exception |

---

## Priority 2 — IAM Refresh Token (Steps 5–8)

> Steps 1–4 are ✅ done. Steps 5–8 complete the feature before the IAM atomic switch.
> Gated by: `AddRefreshTokensTable` migration approval (process, not code).

### Step 5 — `AuthController` (new refresh + revoke endpoints) — **S**

| File | Action |
|---|---|
| `ECommerceApp.API/Controllers/V2/AuthController.cs` (new) | `POST /api/auth/refresh` — accepts `{ refreshToken }`, calls `IAuthenticationService.RefreshAsync`, returns `SignInResponseDto`; `POST /api/auth/revoke` — `[Authorize]`, calls `RevokeAsync`, returns `204` |
| `ECommerceApp.API/Startup.cs` | Register `AuthController` route / ensure DI is wired (likely already is via `AddIamServices`) |

**Effort**: S — 2 endpoints, ~40 lines, no new service code.

---

### Step 6 — HTTP scenario file — **XS**

| File | Action |
|---|---|
| `ECommerceApp.API/HttpScenarios/auth.http` (new) | Sign-in → capture `refreshToken` → call `POST /api/auth/refresh` → call `POST /api/auth/revoke` |

**Effort**: XS — follow existing `.http` file patterns (see `presale.http`).

---

### Step 7 — Integration tests — **S**

| File | Coverage |
|---|---|
| `ECommerceApp.IntegrationTests/Identity/IAM/RefreshTokenIntegrationTests.cs` (new) | Sign-in → refresh → new token valid; refresh with revoked token → `BusinessException`; revoke → subsequent refresh fails |

**Effort**: S — 3 test scenarios, reuse `BcBaseTest<IamDbContext>` pattern.

---

### Step 8 (refresh token roadmap) — Expiry cleanup job — **XS (optional, low priority)**

| File | Action |
|---|---|
| `ExpiredRefreshTokenCleanupJob` | Piggyback on existing `JobManagement` infrastructure — register a Hangfire job that calls `IRefreshTokenRepository.DeleteExpiredAsync` |

**Effort**: XS — deferred; not a blocker for the atomic switch.

---

## Priority 3 — IAM Atomic Switch (Steps 1–9)

> Requires: `InitIamSchema` migration approved + refresh token Steps 5–7 done.
> Full plan: `docs/roadmap/iam-atomic-switch.md`

| Step | File(s) | Action | Size |
|------|---------|--------|------|
| 1 | `InitIamSchema` migration + `AddRefreshTokensTable` migration | Get production sign-off (process, no code) | — |
| 2 | `Web/Areas/Identity/Pages/Account/Login.cshtml.cs` | Swap `Application.Services.Authentication.IAuthenticationService` → `Application.Identity.IAM.Services.IAuthenticationService` | XS |
| 2 | `API/Controllers/LoginController.cs` | Same namespace swap | XS |
| 3 | `Web/Controllers/UserManagementController.cs` | Swap `IUserService` (`Application.Services.Users`) → `IUserManagementService` (`Application.Identity.IAM.Services`); align action methods to new async API | S |
| 4 | `Web/appsettings.json`, `Web/appsettings.Development.json`, `API/appsettings*.json` | Set `"UseIamStore": true` | XS |
| 5 | `Domain/Model/Order.cs` | Replace `ApplicationUser User { get; set; }` with `string UserId { get; set; }` — coordinate with Sales switch PR | XS |
| 5 | `Infrastructure/Database/Configurations/OrderConfiguration.cs` | Remove `HasOne(o => o.User)` EF config | XS |
| 6 | **Delete legacy IAM files** (7 files — see table below) | File removals | XS |
| 6 | `Application/DependencyInjection.cs` | Remove legacy `IAuthenticationService` + `IUserService` DI registrations | XS |
| 7 | `Infrastructure/Identity/IAM/Auth/Extensions.cs` | Remove `UseIamStore` conditional branch; register IAM services unconditionally | XS |
| 7 | `Infrastructure/Identity/IAM/Auth/IamFeatureOptions.cs` | Delete | XS |
| 7 | `appsettings*.json` (all) | Remove `UseIamStore` key | XS |

**Legacy IAM files to delete in Step 6:**

| File |
|---|
| `Application/Services/Authentication/IAuthenticationService.cs` |
| `Application/Services/Authentication/AuthenticationService.cs` |
| `Application/Services/Users/IUserService.cs` |
| `Application/Services/Users/UserService.cs` |
| `Domain/Model/ApplicationUser.cs` |
| `Controllers/UserManagementController.cs` (legacy web controller) |
| `Views/UserManagement/` — 5 view files (Index, AddUser, EditUser, ChangeUserPassword, AddRolesToUser) |

**Total IAM switch effort**: **M** — most sub-steps are XS/trivial; the risk is in the `LoginController` swap (live auth path) and the `UseIamStore` flag flip.

**Gate test**: `dotnet build` must produce zero references to `IUserService`, `Application.Services.Authentication.IAuthenticationService`, `Domain.Model.ApplicationUser`.

---

## Priority 4 — Legacy View Cleanup (dead views)

> These are orphaned view files — their controllers are already removed. Pure file deletions.
> No logic risk; verify with `dotnet build` after each delete batch.

| Folder | Files | Size |
|--------|-------|------|
| `Views/Refund/` | `Index.cshtml`, `EditRefund.cshtml`, `ViewRefundDetails.cshtml` — 3 files | XS |
| `Views/Coupon/` | Cleanup pending (CouponController removed) | XS |
| `Views/CouponType/` | Cleanup pending | XS |
| `Views/CouponUsed/` | Cleanup pending | XS |

**Total**: XS — straight `git rm`, no code changes.

---

## Priority 5 — Payments Legacy Cleanup (Steps 5–6)

> Gate: at least one full payment window cycle confirmed in production.
> Full plan: `docs/roadmap/payments-atomic-switch.md`

| Step | File | Action | Size |
|------|------|--------|------|
| 5a | `Application/Services/Payments/PaymentHandler.cs` | Remove `CreatePayment()` and `HandlePaymentChangesOnOrder()` — or delete file if nothing remains | XS |
| 5b | `Application/DependencyInjection.cs` | Remove `IPaymentHandler` / `PaymentHandler` DI registration | XS |
| 6 | Inventory `PaymentWindowTimeoutJob` | Retire job + delete `PaymentWindowTimeoutJobTests.cs` (Inventory variant) | S |

**Total**: **S** — mostly deletes; Step 6 needs a Hangfire de-registration call.

---

## Priority 6 — R-3 · `PaymentsController.Create` type fix — **S**

> Must be done **before** the Sales atomic switch.

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/Sales/Controllers/PaymentsController.cs` |
| **Defect** | `Create(int id)` uses `id` as orderId — but the target route was `GET /Sales/Payments/Create/{paymentId:guid}` with `Guid paymentId`. Also lacks a Pending-status guard (creating payment for an already-confirmed order should be rejected). |
| **Fix** | Change action to accept the correct identifier; call `IPaymentService` to look up pending payment by order/user; redirect to existing payment if already confirmed. Align with actual payment creation flow. |
| **Effort** | S — review `IPaymentService` contract, update action signature + view model + `Create.cshtml` |
| **Test** | Unit test: non-Pending order → redirect or error; happy path → payment form rendered |

---

## Priority 6 (cont.) — Sales Atomic Switch — Step 8

> Gate: R-3, R-4, R-5 fixed; Payments Step 5 done; Orders + Payments running stably.
> Full plans: `docs/roadmap/orders-atomic-switch.md` Step 8, `docs/roadmap/payments-atomic-switch.md` Step 5.

**This is the largest single step — batch it into one PR.**

### Application layer — DI removals (XS)

| File | Action |
|---|---|
| `Application/DependencyInjection.cs` | Remove legacy `OrderService` + `OrderItemService` registrations (`Application.Services.Orders`) |
| `Infrastructure/DependencyInjection.cs` | Remove legacy `OrderRepository` + `OrderItemRepository` registrations (`Infrastructure.Repositories`) |

### Legacy service / repo file deletes (XS)

| File |
|---|
| `Application/Services/Orders/OrderService.cs` |
| `Application/Services/Orders/OrderItemService.cs` |
| `Infrastructure/Repositories/OrderRepository.cs` |
| `Infrastructure/Repositories/OrderItemRepository.cs` |

### Legacy web controller deletes (XS)

| File |
|---|
| `Web/Controllers/OrderController.cs` |
| `Web/Controllers/OrderItemController.cs` |
| `Web/Controllers/PaymentController.cs` |

### Legacy view deletes — **15 + 3 + ~5 files** (XS — git rm)

| Folder | Count |
|--------|-------|
| `Web/Views/Order/` | 15 views |
| `Web/Views/OrderItem/` | 3 views |
| `Web/Views/Payment/` | ~5 views |

### Area controller cleanup (XS)

| File | Action |
|---|---|
| `Web/Areas/Sales/Controllers/OrderItemsController.cs` | Remove `ByItem(int id)` action — view was intentionally dropped per ADR-0024 |

**Total Sales switch effort**: **L** — large file count but almost all deletes. Main risk is catching any remaining references with `dotnet build` / `dotnet test`.

**Verification checklist:**
- `dotnet build` green — no references to `Application.Services.Orders`, `Infrastructure.Repositories.OrderRepository`, `Infrastructure.Repositories.OrderItemRepository`
- `dotnet test` — full suite green
- Update `bounded-context-map.md` — move Sales/Orders, Sales/Payments, Sales/OrderItems to Completed

---

## Priority 7 — Feature Gaps & Cross-BC Coupling

### R-2 · `RefundController.Report` action missing — **S**

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/Sales/Controllers/RefundController.cs` (new action) + `Views/Refund/Report.cshtml` (new view) |
| **Decision needed** | What does the Report view show? (admin refund summary, date-range aggregation?) — clarify with product owner first |
| **Effort** | S — once requirements are clear; follow `Index` + `RefundVm` pattern |

---

### R-7 · `ShowItemConnectedWithTags` feature gap — **S (or drop)**

| | |
|---|---|
| **Legacy route** | `/Item/ShowItemConnectedWithTags` — grouped product listing by tag |
| **Current state** | No equivalent in `Areas/Catalog/Controllers/ProductController.cs` |
| **Decision** | Drop: tag filtering is now on the public `Index` (searchString); the grouped view was admin-only and low-traffic. **Recommended: drop and document.** If kept: S effort — reuse `IProductService.GetProductsByTagAsync` if it exists, or add it. |

---

### R-8 · `ImageController` legacy service dependency — **S**

| | |
|---|---|
| **File** | `ECommerceApp.Web/Areas/Catalog/Controllers/ImageController.cs` |
| **Defect** | Still injects `IImageService` from `Application.Services.Items` (legacy namespace) instead of the Catalog BC image service |
| **Fix** | Identify or create `Application.Catalog.Images.Services.IImageService`; swap injection and update action method calls |
| **Effort** | S — if new Catalog image service already exists, it's a 1-line injection swap. If it must be created, M. |

---

### R-11 · `web-ui-views-report.md` stale doc entries — **XS**

| Stale entry | Correction |
|---|---|
| `ProfileController` listed as `UserProfileController` | Actual name is `ProfileController` |
| Catalog, IAM, Jobs, Currencies, Inventory sections show old legacy routes | Update to show new Area routes (or mark as superseded by `bc-migration-status.md`) |

---

## Dependency Graph

```
R-6, R-1, R-4, R-5  ─────────────────────────────────────────► fix immediately (no deps)
                                                                     │
Refresh Token Steps 5–7 ─► IAM Atomic Switch ──────────────────────►┤
   (needs migration approval)                                        │
                                                                     │
R-3 ──────────────────────────────────────────────────────────────►  Sales Atomic Switch (Step 8)
R-4 + R-5 (already above) ─────────────────────────────────────────► (needs R-3/R-4/R-5 clear)
Payments Step 5+6 ─────────────────────────────────────────────────►┘

Legacy view cleanup ──► anytime after Refund/Coupon controllers confirmed gone
R-2, R-7, R-8 ────────► independent, can be done in any order
```

---

## Recommended Sprint Allocation

| Sprint | Items | Expected outcome |
|--------|-------|-----------------|
| **Sprint A** (½ day) | R-6, R-1, R-4, R-5 | All security defects closed; one PR |
| **Sprint B** (1 day) | Refresh Token Steps 5–7 + R-3 + R-8 | IAM feature complete; PaymentsController.Create correct; Catalog decoupled |
| **Sprint C** (½ day) | IAM Atomic Switch (after migration approvals) | IAM ✅ DONE; legacy files gone |
| **Sprint D** (½ day) | Legacy view cleanup + Payments Step 5+6 | Dead code removed; PaymentHandler gone |
| **Sprint E** (½ day) | Sales Atomic Switch Step 8 | Sales/Orders + Sales/Payments + Sales/OrderItems ✅ DONE |
| **Backlog** | R-2 (Refund Report), R-7 (tag-grouped listing), R-11 (doc fix) | Low risk, anytime |
