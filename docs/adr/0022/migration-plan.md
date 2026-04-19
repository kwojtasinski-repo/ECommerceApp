## Migration plan

**Phase 1 — `IStockQueryService` — new dedicated query service (prerequisite for Phase 3):** ✅
1. ✅ Create `IStockQueryService` in `Application/Inventory/Availability/Services/`.
2. ✅ Implement `StockQueryService`; wire repository queries for overview, holds, audit, and
   pending adjustments with pagination.
3. ✅ Add view-model types (`StockOverviewVm`, `StockHoldsVm`, `StockAuditVm`,
   `PendingAdjustmentsVm`) to `Application/Inventory/Availability/ViewModels/`.
4. ✅ Register `IStockQueryService` / `StockQueryService` in DI. `IStockService` is unchanged;
   no mock updates required for existing tests.

**Phase 2 — `_Layout.cshtml` two-tier navbar:** ✅
5. ✅ Replace the single `<nav>` block with top bar + secondary nav structure.
6. ✅ Add search form (`GET Item/Index`) with category filter dropdown.
7. ✅ Move cart badge into top bar; preserve existing `ajaxRequest` cart-count mechanism.
8. ✅ Inline the `[👤▾]` user menu (anonymous + signed-in states); remove
   `<partial name="_LoginPartial" />`.
9. ✅ Wire secondary nav: `Kategorie ▾` for guests/users; management bar for `MaintenanceRole`.
10. ✅ Inject `@inject ICategoryService CategoryService` at top of `_Layout.cshtml`; populate
    both `[Kategoria ▾]` and `Kategorie ▾` from `CategoryService.GetAllCategories()`.

**Phase 3 — InventoryController + 5 views:** ✅
11. ✅ Create `ECommerceApp.Web/Controllers/InventoryController.cs`.
12. ✅ Create views: `Views/Inventory/Index.cshtml`, `Reservations.cshtml`,
    `AdjustStock.cshtml`, `PendingAdjustments.cshtml`, `Audit.cshtml`.

**Phase 4 — Cleanup:** ✅
13. ✅ Verified no other layout or area partial renders `<partial name="_LoginPartial" />`.
14. ✅ Deleted `_LoginPartial.cshtml`.
15. Update `project-state.md` and `known-issues.md`.
