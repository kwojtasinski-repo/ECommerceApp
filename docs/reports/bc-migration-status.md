# BC Migration Status Report

> **What has been Area-switched, what is still legacy, and where routes deviate from `web-ui-views-report.md`.**
>
> Last updated: 2026-05-28 (rev 5 — IAM switch complete; Sales/Orders + Payments + OrderItems switched; 30+ legacy service/repo/interface files deleted; 7 V2\* Web controllers deleted; V2 API namespace cleared; `Context` → `DbContext`; `Domain.Model.ApplicationUser` deleted; 1020 tests ✅)
> Routing template (Startup.cs): `{area:exists}/{controller}/{action=Index}/{id?}` and `{controller}/{action=Index}/{id?}`
> ⚠️ **Key implication**: only a parameter literally named `id` binds to the `{id?}` path segment. Any other name (e.g. `orderId`, `jobName`) falls back to **query string**.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Fully switched — Area controller live, no legacy controller, no legacy views |
| ⚠️ | In progress — Area controller live but legacy controller and/or views still exist |
| 🐚 | Area shell only — `_ViewStart` / `_ViewImports` created, no Area controller yet |
| ❌ | Not started — still 100 % legacy |
| 🆕 | New feature (no legacy equivalent) |

---

## BC Migration Status Overview

| BC | Status | Area Controller | Legacy Controller still alive? | Legacy Views still alive? |
|----|--------|----------------|-------------------------------|--------------------------|
| **Presale / Checkout** | ✅ DONE | `Areas/Presale/Controllers/CheckoutController.cs` | No | No |
| **AccountProfile / Profile** | ✅ DONE | `Areas/AccountProfile/Controllers/ProfileController.cs` | No | No — `Views/Customer/`, `Views/Address/`, `Views/ContactDetail/` all gone |
| **Sales / Coupon** | ✅ DONE | `Areas/Sales/Controllers/CouponController.cs` | No | ⚠️ `Views/Coupon/` (4), `Views/CouponType/` (4), `Views/CouponUsed/` (4) — orphaned, no controller |
| **Sales / Shipment** | 🆕 NEW | `Areas/Sales/Controllers/ShipmentController.cs` | N/A | N/A |
| **Catalog / Product** | ✅ DONE | `Areas/Catalog/Controllers/ProductController.cs` | No — `ItemController` removed | No — `Views/Item/` removed |
| **Catalog / Tag** | ✅ DONE | `Areas/Catalog/Controllers/TagController.cs` | No — `TagController` removed | No — `Views/Tag/` removed |
| **Catalog / Category** | 🆕 NEW | `Areas/Catalog/Controllers/CategoryController.cs` | N/A — new concept | N/A |
| **Catalog / Image** | ⚠️ IN PROGRESS | `Areas/Catalog/Controllers/ImageController.cs` | No — moved to Area | No — but still injects legacy `IImageService` from `Application.Services.Items` ⚠️ |
| **Sales / Refund** | ⚠️ IN PROGRESS | `Areas/Sales/Controllers/RefundController.cs` | No — `Controllers/RefundController.cs` removed ✅ | Yes — `Views/Refund/Index.cshtml`, `Views/Refund/EditRefund.cshtml`, `Views/Refund/ViewRefundDetails.cshtml` still exist |
| **Sales / Orders** | ✅ DONE | `Areas/Sales/Controllers/OrdersController.cs` | No — `Controllers/OrderController.cs` deleted ✅ | No — `Views/Order/` (17 views) deleted ✅ |
| **Sales / Payments** | ✅ DONE | `Areas/Sales/Controllers/PaymentsController.cs` | No — `Controllers/PaymentController.cs` deleted ✅ | No — `Views/Payment/` (5 views) deleted ✅ |
| **Sales / OrderItems** | ✅ DONE | `Areas/Sales/Controllers/OrderItemsController.cs` | No — `Controllers/OrderItemController.cs` deleted ✅ | No — `Views/OrderItem/` (3 views) deleted ✅ |
| **Inventory** | ✅ DONE | `Areas/Inventory/Controllers/StockController.cs` | No — `Controllers/InventoryController.cs` removed ✅ | No — `Views/Inventory/` removed ✅ |
| **Currencies** | ✅ DONE | `Areas/Currencies/Controllers/CurrencyController.cs` | No — `Controllers/CurrencyController.cs` removed ✅ | No — `Views/Currency/` removed ✅ |
| **IAM / UserManagement** | ✅ DONE | `Areas/IAM/Controllers/UserManagementController.cs` | No — `Controllers/UserManagementController.cs` deleted ✅ | No — `Views/UserManagement/` (5 views) deleted ✅ |
| **Jobs** | ✅ DONE | `Areas/Jobs/Controllers/JobManagementController.cs` | No — `Controllers/JobManagementController.cs` removed ✅ | No — `Views/JobManagement/` removed ✅ |
| **Brand** | ⚠️ LEGACY ONLY | No BC equivalent — `Controllers/BrandController.cs` active | — (no switch planned yet) | `Views/Brand/` (4 views) — active legacy |

