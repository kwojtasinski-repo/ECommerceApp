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

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/Sales/Refund` | [Maint] | Paginated refund list — OrderId, Status (Requested / Approved / Rejected), RequestedAt |
| POST | `/Sales/Refund` | [Maint] | Same with filter |
| GET | `/Sales/Refund/Edit/{id}` | [Maint] | Refund detail + **Approve / Reject button pair** — operator decision view |
| POST | `/Sales/Refund/Approve/{id}` | [Maint] | Submit approve → `IRefundService.ApproveAsync` — triggers `RefundApproved` event |
| POST | `/Sales/Refund/Reject/{id}` | [Maint] | Submit reject → `IRefundService.RejectAsync` — triggers `RefundRejected` event |
| ANY | `/Sales/Refund/View/{id}` | [Auth] | Read-only refund detail — customer can view own refund status |
| POST | `/Sales/Refund/Request` | [Maint] | JSON — AJAX endpoint called from Order detail view to submit a refund request → `IRefundService.AddRefundAsync` |
| PUT | `/Sales/Refund/Update/{id}` | [Maint] | JSON — update refund data before decision (reason, warranty flag) |

**Deleted:**
- `DELETE /Sales/Refund/Delete/{id}` — **not migrated** — no hard delete in domain; `Reject()` is the terminal negative outcome

---

## API Endpoints — Post-Switch (in-place swap, same routes)

> **Constraint (ADR-0024):** API routes are unchanged. Only the service implementation is swapped.
> `[Auth]` = `[Authorize]`. `[Maint]` = `[Authorize(Roles = MaintenanceRole)]`.

---

### Catalog BC

#### `api/items` → `IProductService`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/items` | [Auth] | Paginated product list — returns Name, Price, Category, imageUrls (not Base64) |
| GET | `api/items/{id}` | [Auth] | Product detail — full fields including imageUrls array |
| POST | `api/items` | [Maint] | Create product — Name, Description, Price, CategoryId, TagIds |
| PUT | `api/items/{id}` | [Maint] | Update product |
| DELETE | `api/items/{id}` | [Maint] | Delete / discontinue product |

#### `api/images` → `IProductService` (image management) + binary serving

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/images/{id}` | **[AllowAnonymous]** | **Binary image file** — `FileStreamResult` (`image/jpeg` / `image/png`). Shared by Web `<img src>` tags and external API clients. Public — no auth header in browser `<img>` requests. |
| POST | `api/images` | [Maint] | Upload single image for a product (`multipart/form-data`) → stored via `IFileStore` |
| POST | `api/images/multi-upload` | [Maint] | Upload multiple images for a product |
| DELETE | `api/images/{id}` | [Maint] | Delete image file and DB record |

**Deleted from legacy API:**
- `GET api/images` (list all images) — not needed in Catalog BC; images are accessed per product via `imageUrls`

#### `api/tags` → `IProductTagService`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/tags` | [Maint] | All tags list — for admin/management only |
| GET | `api/tags/{id}` | [Maint] | Tag detail |
| POST | `api/tags` | [Maint] | Create tag |
| PUT | `api/tags/{id}` | [Maint] | Update tag |
| DELETE | `api/tags/{id}` | [Maint] | Delete tag |

---

### AccountProfile BC

#### `api/customers` → `IUserProfileService`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/customers` | [Maint] | All user profiles — admin list |
| GET | `api/customers/{id}` | [Auth] | Single profile detail — scoped to own profile unless admin |
| POST | `api/customers` | [Auth] | Create user profile — Email + PhoneNumber in body |
| PUT | `api/customers/{id}` | [Auth] | Update profile |

#### `api/addresses` → `IUserProfileService`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/addresses/{id}` | [Auth] | Address detail — scoped to own profile |
| POST | `api/addresses` | [Auth] | Add address to profile |
| PUT | `api/addresses` | [Auth] | Update address |
| DELETE | `api/addresses/{id}` | [Maint] | Admin-only address delete |

**Deleted from legacy API:**
- `api/contact-details` — entire controller removed; Email/PhoneNumber are now fields on the profile

---

### Sales/Coupons BC — Slice 1

#### `api/coupons` → `ICouponService`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/coupons` | [Maint] | Paginated coupon list |
| GET | `api/coupons/{id}` | [Auth] | Coupon detail — Code, DiscountPercent, Status |
| POST | `api/coupons` | [Maint] | Create coupon |
| PUT | `api/coupons/{id}` | [Maint] | Update coupon (only if `Available`) |
| DELETE | `api/coupons/{id}` | [Maint] | Delete coupon (only if `Available`) |

---

### Sales/Fulfillment BC — Slice 1

#### `api/refunds` → `IRefundService`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `api/refunds` | [Maint] | Paginated refund list |
| GET | `api/refunds/{id}` | [Auth] | Refund detail — items, status, reason |
| POST | `api/refunds` | [Maint] | Submit refund request for an order |
| PUT | `api/refunds/{id}` | [Maint] | Update refund before decision (reason, warranty flag) |

**Note:** Approve/Reject on the API side should use dedicated action routes in a future V2 controller (e.g. `POST api/v2/refunds/{id}/approve`, `POST api/v2/refunds/{id}/reject`). The Slice 1 swap keeps `PUT` for now — V2 is post-switch cleanup.

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
