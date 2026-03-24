# Endpoint Map — Post-Switch Target State

> **Reference document.** Lists every HTTP endpoint that will exist **after all BC atomic switches are complete.**
> Legacy endpoints (pre-switch) are not listed. Deleted endpoints are noted where the contrast matters.
> Routing strategy authority: [ADR-0024](../adr/0024-controller-routing-strategy.md).
> Last updated: 2026-03-27 (initial draft — pre-switch)

---

## Image Serving — Design Decision

**Current (legacy):** `ImageService.Get()` reads file bytes from disk → `Convert.ToBase64String()` → embedded as `ImageSource` string in every response JSON. No dedicated binary GET endpoint exists. Views render images via `data:image/jpeg;base64,...` data URLs.

**Target (post-switch) — one endpoint, two callers:**
- `GET api/images/{id}` is the **single** binary-serving endpoint for all callers — no separate Web route.
- Returns `FileStreamResult` (raw bytes, `Content-Type: image/jpeg` or `image/png`).
- **Web** `.cshtml` views use `<img src="/api/images/{id}" loading="lazy">` — browser fetches the API route directly.
- **API** external clients (mobile, SPA, integrations) call the same endpoint.
- Product/Item responses include `imageUrls: ["/api/images/1", ...]` — not Base64.
- `IFileStore` continues to own physical write/read; only the HTTP response format changes.
- `GET api/images/{id}` is `[AllowAnonymous]` — required because product pages are public and `<img>` tags carry no auth headers.
- Upload and delete remain `[Maint]` only.

**Admin image preview (upload flow):** handled client-side via JavaScript `FileReader.readAsDataURL()` — never a server Base64 response.

---

## Web Endpoints — Post-Switch

> Convention: `[Auth]` = any authenticated user. `[Maint]` = `MaintenanceRole` (Administrator / Manager / Service).
> All Web endpoints live under their BC Area: `/Catalog/...`, `/AccountProfile/...`, `/Sales/...`.

> **User-facing vs. backoffice:** Not all Web endpoints are intended for end users. Within each BC section the rows are grouped by audience:
> - **User-facing** — `[Auth]` rows that operate on the caller's own data (`userId` always from session, never from the URL). These are the pages a logged-in customer can reach.
> - **Backoffice-only** — `[Maint]` rows. Operators / managers only. Never linked from the customer-facing UI.
> - **Public** — no auth required (e.g. product browsing).
> An action that posts and immediately redirects (no view rendered) is noted as **action** in the description.

---

### Catalog BC

#### Product (`/Catalog/Product/...`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Catalog/Product` | public | Paginated product list for customers (published products only) |
| POST | `/Catalog/Product` | public | Same list with filter params (`pageSize`, `pageNo`, `searchString`) |
| GET | `/Catalog/Product/Add` | [Maint] | Add product form — fields: Name, Description, Price, Category, Tags (no Quantity, no Brand/Type) |
| POST | `/Catalog/Product/Add` | [Maint] | Submit new product → `IProductService.AddProductAsync` |
| GET | `/Catalog/Product/Edit/{id}` | [Maint] | Edit product form with current values |
| POST | `/Catalog/Product/Edit` | [Maint] | Submit product update → `IProductService.UpdateProductAsync` |
| GET | `/Catalog/Product/View/{id}` | public | Product detail page — shows images, description, price, tags |
| GET | `/Catalog/Product/WithTags` | [Maint] | Paginated list of products grouped with their tags |
| POST | `/Catalog/Product/WithTags` | [Maint] | Same with filter |
| ANY | `/Catalog/Product/Delete/{id}` | [Maint] | JSON — soft-delete or discontinue product → `IProductService.DeleteProductAsync` |

#### Image - Web uses GET api/images/{id} directly (no separate Web GET route)

Web .cshtml views reference images as img src="/api/images/{id}" loading="lazy". The Web ImageController handles **upload and delete only** - no binary serving route exists on the Web side.

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | /Catalog/Image/Upload | [Maint] | Upload one or more images for a product (IFormFile, max 10 MB, .jpg/.png only), stored via IFileStore, returns image id(s) |
| DELETE | /Catalog/Image/{id} | [Maint] | JSON - deletes image file from IFileStore and removes DB record |

