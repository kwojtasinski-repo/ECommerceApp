# Remaining Work Effort

> **Rev 2 — Generated 2026-05-28**
> Test suite: **1020 / 1020** ✅ (843 unit + 177 integration)
> Previous priorities 1–4 (IAM switch, legacy view cleanup, Payments cleanup, Sales switch) are **all complete**.

---

## Summary

| # | Item | Size | Status |
|---|---|---|---|
| 1 | Orphaned view cleanup (~38 files in 11 folders) | XS | 🟡 Pending |
| 2 | R-8 — `ImageController` legacy service swap → `ImageService` moved to `Catalog.Images.Services` | S | ✅ Done |
| 3 | R-2 — `RefundController.Report` action | — | ✅ Dropped (no requirement, API not needed) |
| 4 | R-7 — `ShowItemConnectedWithTags` feature gap | S | ✅ Done — `ByTag` action + tag filter strip + clickable tag links in Details |
| 5 | CurrencyRateSyncTask atomic switch (TimeManagement) | XS | 🟡 Pending |
| 6 | Refresh Token expiry cleanup job | XS | 🔵 Deferred (low priority) |
| 7 | Brand BC — no BC equivalent, legacy-only | TBD | 🔴 Tech debt, no timeline |
| 8 | Coupons Slice 2 — DB migration approval + atomic switch | L | 🔄 In progress |
| 9 | Communication BC | TBD | ❌ Not started |
| 10 | Backoffice BC | TBD | ❌ Blocked (ADR-0013) |
| 11 | Per-BC DbContext interfaces (ADR-0013) | — | ❌ Gate: ~80–100% BCs complete |
| 12 | API: Move controllers into BC subfolders | XS | ✅ Done |
| 13 | API: Remove `v2` from 3 route attributes + `.http` files | XS | ✅ Done |
| 14 | API: Extract `TrustedApiUser` policy const | XS | ✅ Done |

---

## Priority 1 — Orphaned View Cleanup (~38 files) — **XS**

All controllers serving these views have been deleted or moved to Areas. Pure dead code.

### V2\* View Folders (7 folders, ~25 files)

| Folder | Files |
|---|---|
| `Views/V2Category/` | Add, Edit, Index (3) |
| `Views/V2Currency/` | Add, Details, Edit, Index (4) |
| `Views/V2Product/` | Add, Details, Edit (3) |
| `Views/V2Profile/` | Add, Details, Edit, Index (4) |
| `Views/V2Tag/` | Add, Edit (2) |
| `Views/V2User/` | Add, Details, Edit, Index (4) |
| `Views/V2Job/` | DeferredQueue, History, Index, Register, ScheduleDeferred (5) |

### Legacy BC View Folders (4 folders, ~15 files)

| Folder | Files | Notes |
|---|---|---|
| `Views/Coupon/` | AddCoupon, EditCoupon, Index, ViewCoupon (4) | `CouponController` moved to `Areas/Sales` |
| `Views/CouponType/` | AddCouponType, EditCouponType, Index, ViewCouponType (4) | No replacement |
| `Views/CouponUsed/` | AddCouponUsed, EditCouponUsed, Index, ViewCouponUsed (4) | No replacement |
| `Views/Refund/` | EditRefund, Index, ViewRefundDetails (3) | `RefundController` moved to `Areas/Sales` |

**Action**:
```
git rm -r Views/V2Category Views/V2Currency Views/V2Product Views/V2Profile Views/V2Tag Views/V2User Views/V2Job Views/Coupon Views/CouponType Views/CouponUsed Views/Refund
dotnet build  # must stay green
```

**Risk**: None — build confirms no remaining references.

---

## Priority 2 — R-8: `ImageController` Legacy Service Dependency — **S**

| | |
|---|---|
| **Files** | `Web/Areas/Catalog/Controllers/ImageController.cs` + `API/Controllers/ImageController.cs` |
| **Issue** | Both controllers inject `IImageService` from `Application.Services.Items` (legacy namespace) |
| **Decision** | **Option A** — Create `Application.Catalog.Images.Services.IImageService` in the Catalog BC using existing repository/implementation. Full switch — no accepted coupling. |
| **Effort** | S |

---

## Priority 3 — R-2: `RefundController.Report` Action — ~~S~~ **Dropped**

| | |
|---|---|
| **Decision** | **Dropped** — no defined requirement exists. The API has `GET /api/refunds/{id}` for individual lookups; any future reporting need would be served by query params on the list endpoint (`?status=X&from=date&to=date`), not a separate `/report` action. Web `Report` view also dropped — no product owner requirement. |
| **R-2 status** | Resolved (dropped) |

---

## Priority 4 — R-7: `ShowItemConnectedWithTags` Feature Gap — **S (Web only)**

| | |
|---|---|
| **Legacy route** | `/Item/ShowItemConnectedWithTags` — grouped product listing by tag |
| **Decision** | **Web only** — add `ByTag(int tagId)` action to `Areas/Catalog/Controllers/ProductController.cs`. User clicks a tag → sees products with that tag. **API: No** — API consumers handle their own grouping; add optional `?tagId=X` filter to existing list endpoint if needed later. |
| **Effort** | S — reuse `IProductService.GetProductsByTagAsync` if it exists |

