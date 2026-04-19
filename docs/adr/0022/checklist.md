## Conformance checklist

- [x] Top bar rendered for all visitors (no `@if (SignInManager.IsSignedIn(...))` wrapper)
- [x] 🛒 badge hidden for anonymous users; cart-count `ajaxRequest` call preserved for signed-in users
- [x] `[👤▾]` anonymous state shows Logowanie + Rejestracja only
- [x] `[👤▾]` signed-in state shows Moje zamówienia, Moje płatności, Dane kontaktowe, Wyloguj
- [x] Secondary nav `Kategorie ▾` rendered for guests and `User` role only (not MaintenanceRole)
- [x] Secondary management bar rendered only for `MaintenanceRole`
- [x] `Kupony` top-level link guarded by `ManagingRole` (not visible to Service)
- [x] `Katalog ▾ → Marki` guarded by `ManagingRole`
- [x] `Zaplecze ▾ → Użytkownicy` and `Kupony - typy` guarded by `ManagingRole`
- [x] `[Kategoria ▾]` filter and `Kategorie ▾` dropdown both populated from single `ICategoryService` injection
- [x] `IStockQueryService` has `GetOverviewAsync`, `GetHoldsAsync`, `GetPendingAdjustmentsAsync`, `GetAuditAsync` *(deviation: separate service instead of extending `IStockService` — see §6 and Alternatives)*
- [x] `InventoryController` class-level `[Authorize(Roles = MaintenanceRole)]`
- [x] All five Inventory views exist under `Views/Inventory/`
- [x] No `<partial name="_LoginPartial" />` call remains in `_Layout.cshtml`
- [x] `_LoginPartial.cshtml` deleted after Phase 4 verification
- [x] No new views in this ADR introduce `ajaxRequest.js` (ADR-0021 § 3)