#### Tag (`/Catalog/Tag/...`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Catalog/Tag` | [Maint] | Paginated tag list (was `[Authorize]` in legacy — now restricted to maintenance) |
| POST | `/Catalog/Tag` | [Maint] | Same with filter |
| GET | `/Catalog/Tag/Add` | [Maint] | Add tag form |
| POST | `/Catalog/Tag/Add` | [Maint] | Submit new tag → `IProductTagService.AddTagAsync` |
| GET | `/Catalog/Tag/Edit/{id}` | [Maint] | Edit tag form |
| POST | `/Catalog/Tag/Edit` | [Maint] | Submit tag update → `IProductTagService.UpdateTagAsync` |
| GET | `/Catalog/Tag/View/{id}` | [Maint] | Tag detail — name, slug, products using this tag |
| ANY | `/Catalog/Tag/Delete/{id}` | [Maint] | JSON — removes tag from all products and deletes it |

**Deleted (not migrated):**
- `BrandController` + all Brand views — `Brand` concept removed in ADR-0007
- `TypeController` + all Type views — `Type` concept removed in ADR-0007
- `/Item/ShowItemBrands` — Brand gone

---

### AccountProfile BC

#### UserProfile (`/AccountProfile/UserProfile/...`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/AccountProfile/UserProfile` | [Auth] | Current user's profile list (own profiles only) |
| POST | `/AccountProfile/UserProfile` | [Auth] | Same with filter |
| GET | `/AccountProfile/UserProfile/All` | [Maint] | All profiles across all users — admin/manager view |
| POST | `/AccountProfile/UserProfile/All` | [Maint] | Same with filter |
| GET | `/AccountProfile/UserProfile/Add` | [Auth] | Create profile form — fields: FirstName, LastName, IsCompany, NIP, CompanyName, **Email, PhoneNumber** (replaces ContactDetail) |
| POST | `/AccountProfile/UserProfile/Add` | [Auth] | Submit new profile → `IUserProfileService.AddUserProfileAsync` |
| GET | `/AccountProfile/UserProfile/Edit/{id}` | [Auth] | Edit profile form — includes Email, PhoneNumber inline |
| POST | `/AccountProfile/UserProfile/Edit` | [Auth] | Submit profile update |
| ANY | `/AccountProfile/UserProfile/View/{id}` | [Auth] | Profile detail — shows Email, PhoneNumber, addresses inline |
| ANY | `/AccountProfile/UserProfile/Delete/{id}` | [Maint] | JSON — admin-only profile delete |
| ANY | `/AccountProfile/UserProfile/Partial` | [Auth] | Partial view for inline profile creation (used in Orders flow) |
| GET | `/AccountProfile/UserProfile/Contacts` | [Maint] | JSON — returns Email + PhoneNumber for a `userId` (replaces `GetContacts` returning ContactDetail list) |