---

## Priority 5 — CurrencyRateSyncTask Atomic Switch — **XS**

| | |
|---|---|
| **Context** | `JobManagementController` switched to `Areas/Jobs` ✅. `CurrencyRateSyncTask` wiring in the TimeManagement BC still uses legacy scheduling hook. |
| **Action** | Verify task registration goes through the new BC's `IDeferredJobScheduler`; delete any legacy scheduling call if present |
| **ADR** | [ADR-0009](../../docs/adr/0009-supporting-timemanagement-bc-design.md) |

---

## Priority 6 — Refresh Token Expiry Cleanup Job — **XS (Deferred)**

| | |
|---|---|
| **Action** | Register a Hangfire job calling `IRefreshTokenRepository.DeleteExpiredAsync` — piggyback on `JobManagement` infrastructure |
| **Priority** | Low — not a blocker for any other work |

---

## Priority 7 — Brand BC — **Tech Debt, No Timeline**

Brand has no BC equivalent. Legacy stack is healthy and fully functional.

| Component | File | Status |
|---|---|---|
| Controller | `Web/Controllers/BrandController.cs` | ✅ Active (legacy) |
| Service | `Application/Services/Brands/BrandService.cs` + `IBrandService.cs` | ✅ Active (legacy) |
| Repository | `Infrastructure/Repositories/BrandRepository.cs` | ✅ Active (legacy) |
| Views | `Views/Brand/` (AddBrand, EditBrand, Index, ViewBrand — 4 files) | ✅ Active (legacy) |

**Decision needed**: Create a `Supporting/Brands` BC (or fold into `Catalog`) — or accept as perpetual legacy. No action until decision made.

---

## Priority 8 — Coupons Slice 2 — **L (In Progress)**

Implementation complete at Domain + Application + Infrastructure layers.

| Item | Status |
|---|---|
| Domain: CouponOversizeGuard, Catalog name-sync messages (3 messages + 3 handlers + `IScopeTargetRepository`) | ✅ Done |
| Application: 16 evaluators (15 + CouponOversizeGuard auto-injected), workflow builder, contracts | ✅ Done |
| Infrastructure: 5 adapters/repos (StockAvailabilityChecker, CompletedOrderCounter, SpecialEventCache, CouponApplicationRecordRepository, NullRuntimeCouponSource) | ✅ Done |
| DB migration: `CouponApplicationRecords` + `SpecialEvents` tables | 🟡 Pending approval |
| Atomic switch | ❌ Blocked by Coupons Slice 1 in production |

See [ADR-0016](../../docs/adr/0016-sales-coupons-bc-design.md) §9.

---

## Priority 9 — Communication BC — **Not Started**

Unblocked (Fulfillment Slice 1 + Coupons Slice 1 both live). No ADR yet. Not critical path.

---

## Priority 10 — Backoffice BC — **Blocked**

Blocked by ADR-0013 (per-BC DbContext interfaces). Gate: ~80% BC implementations complete.

---

## Priority 11 — Per-BC DbContext Interfaces (ADR-0013) — **Gate Condition**

Gate: ~80–100% BC implementations complete. With 10 of ~12 BCs fully switched, this gate is approaching.

---

## ✅ Completed (All Previous Priorities)

| Sprint | Item | Status |
|---|---|---|
| Sprint A | R-1, R-3, R-4, R-5, R-6 security fixes | ✅ Done |
| Sprint A | IAM Refresh Token Steps 1–8 (full feature) | ✅ Done |
| Sprint B | IAM Atomic Switch — `Context` → `DbContext`, `ApplicationUser` deleted, all legacy IAM files deleted, ADR-0019 Accepted | ✅ Done |
| Sprint C | Legacy view cleanup — `Views/Order/` (17), `Views/OrderItem/` (3), `Views/Payment/` (5), `Views/UserManagement/` (5) deleted | ✅ Done |
| Sprint C | Payments Legacy Cleanup — `PaymentHandler`, `PaymentService`, `IPaymentService` (legacy) deleted; DI cleaned | ✅ Done |
| Sprint D | Sales Atomic Switch — `OrderController`, `OrderItemController`, `PaymentController` deleted; 30+ legacy service/repo/interface files deleted | ✅ Done |
| Sprint D | API V2 namespace → `Controllers` namespace — 12 controllers moved, V2 folder deleted | ✅ Done |
| Sprint D | 7 intermediate V2\* Web controllers deleted | ✅ Done |
| Sprint D | 15 legacy integration test files + 16 unit test directories deleted; test infra updated for IAM auth | ✅ Done |
| Sprint D | All Area switches live: AccountProfile, Catalog, Inventory, Currencies, Jobs, Presale, Sales/Orders, Sales/Payments, Sales/Fulfillment, Sales/Coupons, IAM | ✅ Done |
