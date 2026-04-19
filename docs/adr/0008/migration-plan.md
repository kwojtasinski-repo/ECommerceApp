## Migration plan

**Phase 1 — Parallel implementation (✅ complete)**
All new BC artifacts created: domain, infrastructure, application, unit tests.
New services registered alongside legacy services in DI — no old registrations removed.

**Phase 2 — DB migration (✅ approved — applied)**
Generate and review `ECommerceApp.Infrastructure/Supporting/Currencies/Migrations/` via
`dotnet ef migrations add InitCurrenciesSchema --context CurrencyDbContext`.
Includes a data migration script to populate "currencies" schema tables from legacy "dbo" tables.
Follow `docs/instructions/migration-policy.md` checklist before running against any shared environment.

**Phase 3 — Integration tests (⬜ pending migration)**
Write integration tests under `ECommerceApp.IntegrationTests/Supporting/Currencies/` targeting
`ICurrencyService` and `ICurrencyRateService` via `BaseTest<TService>`.

**Phase 4 — Controller migration (⬜ pending integration tests)**
Update `Web/Controllers/CurrencyController.cs`:
- Inject `Application.Supporting.Currencies.Services.ICurrencyService` and `ICurrencyRateService`.
- Convert all action methods to `async Task<IActionResult>`.
- Adapt to new DTO/VM contracts (`CreateCurrencyDto`, `UpdateCurrencyDto`, `CurrencyVm`, `CurrencyListVm`).
- Coordinate with Catalog BC switch: ensure `ItemService` no longer references legacy
  `Domain/Interface/ICurrencyRepository` before the legacy registrations are removed.

**Phase 5 — Atomic switch (⬜ after integration tests pass)**
1. Remove legacy DI registrations from `Application/Services/Extensions.cs`:
   `ICurrencyService → CurrencyService` and `ICurrencyRateService → CurrencyRateService`.
2. Remove `ICurrencyRepository` and `ICurrencyRateRepository` from `Infrastructure/Repositories/Extensions.cs`.
3. Run full test suite — all tests must pass.
4. Delete legacy files: `Domain/Model/Currency.cs`, `Domain/Model/CurrencyRate.cs`,
   `Domain/Interface/ICurrencyRepository.cs`, `Domain/Interface/ICurrencyRateRepository.cs`,
   `Application/Services/Currencies/`, `Application/ViewModels/Currency/`,
   `Application/DTO/CurrencyDto.cs`, `Application/DTO/CurrencyRateDto.cs`,
   `Application/Constants/CurrencyConstants.cs`,
   `Infrastructure/Repositories/CurrencyRepository.cs`, `Infrastructure/Repositories/CurrencyRateRepository.cs`,
   `Infrastructure/Database/Configurations/CurrencyConfiguration.cs`,
   `Infrastructure/Database/Configurations/CurrencyRateConfiguration.cs`.
5. Remove `DbSet<Currency>` and `DbSet<CurrencyRate>` from `Infrastructure/Database/Context.cs`.
6. Update `## Implementation Status` table in this ADR (all rows → ✅ Done).
7. Update `docs/architecture/bounded-context-map.md` — move **Currencies** from
   "Completed (switch pending)" to fully switched.

---