#### Address (`/AccountProfile/Address/...`) — no independent list; address operations are sub-actions of UserProfile

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/AccountProfile/Address/Add?profileId=` | [Auth] | Add address form, pre-filled with `UserProfileId` |
| POST | `/AccountProfile/Address/Add` | [Auth] | Submit → `IUserProfileService.AddAddressAsync` — redirects back to Edit profile |
| GET | `/AccountProfile/Address/Edit/{id}` | [Auth] | Edit address form |
| POST | `/AccountProfile/Address/Edit` | [Auth] | Submit address update — redirects back to Edit profile |
| ANY | `/AccountProfile/Address/View/{id}` | [Auth] | Read-only address detail |
| ANY | `/AccountProfile/Address/Delete/{id}` | [Auth] | JSON — removes address from UserProfile aggregate |

**Deleted (not migrated):**
- `ContactDetailController` + all ContactDetail views — Email/PhoneNumber are now direct fields on `UserProfile`
- `api/contact-details` endpoints — same reason

---

### Presale BC

#### Checkout (`/Presale/Checkout/...`)

| Method | Route | Auth | Description |
|--------|-------|------|--------------|
| GET | `/Presale/Checkout/Cart` | [Auth] | View current cart — items, quantities, line totals, running total |
| POST | `/Presale/Checkout/Cart` | [Auth] | Add/update item — `AddToCartDto`, max 99 units/line (Web limit via `AddToCartDtoValidator`) |
| GET | `/Presale/Checkout/PlaceOrder` | [Auth] | Checkout form — shipping address autofill, coupon code field, order summary |
| POST | `/Presale/Checkout/PlaceOrder` | [Auth] | Submit order → `ICheckoutService.PlaceOrderAsync` — coupon codes validated server-side. Redirects to `Summary`. |
| GET | `/Presale/Checkout/GetProfileForCheckout` | [Auth] | AJAX — returns default shipping address for autofill; `userId` from session |
| GET | `/Presale/Checkout/Summary` | [Auth] | Order confirmation — order number, total, next-step guidance (link to pay) |

**No backoffice Checkout views** — cart and order placement are customer-only flows.

---

### Sales BC

#### Orders (`/Sales/Orders/...`)

**User-facing:**

| Method | Route | Auth | Description |
|--------|-------|------|--------------|
| GET | `/Sales/Orders/MyOrders` | [Auth] | Own orders list — scoped to `userId` from session |
| GET | `/Sales/Orders/Details/{id}` | [Auth] | Own order detail — returns `403` if `order.UserId ≠ caller`. ⚠️ Current code missing scope check — must be fixed before switch. |

**Backoffice-only:**

| Method | Route | Auth | Description |
|--------|-------|------|--------------|
| GET | `/Sales/Orders` | [Maint] | All orders — paginated, searchable |
| POST | `/Sales/Orders` | [Maint] | Same with filter (`pageSize`, `pageNo`, `searchString`) |
| GET | `/Sales/Orders/Edit/{id}` | [Maint] | Edit order form |
| POST | `/Sales/Orders/Edit/{id}` | [Maint] | Submit order update |
| GET | `/Sales/Orders/PaidOrders` | [Maint] | Paid orders list — contains the **Dispatch** button per row (POSTs to `Dispatch/{id}`, no separate view) |
| POST | `/Sales/Orders/PaidOrders` | [Maint] | Same with filter |
| GET | `/Sales/Orders/Fulfillment/{id}` | [Maint] | Single-order fulfillment detail view — operator inspects one specific order's items and status before or after dispatch |

#### Payments (`/Sales/Payments/...`)

**User-facing:**

| Method | Route | Auth | Description |
|--------|-------|------|--------------|
| GET | `/Sales/Payments/Create/{paymentId:guid}` | [Auth] | Payment form — `paymentId` is `Payment.PaymentId` (Guid business identifier). Looks up via `GetByTokenAsync(guid, userId)`. Returns `403` if `Status ≠ Pending` (already paid or expired) or `UserId ≠ caller`. ⚠️ Current code takes `int id` (orderId) — must be changed to Guid before switch. |
| POST | `/Sales/Payments/Create/{paymentId:guid}` | [Auth] | Submit payment → `IPaymentService.ConfirmPaymentAsync` |
| GET | `/Sales/Payments/MyPayments` | [Auth] | Own payments list — scoped to `userId` from session |
| GET | `/Sales/Payments/Details/{id}` | [Auth] | Own payment detail — returns `403` if `payment.UserId ≠ caller`. ⚠️ Current code missing scope check — must be fixed before switch. |

**Backoffice-only:**

| Method | Route | Auth | Description |
|--------|-------|------|--------------|
| GET | `/Sales/Payments` | [Maint] | All payments — paginated, searchable |
| POST | `/Sales/Payments` | [Maint] | Same with filter |

---

### Supporting/Currencies BC

#### Currency (`/Currency/...`) — no Area (Supporting group; no Area-based routing for Supporting BCs)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Currency` | [Maint] | Paginated currency list |
| POST | `/Currency` | [Maint] | Same with filter |
| GET | `/Currency/Add` | [Maint] | Add currency form — fields: Code, Description |
| POST | `/Currency/Add` | [Maint] | Submit → `ICurrencyService.AddAsync` (async) |
| GET | `/Currency/Edit/{id}` | [Maint] | Edit currency form |
| POST | `/Currency/Edit` | [Maint] | Submit update → `ICurrencyService.UpdateAsync` (async) |
| GET | `/Currency/View/{id}` | [Maint] | Currency detail — Code, Description, current rates |
| ANY | `/Currency/Delete/{id}` | [Maint] | JSON async — `ICurrencyService.DeleteAsync` |

**Key change:** all actions are `async Task<IActionResult>` — new service is fully async.
**No API controller** for Currencies.

---

### Sales/Coupons BC — Slice 1

#### Coupon (`/Sales/Coupon/...`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Sales/Coupon` | [Maint] | Paginated coupon list — Code, DiscountPercent, Status |
| POST | `/Sales/Coupon` | [Maint] | Same with filter |
| GET | `/Sales/Coupon/Add` | [Maint] | Add coupon form — Code, DiscountPercent, Description (**no CouponType dropdown** — Slice 2) |
| POST | `/Sales/Coupon/Add` | [Maint] | Submit → `ICouponService.AddCouponAsync` |
| GET | `/Sales/Coupon/Edit/{id}` | [Maint] | Edit coupon form — same fields |
| POST | `/Sales/Coupon/Edit` | [Maint] | Submit update |
| GET | `/Sales/Coupon/View/{id}` | [Maint] | Coupon detail — Code, DiscountPercent, Status, usage record if used |
| DELETE | `/Sales/Coupon/Delete/{id}` | [Maint] | JSON — only allowed if `CouponStatus.Available` (not yet used) |
| GET | `/Sales/Coupon/ByCode` | [Maint] | JSON — lookup coupon by code string, returns status |

