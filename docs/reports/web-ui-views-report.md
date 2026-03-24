# Web UI Views Report

> **Current-state inventory** of every `.cshtml` view file in the Web project, organised by bounded context.
> Covers both legacy (non-Area) controllers and the new Area-based BC controllers.
> This is a living reference — update when views are added, moved, or deleted during atomic switches.
>
> Last updated: 2026-03-27

---

## Legend

| Symbol | Meaning |
|--------|---------|
| 🌐 | **Public** — no authentication required |
| 👤 | **User-facing** — any authenticated user; operates on own data only (`userId` from session) |
| 🔐 | **Backoffice** — `[Maint]` role only (Administrator / Manager / Service) |
| ❌ | **Deleted at atomic switch** — view exists today, removed in target state |
| ✅ | Exists and correct |
| ⚠️ | Exists but has a known issue |
| 🆕 | Does **not** exist yet — required by target state |

---

## Summary

| BC | 🌐 Public | 👤 User | 🔐 Backoffice | Issues |
|----|-----------|---------|---------------|--------|
| Presale / Checkout | 0 | 3 | 0 | — |
| Sales / Orders | 0 | 2 | 4 | ⚠️ Details missing scope |
| Sales / Payments | 0 | 3 | 1 | ⚠️ Create route wrong, Details missing scope |
| Sales / OrderItems | 0 | 0 | 2 | — |
| Fulfillment / Refunds | 0 | 3 | 2 | ⚠️ customer scope, 🆕 Approve/Reject/Report, 🆕 Request form |
| AccountProfile / Profile | 0 | 5 | 1 | ⚠️ Edit/View scope unclear |
| AccountProfile / Address | 0 | 3 | 0 | — |
| Catalog / Item (legacy) | 3 | 0 | 3 | ShowItemBrands ❌ deleted |
| Catalog / Tag (legacy) | 0 | 0 | 4 | — |
| Inventory | 0 | 0 | 5 | — |
| Coupons | 0 | 0 | 4 (+ 8 ❌ deleted) | CouponType + CouponUsed deleted |
| Currencies | 0 | 0 | 4 | — |
| IAM / UserManagement | 0 | 0 | 5 | — |
| Jobs | 0 | 0 | 2 | — |
| **Legacy Payment** | 0 | 0 | 5 ❌ | All replaced by Sales/Payments |
| **ContactDetail** | 0 | 0 | 3 ❌ | Replaced by inline Email/Phone on UserProfile |

---

## Presale BC — Checkout

**Controller:** `Areas/Presale/Controllers/CheckoutController.cs`
**View path:** `Areas/Presale/Views/Checkout/`
**No backoffice views.** Cart and order placement are customer-only flows.

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Cart.cshtml` | 👤 | `GET /Presale/Checkout/Cart` | ✅ | Item list, quantities, running total |
| `PlaceOrder.cshtml` | 👤 | `GET /Presale/Checkout/PlaceOrder` | ✅ | Checkout form — address autofill, coupon field |
| `Summary.cshtml` | 👤 | `GET /Presale/Checkout/Summary` | ✅ | Order confirmation — order number, link to pay |

---

## Sales BC — Orders

**Controller:** `Areas/Sales/Controllers/OrdersController.cs`
**View path:** `Areas/Sales/Views/Orders/`

### User-facing

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `MyOrders.cshtml` | 👤 | `GET /Sales/Orders/MyOrders` | ✅ | Own orders list — scoped by `userId` from session |
| `Details.cshtml` | 👤 | `GET /Sales/Orders/Details/{id}` | ⚠️ | **Missing scope check** — any authenticated user can view any order. Must return `403` if `order.UserId ≠ caller`. |

### Backoffice-only

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Sales/Orders` | ✅ | All orders — paginated, searchable |
| `Edit.cshtml` | 🔐 | `GET /Sales/Orders/Edit/{id}` | ✅ | Edit order form |
| `PaidOrders.cshtml` | 🔐 | `GET /Sales/Orders/PaidOrders` | ✅ | Paid orders list — **Dispatch button** per row (POST action, no separate view) |
| `Fulfillment.cshtml` | 🔐 | `GET /Sales/Orders/Fulfillment/{id}` | ✅ | Single-order fulfillment detail — inspect items and status before/after dispatch |

---

## Sales BC — Payments

**Controller:** `Areas/Sales/Controllers/PaymentsController.cs`
**View path:** `Areas/Sales/Views/Payments/`

