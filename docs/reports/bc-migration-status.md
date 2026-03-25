# BC Migration Status Report

> **What has been Area-switched, what is still legacy, and where routes deviate from `web-ui-views-report.md`.**
>
> Last updated: 2026-05-27
> Routing template (Startup.cs): `{area:exists}/{controller}/{action=Index}/{id?}` and `{controller}/{action=Index}/{id?}`
> ⚠️ **Key implication**: only a parameter literally named `id` binds to the `{id?}` path segment. Any other name (e.g. `orderId`, `userProfileId`) falls back to **query string**.

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
| **AccountProfile / Profile** | ✅ DONE | `Areas/AccountProfile/Controllers/ProfileController.cs` | No (`CustomerController` + `AddressController` gone) | No (`Views/Customer/`, `Views/Address/`, `Views/ContactDetail/` gone) |
| **Sales / Coupon** | ✅ DONE | `Areas/Sales/Controllers/CouponController.cs` | No | Yes — `Views/Coupon/`, `Views/CouponType/`, `Views/CouponUsed/` still exist (cleanup pending) |
| **Sales / Shipment** | 🆕 NEW | `Areas/Sales/Controllers/ShipmentController.cs` | N/A | N/A |
| **Catalog / Product** | ⚠️ IN PROGRESS | `Areas/Catalog/Controllers/ProductController.cs` | No — `Controllers/ItemController.cs` **removed** ✅ | Yes — `Views/Item/` still exist (cleanup pending) |
| **Catalog / Tag** | ⚠️ IN PROGRESS | `Areas/Catalog/Controllers/TagController.cs` | No — `Controllers/TagController.cs` **removed** ✅ | Yes — `Views/Tag/` still exist (cleanup pending) |
| **Catalog / Category** | 🆕 NEW | `Areas/Catalog/Controllers/CategoryController.cs` | N/A — new concept (replaces legacy Brand/Type) | N/A |
| **Catalog / Image** | ⚠️ IN PROGRESS | `Areas/Catalog/Controllers/ImageController.cs` | No — moved to Area | No — but still injects legacy `IImageService` from `Application.Services.Items` ⚠️ |
| **Sales / Refund** | ⚠️ IN PROGRESS | `Areas/Sales/Controllers/RefundController.cs` | No — `Controllers/RefundController.cs` removed ✅ | Yes — `Views/Refund/Index.cshtml`, `Views/Refund/EditRefund.cshtml`, `Views/Refund/ViewRefundDetails.cshtml` still exist |
| **Sales / Orders** | ⚠️ IN PROGRESS | `Areas/Sales/Controllers/OrdersController.cs` | Yes — `Controllers/OrderController.cs` still live | Yes — `Views/Order/` (many views) still exist |
| **Sales / Payments** | ⚠️ IN PROGRESS | `Areas/Sales/Controllers/PaymentsController.cs` | Yes — `Controllers/PaymentController.cs` still live | Yes — `Views/Payment/` still exist |
| **Sales / OrderItems** | ⚠️ IN PROGRESS | `Areas/Sales/Controllers/OrderItemsController.cs` | Yes — `Controllers/OrderItemController.cs` still live | Yes — `Views/OrderItem/` still exists |
| **Inventory** | ❌ NOT STARTED | None | Yes — `Controllers/InventoryController.cs` still live | Yes — `Views/Inventory/` still exist |
| **Currencies** | ❌ NOT STARTED | None | Yes — `Controllers/CurrencyController.cs` still live | Yes — `Views/Currency/` still exist |
| **IAM / UserManagement** | ❌ NOT STARTED | None | Yes — `Controllers/UserManagementController.cs` still live | Yes — `Views/UserManagement/` still exist |
| **Jobs** | ❌ NOT STARTED | None | Yes — `Controllers/JobManagementController.cs` still live | Yes — `Views/JobManagement/` still exist |

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