**Deleted (not migrated):**
- `CouponUsedController` + all CouponUsed views — `CouponUsed` is created/released by domain events (`CouponApplied`, `CouponRemovedFromOrder`) — no manual CRUD
- `CouponTypeController` + all CouponType views — `CouponType` is Slice 2 only

---

### Sales/Fulfillment BC — Slice 1

#### Refund (`/Sales/Refund/...`)

**User-facing:**

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Sales/Refund/Request/{orderId}` | [Auth] | Refund request form — reason field, items to include. Own-scoped: returns `403` if `order.UserId ≠ caller`. Linked from the Order Details page. |
| POST | `/Sales/Refund/Request/{orderId}` | [Auth] | Submit refund request → `IRefundService.RequestRefundAsync`. Own-scoped by `orderId` + `userId` from session. |
| GET | `/Sales/Refund/View/{id}` | [Auth] | Read-only refund status — own-scoped: returns `403` if `refund.UserId ≠ caller`. ⚠️ Current code missing scope check. |

**Backoffice-only:**

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Sales/Refund` | [Maint] | Paginated refund list — OrderId, Status (Requested / Approved / Rejected), RequestedAt |
| POST | `/Sales/Refund` | [Maint] | Same with filter |
| GET | `/Sales/Refund/Report` | [Maint] | Admin report — aggregate stats: total requested / approved / rejected counts, total amounts, date-range filter, export-ready table |
| GET | `/Sales/Refund/Edit/{id}` | [Maint] | Refund detail + **Approve / Reject button pair** — operator decision view; also editable fields: reason, warranty flag |
| POST | `/Sales/Refund/Edit/{id}` | [Maint] | Save field edits (reason, warranty flag) without approving or rejecting |
| POST | `/Sales/Refund/Approve/{id}` | [Maint] | Submit approve → `IRefundService.ApproveAsync` — triggers `RefundApproved` event |
| POST | `/Sales/Refund/Reject/{id}` | [Maint] | Submit reject → `IRefundService.RejectAsync` — triggers `RefundRejected` event |
| PUT | `/Sales/Refund/Update/{id}` | [Maint] | JSON — update refund data before decision (reason, warranty flag) |

**Deleted:**
- `DELETE /Sales/Refund/Delete/{id}` — **not migrated** — no hard delete in domain; `Reject()` is the terminal negative outcome
- `POST /Sales/Refund/Request` as `[Maint]` — replaced by the `[Auth]` own-scoped version above

---

## API Endpoints — Post-Switch (Client-Facing Surface)

> **V2 replacement:** `api/v2/cart` → `api/cart`, `api/v2/checkout` → `api/checkout`, `api/v2/orders` → `api/orders`, `api/v2/payments` → `api/payments`. No `api/v2/` prefix in the final state — V2 controllers are fully replaced.
> **Philosophy:** API is client-facing only. Admin operations have no API counterpart — they are Web-only.
> `[Auth]` = `[Authorize]`. `[Maint]` = `[Authorize(Roles = MaintenanceRole)]`. `[Trusted]` = TrustedApiUser policy (authenticated + `api:purchase` claim OR Service/Manager/Administrator role). See [ADR-0025](../adr/0025-api-tiered-access-trusted-purchase-policy.md).

---

### Catalog BC

#### `api/images` — binary serving

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/images/{id}` | **[AllowAnonymous]** | Binary image file — `FileStreamResult` (`image/jpeg` / `image/png`). Shared by Web `<img src>` tags and external API clients. |

> Admin operations (upload/delete images, manage tags, create/update/delete products) → **Web only**.

#### `api/items` — product browsing (read-only)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/items` | [Auth] | Paginated product list — Name, Price, Category, `imageUrls` (not Base64) |
| GET | `api/items/{id}` | [Auth] | Product detail — full fields including `imageUrls` array |

---

### Presale BC

#### `api/cart`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/cart` | [Auth] | Own cart — current items with prices |
| POST | `api/cart/items` | [Trusted] | Add or update item — max 5 units/line (`MaxApiQuantityFilter`) |
| DELETE | `api/cart/items/{productId}` | [Trusted] | Remove one item from cart |
| DELETE | `api/cart` | [Trusted] | Clear entire cart |