---

## Orphaned View Folders (no controller — safe to delete)

These root `Views/` folders have no matching controller. All associated controllers were either moved to Areas or deleted. Pure dead code — no logic risk.

| Folder | Files | Reason orphaned |
|---|---|---|
| `Views/V2Category/` | Add, Edit, Index (3) | `V2CategoryController` deleted in Session 2 |
| `Views/V2Currency/` | Add, Details, Edit, Index (4) | `V2CurrencyController` deleted |
| `Views/V2Product/` | Add, Details, Edit (3) | `V2ProductController` deleted |
| `Views/V2Profile/` | Add, Details, Edit, Index (4) | `V2ProfileController` deleted |
| `Views/V2Tag/` | Add, Edit (2) | `V2TagController` deleted |
| `Views/V2User/` | Add, Details, Edit, Index (4) | `V2UserController` deleted |
| `Views/V2Job/` | DeferredQueue, History, Index, Register, ScheduleDeferred (5) | `V2JobController` deleted |
| `Views/Coupon/` | AddCoupon, EditCoupon, Index, ViewCoupon (4) | `CouponController` moved to `Areas/Sales` |
| `Views/CouponType/` | AddCouponType, EditCouponType, Index, ViewCouponType (4) | No replacement |
| `Views/CouponUsed/` | AddCouponUsed, EditCouponUsed, Index, ViewCouponUsed (4) | No replacement |
| `Views/Refund/` | EditRefund, Index, ViewRefundDetails (3) | `RefundController` moved to `Areas/Sales` |

**Total**: ~38 files. `dotnet build` confirms no references — safe to `git rm` in one batch.

---

## Route Audit — Areas already switched vs `web-ui-views-report.md`

### Presale / Checkout ✅

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Presale/Checkout/Cart` | `Cart()` | ✅ |
| `GET /Presale/Checkout/PlaceOrder` | `PlaceOrder()` | ✅ |
| `GET /Presale/Checkout/Summary` | `Summary(int id)` — `id` matches `{id?}` | ✅ |

---

### Catalog / Product ⚠️

> **Legacy routes** were `/Item/...`. New Area routes are `/Catalog/Product/...`. Legacy `ItemController` removed.

| Legacy → New route | Actual action signature | Binds correctly? |
|---|---|---|
| `/Item` → `GET /Catalog/Product` | `Index(string? searchString)` — public | ✅ |
| `/Item/ViewItem/{id}` → `GET /Catalog/Product/Details/{id}` | `Details(int id)` | ✅ |
| `/Item/AddItem` → `GET /Catalog/Product/Create` | `Create()` — Maint only | ✅ |
| `/Item/EditItem/{id}` → `GET /Catalog/Product/Edit/{id}` | `Edit(int id)` — Maint only | ✅ |
| `/Item/ShowItemConnectedWithTags` → *(dropped)* | **No equivalent action** | ⚠️ **Feature gap** — no grouped-by-tag listing in new Area |
| `/Item/ShowItemBrands` → *(deleted per ADR-0007)* | Correctly absent | ✅ |
| *(new)* `POST /Catalog/Product/Publish/{id}` | `Publish(int id)` | ✅ |
| *(new)* `POST /Catalog/Product/Unpublish/{id}` | `Unpublish(int id)` | ✅ |
| *(new)* `GET /Catalog/Product/All` | `All()` — Maint only (admin product list) | ✅ |

---

### Catalog / Tag ⚠️

> **Legacy routes** were `/Tag/...`. New Area routes are `/Catalog/Tag/...`. Legacy `TagController` removed.

| Legacy → New route | Actual action signature | Binds correctly? |
|---|---|---|
| `/Tag` → `GET /Catalog/Tag` | `Index()` — Maint only | ✅ |
| `/Tag/AddTag` → `GET /Catalog/Tag/Create` | `Create()` | ✅ |
| `/Tag/EditTag/{id}` → `GET /Catalog/Tag/Edit/{id}` | `Edit(int id)` | ✅ |
| `/Tag/ViewTag/{id}` → *(dropped)* | **No `Details` action** | ⚠️ **Feature gap** — tag detail view removed; info now inline on Index |

---

### Catalog / Category 🆕

> New concept — no legacy equivalent.

| Route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Catalog/Category` | `Index()` — Maint only | ✅ |
| `GET /Catalog/Category/Create` | `Create()` | ✅ |
| `GET /Catalog/Category/Edit/{id}` | `Edit(int id)` | ✅ |