### User-facing

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Create.cshtml` | 👤 | `GET /Sales/Payments/Create/{paymentId:guid}` | ⚠️ | **Route wrong** — controller currently takes `int id` (orderId). Must change to `Guid paymentId`, look up via `GetByTokenAsync(guid, userId)`, return `403` if `Status ≠ Pending`. |
| `MyPayments.cshtml` | 👤 | `GET /Sales/Payments/MyPayments` | ⚠️ | Stub — page exists but not yet fully implemented. |
| `Details.cshtml` | 👤 | `GET /Sales/Payments/Details/{id}` | ⚠️ | **Missing scope check** — must return `403` if `payment.UserId ≠ caller`. |

### Backoffice-only

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Sales/Payments` | ✅ | All payments — paginated, searchable |

---

## Sales BC — Order Items

**Controller:** `Areas/Sales/Controllers/OrderItemsController.cs`
**View path:** `Areas/Sales/Views/OrderItems/`
**No user-facing views** — order items are shown inline on the order detail view.

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Sales/OrderItems` | ✅ | All order items — admin list |
| `Details.cshtml` | 🔐 | `GET /Sales/OrderItems/Details/{id}` | ✅ | Single order-item detail |

---

## Fulfillment BC — Refunds

**Controller (legacy):** `Controllers/RefundController.cs`
**View path:** `Views/Refund/`
**Target:** `Areas/Sales/Controllers/RefundController.cs` + `Areas/Sales/Views/Refund/`

### User-facing

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| 🆕 `Request.cshtml` | 👤 | `GET /Sales/Refund/Request/{orderId}` | 🆕 | **Does not exist.** Refund request form — reason field, items to refund. Own-scoped: validates `order.UserId == caller`. Linked from the Order Details page. |
| `ViewRefundDetails.cshtml` | 👤 | `GET /Refund/ViewRefundDetails/{id}` | ⚠️ | **Missing scope check** — must return `403` if `refund.UserId ≠ caller`. Target route: `GET /Sales/Refund/View/{id}`. |

### Backoffice-only

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Refund` | ✅ | All refunds — paginated, searchable |
| `EditRefund.cshtml` | 🔐 | `GET /Refund/EditRefund/{id}` | ⚠️ | Edit form exists, but **Approve and Reject buttons are missing** — these must be added as distinct POST actions (`/Refund/Approve/{id}`, `/Refund/Reject/{id}`). |

### Missing — not yet implemented

| View file | Audience | Route | Notes |
|-----------|----------|-------|-------|
| 🆕 `Report.cshtml` | 🔐 | `GET /Sales/Refund/Report` | Admin report — total requested / approved / rejected counts, amounts, date-range filter |

---

## AccountProfile BC — User Profile

**Controller (legacy):** `Controllers/CustomerController.cs`
**View path:** `Views/Customer/`
**Target:** `Areas/AccountProfile/Controllers/UserProfileController.cs`

### User-facing

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 👤 | `GET /Customer` | ✅ | Own profile (scoped by `userId`) |
| `AddCustomer.cshtml` | 👤 | `GET /Customer/AddCustomer` | ✅ | Create profile form |
| `AddCustomerPartialView.cshtml` | 👤 | `GET /Customer/AddCustomerPartialView` | ✅ | Partial — inline profile creation used in checkout flow |
| `EditCustomer.cshtml` | 👤 | `GET /Customer/EditCustomer/{id}` | ⚠️ | Scope of `id` relative to caller is not enforced in controller |
| `ViewCustomer.cshtml` | 👤 | `GET /Customer/ViewCustomer/{id}` | ⚠️ | Same scope concern |

### Backoffice-only

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `All.cshtml` | 🔐 | `GET /Customer/All` | ✅ | All customer profiles — paginated |

---

## AccountProfile BC — Address

**Controller (legacy):** `Controllers/AddressController.cs`
**View path:** `Views/Address/`
**Target:** `Areas/AccountProfile/Controllers/AddressController.cs`
**No backoffice views** — addresses are sub-actions of a UserProfile.

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `AddAddress.cshtml` | 👤 | `GET /Address/AddAddress?id={customerId}` | ✅ | Add address form |
| `EditAddress.cshtml` | 👤 | `GET /Address/EditAddress/{id}` | ✅ | Edit address form |
| `ViewAddress.cshtml` | 👤 | `GET /Address/ViewAddress/{id}` | ✅ | Read-only address detail |

---

## Catalog BC — Products (legacy Item)