#### `api/checkout`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `api/checkout/initiate` | [Trusted] | Lock current cart prices into soft reservations (15-min TTL). Re-calling refreshes TTL and prices. |
| POST | `api/checkout/confirm` | [Trusted] | Place order — inline customer data (name, address, contact) + optional coupon codes validated server-side. Returns `orderId`. |

---

### Sales BC

#### `api/orders`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/orders/{id}` | [Auth] | Own order — status, items, fulfillment history. Returns `403` for another user's order. When `status == "Placed"`: response includes `paymentUrl = "{webBase}/Sales/Payments/Create/{payment.PaymentId}"` (Guid — not int orderId). When `status == "PaymentConfirmed"`: response includes `paymentId` (Guid) read from order events (`PaymentConfirmedPayload`). `paymentUrl` is `null` for all other statuses. |

> Admin operations (list all orders, list paid orders) → **Web only**.

#### `api/payments`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/payments/{paymentId}` | [Auth] | Own payment history — `paymentId` is the Guid business identifier returned in the order response. Own-scoped by `UserId` stored on the payment. |

> Admin operations (confirm payment) → **Web only**.
> Web payment page (`/Sales/Payments/Create/{paymentId}`) returns `403` when the payment is no longer `Pending` (already confirmed or expired) — prevents double payment.

#### `api/refunds`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/refunds/{id}` | [Auth] | Own refund status — customer-facing view. |
| POST | `api/refunds` | [Auth] | Submit refund request for own order. |

> Admin operations (list all refunds, approve, reject) → **Web only**.

---

### AccountProfile BC

#### `api/customers`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/customers/{id}` | [Auth] | Own profile detail — scoped to caller unless admin |
| POST | `api/customers` | [Auth] | Create profile — Email + PhoneNumber in body |
| PUT | `api/customers/{id}` | [Auth] | Update own profile |

> Admin operation (list all profiles) → **Web only**.

#### `api/addresses`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/addresses/{id}` | [Auth] | Address detail — own profile |
| POST | `api/addresses` | [Auth] | Add address to own profile |
| PUT | `api/addresses` | [Auth] | Update address |
| DELETE | `api/addresses/{id}` | [Auth] | Delete own address — own-scoped. |

**Deleted from legacy API:**
- `api/contact-details` — entire controller removed; Email/PhoneNumber are now fields on the profile

---

### Coupons

> **Security:** No coupon endpoint is exposed on the client API — ever. Coupon codes are submitted inline with `POST api/checkout/confirm` and validated server-side, preventing brute-force code scanning. No `IsPublic` flag, no public coupon list. Coupon management is Web/admin only.

---

## Endpoints Not Migrated (deleted at atomic switch)

| Endpoint | BC | Reason |
|----------|-----|--------|
| `BrandController` (Web + API) | Catalog | `Brand` concept removed in ADR-0007 |
| `TypeController` (Web + API) | Catalog | `Type` concept removed in ADR-0007 |
| `GET /Item/ShowItemBrands` | Catalog | Brand gone |
| `GET api/images` (list all) | Catalog | Replaced by per-product `imageUrls` |
| `ContactDetailController` (Web + API) | AccountProfile | Email/PhoneNumber replace ContactDetail |
| `api/contact-details` (3 endpoints) | AccountProfile | Same |
| `CouponUsedController` (Web) | Coupons Slice 1 | Event-driven — no manual CRUD |
| `CouponTypeController` (Web) | Coupons Slice 1 | Slice 2 only — not part of Slice 1 switch |
| `DELETE /Sales/Refund/Delete/{id}` | Fulfillment Slice 1 | No hard delete in domain — `Reject()` is terminal |

---

## Not covered here (separate concerns)

- **TimeManagement:** no controller migration — `JobManagementController` already exists; only background wiring changes
- **Inventory:** no controller migration — `InventoryController` already exists (ADR-0022); switch is `ItemHandler` → `IMessageBroker` + data migration
- **IAM:** last switch — `AuthenticationController` rewired to new `IAuthenticationService`
- **Sales/Coupons Slice 2, Sales/Fulfillment Slice 2:** blocked until Slice 1 switches are live
- **Communication, Backoffice:** blocked by upstream switches

---

## References

- [ADR-0024 — Controller Routing Strategy](../adr/0024-controller-routing-strategy.md)
- [ADR-0005 — AccountProfile BC](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0007 — Catalog BC](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)
- [ADR-0008 — Currencies BC](../adr/0008-supporting-currencies-bc-design.md)
- [ADR-0016 — Sales/Coupons BC](../adr/0016-sales-coupons-bc-design.md)
- [ADR-0017 — Sales/Fulfillment BC](../adr/0017-sales-fulfillment-bc-design.md)