### Sales / Orders ⚠️

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/Orders` | `Index()` | ✅ |
| `GET /Sales/Orders/MyOrders` | `MyOrders()` | ✅ |
| `GET /Sales/Orders/Details/{id}` | `Details(int id)` — `id` matches `{id?}` | ✅ route — ⚠️ **no `UserId` scope check** (security issue #1 from report) |
| `GET /Sales/Orders/Edit/{id}` | `Edit(int id)` | ✅ |
| `GET /Sales/Orders/PaidOrders` | `PaidOrders()` | ✅ |
| `GET /Sales/Orders/Fulfillment/{id}` | `Fulfillment(int id)` | ✅ |
| *(not in report)* | `ByCustomer(int id)` | Extra action, documented in ADR-0024 |

---

### Sales / Payments ⚠️

| Report route | Actual action signature | Binds correctly? |
|---|---|---|
| `GET /Sales/Payments` | `Index()` (stub — empty list) | ✅ route — ⚠️ stub |
| `GET /Sales/Payments/Create/{paymentId:guid}` *(target)* | `Create(int id)` — `id` = orderId | ⚠️ **Type wrong**: `int id` (orderId) instead of `Guid paymentId`; no Pending-status guard — known issue #R-3 |
| `GET /Sales/Payments/Details/{id}` | `Details(int id)` — `id` matches `{id?}` | ✅ route — ⚠️ **no `UserId` scope check** (security issue #R-4) |
| `GET /Sales/Payments/MyPayments` | `MyPayments()` (stub — empty list) | ✅ route — ⚠️ stub |

---

### Sales / OrderItems ⚠️

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

## Summary of Route Defects

| # | Controller | Issue | Severity |
|---|---|---|---|
| R-1 | `RefundController.Request` | `orderId` param doesn't bind to `{id?}` path segment — route resolves to query string `?orderId=x` instead of `/{orderId}` | 🔴 Breaks links that assume path-segment URL |
| R-2 | `RefundController` | `Report` action missing entirely | 🟠 Missing feature |
| R-3 | `PaymentsController.Create` | `int id` (orderId) instead of `Guid paymentId`; no Pending-status guard | 🔴 Must fix before atomic switch |
| R-4 | `PaymentsController.Details` | No `payment.UserId ≠ caller` scope check | 🔴 Security |
| R-5 | `OrdersController.Details` | No `order.UserId ≠ caller` scope check | 🔴 Security |
| R-6 | `ProductController` | `ShowItemConnectedWithTags` equivalent missing — no grouped-by-tag product listing in new Area | 🟠 Feature gap (decide: drop or implement?) |
| R-7 | `Catalog/ImageController` | Still injects `IImageService` from `Application.Services.Items` — legacy namespace, not new Catalog BC service | 🟠 Cross-BC coupling |
| R-8 | `ProfileController.AddAddress` | `userProfileId` falls back to query string — inconsistent with path-segment convention | 🟡 Style |
| R-9 | `ProfileController.EditAddress` | Same as R-8 for both params | 🟡 Style |
| R-10 | `web-ui-views-report.md` | Controller named `ProfileController` not `UserProfileController`; Catalog section still shows as SHELL ONLY | 📝 Doc fix needed |

---

## Suggested Migration Order

| Priority | BC | Rationale |
|----------|----|-----------|
| 1 | **Fix R-1, R-3, R-4, R-5** | Security and broken-route defects in already-switched BCs — resolve before driving more traffic to Area controllers |
| 2 | **Fix R-6, R-7** | Catalog feature gap and cross-BC coupling — complete Catalog switch cleanly |
| 3 | **Catalog legacy view cleanup** | Delete `Views/Item/` and `Views/Tag/` — controllers already gone, views are orphaned |
| 4 | **Coupon legacy view cleanup** | Delete `Views/Coupon/`, `Views/CouponType/`, `Views/CouponUsed/` |
| 5 | **Inventory** | Backoffice-only, isolated, no user-facing coupling — low risk |
| 6 | **Currencies / Jobs / IAM** | Pure backoffice, no cross-BC deps — can be done in parallel |
| 7 | **Sales atomic switch** (Orders + Payments + OrderItems) | Delete `Controllers/OrderController.cs`, `Controllers/OrderItemController.cs`, `Controllers/PaymentController.cs` and all legacy `Views/Order/`, `Views/Payment/`, `Views/OrderItem/` — largest step, requires R-3/R-4/R-5 resolved first |
| 8 | **Refund / Shipment cleanup** | Delete legacy `Views/Refund/` after confirming no nav links reference old routes |