**Controller:** `Controllers/ItemController.cs`
**View path:** `Views/Item/`
**Target:** `Areas/Catalog/Controllers/ProductController.cs`

### Public

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🌐 | `GET /Item` | ✅ | Published product list |
| `ViewItem.cshtml` | 🌐 | `GET /Item/ViewItem/{id}` | ✅ | Product detail page |
| `ShowItemConnectedWithTags.cshtml` | 🌐 | `GET /Item/ShowItemConnectedWithTags` | ✅ | Products grouped by tag |

### Backoffice-only

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `AddItem.cshtml` | 🔐 | `GET /Item/AddItem` | ✅ | Add product form |
| `EditItem.cshtml` | 🔐 | `GET /Item/EditItem/{id}` | ✅ | Edit product form (includes image upload) |
| `ShowItemBrands.cshtml` | 🔐 | `GET /Item/ShowItemBrands` | ❌ | **Deleted at switch** — `Brand` concept removed (ADR-0007) |

---

## Catalog BC — Tags (legacy)

**Controller:** `Controllers/TagController.cs`
**View path:** `Views/Tag/`
**Target:** `Areas/Catalog/Controllers/TagController.cs`
All views are **backoffice** in the target state (legacy had `Index` as `[Auth]` — tightened).

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Tag` | ✅ | Tag list |
| `AddTag.cshtml` | 🔐 | `GET /Tag/AddTag` | ✅ | Add tag form |
| `EditTag.cshtml` | 🔐 | `GET /Tag/EditTag/{id}` | ✅ | Edit tag form |
| `ViewTag.cshtml` | 🔐 | `GET /Tag/ViewTag/{id}` | ✅ | Tag detail — name, products using this tag |

---

## Inventory BC

**Controller:** `Controllers/InventoryController.cs`
**View path:** `Views/Inventory/`
**No user-facing views.** Inventory management is entirely backoffice.

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Inventory` | ✅ | Stock levels — paginated overview |
| `Reservations.cshtml` | 🔐 | `GET /Inventory/Reservations` | ✅ | Active holds — paged, filter by status |
| `Audit.cshtml` | 🔐 | `GET /Inventory/Audit` | ✅ | Audit log — all stock movement history |
| `AdjustStock.cshtml` | 🔐 | `GET /Inventory/AdjustStock` | ✅ | Adjustment form — schedules a stock correction |
| `PendingAdjustments.cshtml` | 🔐 | `GET /Inventory/PendingAdjustments` | ✅ | List of scheduled adjustments awaiting confirmation |

---

## Sales BC — Coupons (legacy)

**Controller:** `Controllers/CouponController.cs`
**View path:** `Views/Coupon/`
**Target:** `Areas/Sales/Controllers/CouponController.cs`
All views are **backoffice**. No customer-facing coupon pages — coupons are entered inline at checkout.

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Coupon` | ✅ | Coupon list |
| `AddCoupon.cshtml` | 🔐 | `GET /Coupon/AddCoupon` | ✅ | Add coupon form |
| `EditCoupon.cshtml` | 🔐 | `GET /Coupon/EditCoupon/{id}` | ✅ | Edit coupon form |
| `ViewCoupon.cshtml` | 🔐 | `GET /Coupon/ViewCoupon/{id}` | ✅ | Coupon detail — status, usage record if used |

### Deleted at Slice 1 atomic switch

| View file | Reason |
|-----------|--------|
| `CouponType/Index.cshtml` | ❌ `CouponType` is Slice 2 — not in Slice 1 target |
| `CouponType/AddCouponType.cshtml` | ❌ Same |
| `CouponType/EditCouponType.cshtml` | ❌ Same |
| `CouponType/ViewCouponType.cshtml` | ❌ Same |
| `CouponUsed/Index.cshtml` | ❌ `CouponUsed` records created/released by domain events — no manual CRUD |
| `CouponUsed/AddCouponUsed.cshtml` | ❌ Same |
| `CouponUsed/EditCouponUsed.cshtml` | ❌ Same |
| `CouponUsed/ViewCouponUsed.cshtml` | ❌ Same |

---

## Supporting — Currencies

**Controller:** `Controllers/CurrencyController.cs`
**View path:** `Views/Currency/`
**No user-facing views.** Currency management is entirely backoffice.

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /Currency` | ✅ | Currency list |
| `AddCurrency.cshtml` | 🔐 | `GET /Currency/AddCurrency` | ✅ | Add currency form |
| `EditCurrency.cshtml` | 🔐 | `GET /Currency/EditCurrency/{id}` | ✅ | Edit currency form |
| `ViewCurrency.cshtml` | 🔐 | `GET /Currency/ViewCurrency/{id}` | ✅ | Currency detail — code, description, current rates |