---

### Catalog / Image ⚠️

| Route | Actual action signature | Notes |
|---|---|---|
| `POST /Catalog/Image/UploadImages` | `UploadImages(int itemId, ...)` — `itemId` from form body | ✅ route (POST, form bind) |
| `DELETE /Catalog/Image/DeleteImage/{id}` | `DeleteImage(int id)` | ✅ |
| — | Injects `IImageService` from `Application.Services.Items` | ⚠️ **Legacy service dependency** — still coupled to old namespace, not the new Catalog BC service |

---

### Jobs / JobManagement ✅

| Route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Jobs/JobManagement` | `Index()` — admin only | ✅ |
| `GET /Jobs/JobManagement/History` | `History(string jobName, int page = 1)` — `jobName` and `page` via query string | ✅ route — query-string bind is correct (non-`id` params) |
| `POST /Jobs/JobManagement/Trigger` | `Trigger(string jobName)` — `[ValidateAntiForgeryToken]`, `jobName` from form | ✅ |
| `POST /Jobs/JobManagement/Enable` | `Enable(string jobName)` — `[ValidateAntiForgeryToken]` | ✅ |
| `POST /Jobs/JobManagement/Disable` | `Disable(string jobName)` — `[ValidateAntiForgeryToken]` | ✅ |

---

### Sales / Orders ✅

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/Orders` | `Index()` | ✅ |
| `GET /Sales/Orders/MyOrders` | `MyOrders()` | ✅ |
| `GET /Sales/Orders/Details/{id}` | `Details(int id)` — `id` matches `{id?}` | ✅ — ownership check present (R-5 fixed 2026-05-28) |
| `GET /Sales/Orders/Edit/{id}` | `Edit(int id)` | ✅ |
| `GET /Sales/Orders/PaidOrders` | `PaidOrders()` | ✅ |
| `GET /Sales/Orders/Fulfillment/{id}` | `Fulfillment(int id)` | ✅ |
| *(not in report)* | `ByCustomer(int id)` | Extra action, documented in ADR-0024 |

---

### Sales / Payments ✅

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/Payments` | `Index()` | ✅ |
| `GET /Sales/Payments/Create/{id}` | `Create(int id)` — calls `GetPendingByOrderIdAsync(id, GetUserId())` | ✅ — user-scope + Pending-status guard (R-3 fixed 2026-05-28) |
| `GET /Sales/Payments/Details/{id}` | `Details(int id)` — `id` matches `{id?}` | ✅ — ownership check present (R-4 fixed 2026-05-28) |
| `GET /Sales/Payments/MyPayments` | `MyPayments()` | ✅ |

---

### Sales / OrderItems ✅

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/OrderItems` | `Index()` | ✅ |
| `GET /Sales/OrderItems/Details/{id}` | `Details(int id)` | ✅ |

---

### Sales / Refund ⚠️

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/Refund` | `Index()` | ✅ |
| `GET /Sales/Refund/Edit/{id}` | `Edit(int id)` — `id` matches `{id?}` | ✅ |
| `GET /Sales/Refund/View/{id}` | `View(int id)` — `id` matches `{id?}` | ✅ |
| `GET /Sales/Refund/MyRefunds` | `MyRefunds()` | ✅ |
| `GET /Sales/Refund/Request/{orderId}` | `Request(int orderId)` — **parameter named `orderId` not `id`** | ❌ **Route mismatch** — `orderId` does NOT bind to `{id?}` path segment; actual route resolves to `GET /Sales/Refund/Request?orderId={x}` (query string) |
| `GET /Sales/Refund/Report` | **MISSING** — no action exists | ❌ **Not implemented** |

**Approve / Reject**: `POST /Sales/Refund/Approve/{id}` and `POST /Sales/Refund/Reject/{id}` — both present with `int id` (matches `{id?}`). ✅ The Edit view needs buttons wired to these (open issue #5 from report).

---

### Sales / Coupon ✅

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/Coupon` | `Index()` | ✅ |
| `GET /Sales/Coupon/Create` | `Create()` | ✅ |
| `GET /Sales/Coupon/Edit/{id}` | `Edit(int id)` — `id` matches `{id?}` | ✅ |
| `GET /Sales/Coupon/Details/{id}` | `Details(int id)` — `id` matches `{id?}` | ✅ |

---

### AccountProfile / Profile ✅

> **Note**: `web-ui-views-report.md` lists the target controller as `UserProfileController` — actual is `ProfileController`. Routes resolve to `/AccountProfile/Profile/...`. The report needs a doc correction.

| Actual route | Action signature | Binds correctly? |
|---|---|---|
| `GET /AccountProfile/Profile` | `Index()` | ✅ |
| `GET /AccountProfile/Profile/Details/{id}` | `Details(int id)` — scope check present | ✅ |
| `GET /AccountProfile/Profile/Create` | `Create()` | ✅ |
| `GET /AccountProfile/Profile/Edit/{id}` | `Edit(int id)` | ✅ |
| `GET /AccountProfile/Profile/EditContactInfo/{id}` | `EditContactInfo(int id)` | ✅ |
| `GET /AccountProfile/Profile/AddAddress` | `AddAddress(int userProfileId)` — `userProfileId` ≠ `id` | ⚠️ **Query-string bind** — route is `...?userProfileId={x}`. Functionally works. |
| `GET /AccountProfile/Profile/EditAddress` | `EditAddress(int userProfileId, int addressId)` | ⚠️ **Query-string bind** — both params via query string. Functionally works. |
| `GET /AccountProfile/Profile/All` | `All()` — Maint-only | ✅ |

---

### Jobs ✅

> **Legacy routes** were `/JobManagement/...`. New Area routes are `/Jobs/JobManagement/...`. Legacy controller removed.

| Legacy → New route | Actual action signature | Binds correctly? |
|---|---|---|
| `/JobManagement` → `GET /Jobs/JobManagement` | `Index()` | ✅ |
| `/JobManagement/History/{name}` → `GET /Jobs/JobManagement/History` | `History(string jobName, int page)` — `jobName` ≠ `id` | ⚠️ **Query-string bind** — `jobName` resolves to `?jobName=x`. Functionally correct (named strings are unnatural in path). |
| *(new)* `POST /Jobs/JobManagement/Trigger` | `Trigger(string jobName)` — from form | ✅ |
| *(new)* `POST /Jobs/JobManagement/Enable` | `Enable(string jobName)` — from form | ✅ |
| *(new)* `POST /Jobs/JobManagement/Disable` | `Disable(string jobName)` — from form | ✅ |

---

### Currencies ✅

> **Legacy routes** were `/Currency/...`. New Area routes are `/Currencies/Currency/...`. Legacy controller removed.

| Legacy → New route | Actual action signature | Binds correctly? |
|---|---|---|
| `/Currency` → `GET /Currencies/Currency` | `Index()` | ✅ |
| `/Currency/AddCurrency` → `GET /Currencies/Currency/Create` | `Create()` | ✅ |
| `/Currency/EditCurrency/{id}` → `GET /Currencies/Currency/Edit/{id}` | `Edit(int id)` | ✅ |
| `/Currency/ViewCurrency/{id}` → `GET /Currencies/Currency/Details/{id}` | `Details(int id)` | ✅ |

---

### Inventory ✅

> **Legacy routes** were `/Inventory/...` served by `InventoryController`. New routes are `/Inventory/Stock/...` served by `StockController`. **Route shape changed** — the segment after `/Inventory/` is now `Stock`, not the action name directly.

| Legacy → New route | Actual action signature | Binds correctly? |
|---|---|---|
| `/Inventory` → `GET /Inventory/Stock` | `Index(int page, int pageSize)` | ✅ — params from query string (no `id` involved) |
| `/Inventory/Reservations` → `GET /Inventory/Stock/Reservations` | `Reservations(int page, int pageSize, string status)` | ✅ |
| `/Inventory/Audit` → `GET /Inventory/Stock/Audit` | `Audit(int page, int pageSize)` | ✅ |
| `/Inventory/AdjustStock` → `GET /Inventory/Stock/AdjustStock` | `AdjustStock()` | ✅ |
| `/Inventory/PendingAdjustments` → `GET /Inventory/Stock/PendingAdjustments` | `PendingAdjustments(CancellationToken)` | ✅ |
| *(new)* `POST /Inventory/Stock/Adjust` | `Adjust(int productId, int newQuantity)` — Maint | ✅ |
| *(new)* `POST /Inventory/Stock/Release` | `Release(int orderId, int productId, int quantity)` — Maint | ✅ |