---

## Supporting — Jobs

**Controller:** `Controllers/JobManagementController.cs`
**View path:** `Views/JobManagement/`
**No user-facing views.**

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /JobManagement` | ✅ | Job list — name, schedule, last-run status |
| `History.cshtml` | 🔐 | `GET /JobManagement/History/{name}` | ✅ | Execution history for a specific job — paged |

---

## IAM — User Management

**Controller:** `Controllers/UserManagementController.cs`
**View path:** `Views/UserManagement/`
**No user-facing views.**

| View file | Audience | Route | Status | Notes |
|-----------|----------|-------|--------|-------|
| `Index.cshtml` | 🔐 | `GET /UserManagement` | ✅ | All users — paginated, searchable |
| `AddUser.cshtml` | 🔐 | `GET /UserManagement/AddUser` | ✅ | Create user form |
| `EditUser.cshtml` | 🔐 | `GET /UserManagement/EditUser/{id}` | ✅ | Edit user details |
| `AddRolesToUser.cshtml` | 🔐 | `GET /UserManagement/AddRolesToUser/{id}` | ✅ | Assign role to user |
| `ChangeUserPassword.cshtml` | 🔐 | `GET /UserManagement/ChangeUserPassword/{id}` | ✅ | Admin password reset |

---

## Deleted at Atomic Switch — Legacy Payment Views

These five views are replaced in full by `Areas/Sales/Views/Payments/`.

| View file | Replaced by |
|-----------|-------------|
| `Views/Payment/Index.cshtml` | `Areas/Sales/Views/Payments/Index.cshtml` |
| `Views/Payment/AddPayment.cshtml` | `Areas/Sales/Views/Payments/Create.cshtml` |
| `Views/Payment/EditPayment.cshtml` | ❌ No direct equivalent — payments are confirmed, not edited |
| `Views/Payment/ViewPayment.cshtml` | `Areas/Sales/Views/Payments/Details.cshtml` |
| `Views/Payment/ViewMyPayments.cshtml` | `Areas/Sales/Views/Payments/MyPayments.cshtml` |

---

## Deleted at Atomic Switch — ContactDetail Views

Replaced by inline `Email` + `PhoneNumber` fields on `UserProfile`.

| View file |
|-----------|
| `Views/ContactDetail/AddNewContactDetail.cshtml` |
| `Views/ContactDetail/EditContactDetail.cshtml` |
| `Views/ContactDetail/ViewContactDetail.cshtml` |

---

## Open Issues Summary

| # | View | Issue | Severity |
|---|------|-------|----------|
| 1 | `Areas/Sales/Views/Orders/Details.cshtml` | Controller serves any order to any auth user — scope check missing | 🔴 Security |
| 2 | `Areas/Sales/Views/Payments/Create.cshtml` | Controller route takes `int id` (orderId) instead of `Guid paymentId`; no 403 guard for non-Pending state | 🔴 Must fix before switch |
| 3 | `Areas/Sales/Views/Payments/Details.cshtml` | No `payment.UserId ≠ caller` check in controller | 🔴 Security |
| 4 | `Views/Refund/ViewRefundDetails.cshtml` | No `refund.UserId ≠ caller` check — any auth user sees any refund | 🔴 Security |
| 5 | `Views/Refund/EditRefund.cshtml` | Approve / Reject buttons not present — decision actions not yet implemented | 🟠 Missing feature |
| 6 | *(missing)* `Views/Refund/Report.cshtml` | Admin refund report view does not exist | 🟠 Missing feature |
| 7 | `Views/Customer/EditCustomer.cshtml` | Ownership of the `id` parameter relative to session user not enforced | 🟡 Investigate |
| 8 | `Views/Customer/ViewCustomer.cshtml` | Same ownership concern | 🟡 Investigate |
| 9 | *(missing)* `Views/Refund/Request.cshtml` | Customer has no Web page to submit a refund request — `POST /Refund/Request` is `[Maint]` only in current code | 🔴 Missing user flow |

---

## References

- [endpoint-map.md](endpoint-map.md) — post-switch target route definitions
- [ADR-0007 — Catalog BC](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)
- [ADR-0005 — AccountProfile BC](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0017 — Sales/Fulfillment BC](../adr/0017-sales-fulfillment-bc-design.md)