---

### IAM / UserManagement ✅

> **Legacy routes** were `/UserManagement/...`. New Area routes are `/IAM/UserManagement/...`. Legacy controller deleted ✅ — `Controllers/UserManagementController.cs` removed, `Views/UserManagement/` removed.

| Legacy → New route | Actual action signature | Binds correctly? |
|---|---|---|
| `/UserManagement` → `GET /IAM/UserManagement` | `Index()` | ✅ |
| `/UserManagement/AddRolesToUser/{id}` → `GET /IAM/UserManagement/AddRolesToUser/{id}` | `AddRolesToUser(string id)` — `id` matches `{id?}` | ✅ |
| `/UserManagement/EditUser/{id}` → `GET /IAM/UserManagement/EditUser/{id}` | `EditUser(string id)` | ✅ |
| `/UserManagement/AddUser` → `GET /IAM/UserManagement/AddUser` | `AddUser()` | ✅ |
| `/UserManagement/ChangeUserPassword/{id}` → `GET /IAM/UserManagement/ChangeUserPassword/{id}` | `ChangeUserPassword(string id)` | ✅ |
| `POST /IAM/UserManagement/DeleteUser` | `DeleteUser(string id)` — `[HttpPost]` + `[ValidateAntiForgeryToken]` | ✅ — R-6 fixed 2026-05-28 |

---

Route Defects

| # | Controller | Issue | Severity |
|---|---|---|---|
| R-1 | `RefundController.Request` | ~~`orderId` param doesn't bind to `{id?}` path segment~~ | ✅ Fixed 2026-05-28 — renamed to `id` |
| R-2 | `RefundController` | `Report` action missing entirely | 🟠 Missing feature |
| R-3 | `PaymentsController.Create` | ~~`int id` (orderId) instead of `Guid paymentId`; no Pending-status guard~~ | ✅ Fixed 2026-05-28 — `GetPendingByOrderIdAsync(id, GetUserId())` |
| R-4 | `PaymentsController.Details` | ~~No `payment.UserId ≠ caller` scope check~~ | ✅ Fixed 2026-05-28 — ownership check + `UserId` in `PaymentDetailsVm` |
| R-5 | `OrdersController.Details` | ~~No `order.UserId ≠ caller` scope check~~ | ✅ Fixed 2026-05-28 — maintenance-bypass ownership check |
| R-6 | `IAM/UserManagementController.DeleteUser` | ~~No `[HttpPost]`/`[HttpDelete]` attribute — GET-triggered delete, unsafe~~ | ✅ Fixed 2026-05-28 — `[HttpPost]` + `[ValidateAntiForgeryToken]` |
| R-7 | `ProductController` | `ShowItemConnectedWithTags` equivalent missing — no grouped-by-tag product listing in new Area | 🟠 Feature gap (decide: drop or implement?) |
| R-8 | `Catalog/ImageController` | Still injects `IImageService` from `Application.Services.Items` — legacy namespace, not new Catalog BC service | 🟠 Cross-BC coupling |
| R
| R-10 | `ProfileController.EditAddress` | Same as R-9 for both params | 🟡 Style |
| R-11 | `web-ui-views-report.md` | Multiple stale entries: `ProfileController` vs `UserProfileController`; Catalog/IAM/Jobs/Currencies/Inventory sections out of date | 📝 Doc fix needed |

---

## Suggested Migration Order

| Priority | Work item | Rationale |
|----------|-----------|-----------|
| 1 | **Fix R-6** (`DeleteUser` HTTP verb) | Fast fix, security issue in already-switched IAM Area |
| 2 | **Fix R-1, R-3, R-4, R-5** | Remaining 🔴 defects in already-switched BCs |
| 3 | **IAM atomic switch** | Delete `Controllers/UserManagementController.cs` + `Views/UserManagement/` — only thing blocking IAM from ✅ |
| 4 | **Fix R-7** (tag-by-product listing) and **R-8** (`ImageController` service swap) | Complete Catalog cleanly before Sales push |
| 5 | **Legacy view cleanup** | Delete `Views/Refund/` (3 files), `Views/Coupon/`, `Views/CouponType/`, `Views/CouponUsed/` — controllers are already gone, views are dead code |
| 6 | **Sales atomic switch** (Orders + Payments + OrderItems) | Delete `OrderController`, `OrderItemController`, `PaymentController` and all legacy `Views/Order/`, `Views/Payment/`, `Views/OrderItem/` — largest remaining step, requires R-3/R-4/R-5 clear first |
