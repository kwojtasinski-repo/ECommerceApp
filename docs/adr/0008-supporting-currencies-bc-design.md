# ADR-0008: Supporting/Currencies BC — Currency and CurrencyRate Aggregate Design

## Status

Accepted

## Date

2026-02-25

## Context

The `Currency` and `CurrencyRate` domain models currently live in `ECommerceApp.Domain/Model/`
as anemic classes with public setters and no invariant enforcement. The legacy implementation
has several structural problems:

**Domain model issues:**
- `Domain/Model/Currency.cs` exposes `ICollection<Payment> Payments`, `ICollection<Order> Orders`,
  and `ICollection<Item> Items` — navigation properties that couple the Currencies BC directly
  to the Orders, Payments, and Catalog BCs. Changes to any of those BCs can cascade into `Currency`.
- `Domain/Model/CurrencyRate.cs` has a public `Currency Currency` navigation property and
  all-public setters. Any caller can mutate rate data without validation.
- No value objects: `Code` is a raw `string` with no format guarantee (3-char uppercase ISO code).
  Validation is duplicated between `CurrencyService.Add` and `CurrencyService.Update` with string
  comparisons instead of a type-safe guard.

**Service layer issues:**
- `Application/Services/Currencies/CurrencyService.cs` is `public class` (not `internal sealed`),
  exposing it outside the Application assembly boundary.
- `Application/Services/Currencies/CurrencyRateService.cs` calls the external NBP API via
  `.GetAwaiter().GetResult()` and `_NBPClient.GetCurrencyRateOnDate(...).Result`, blocking the
  thread pool and defeating the purpose of the async `INBPClient` contract.
- Both services perform validation logic (e.g., empty-code check) that belongs in domain
  value objects.

**Repository issues:**
- `Domain/Interface/ICurrencyRepository.cs` does not extend `IGenericRepository<T>`,
  inconsistent with all other repository interfaces.
- `Infrastructure/Repositories/CurrencyRepository.cs` wraps `IGenericRepository<Currency>`
  via composition instead of extending `GenericRepository<T>`, the established pattern.

**Cross-cutting:**
- `Application/Services/Items/ItemService.cs` injects `Domain/Interface/ICurrencyRepository`
  directly — Catalog's legacy service reaching into Currencies infrastructure.
- `Domain/Model/Currency.cs` has no dedicated `DbContext`; all `Currency` and `CurrencyRate`
  state is managed by the shared `Context.cs`, which offers no persistence-level BC boundary.

The project's strategic direction (ADR-0002) calls for incremental migration of all BCs toward
the target architecture. ADR-0003 and ADR-0004 define the feature-folder structure and module
taxonomy. Currencies belongs to the **Supporting** group as `Supporting/Currencies`.

---

## Decision

We implement the **Supporting/Currencies** bounded context in parallel alongside the existing
legacy implementation, following the Parallel Change strategy defined in ADR-0002.

### Domain layer (`ECommerceApp.Domain/Supporting/Currencies/`)

We introduce two self-validating **value objects**:

- `CurrencyCode` — `sealed record`; trims and uppercases input; enforces exactly 3 characters;
  throws `DomainException` on violation. File: `ValueObjects/CurrencyCode.cs`.
- `CurrencyDescription` — `sealed record`; trims input; enforces non-empty and max 300
  characters; throws `DomainException` on violation. File: `ValueObjects/CurrencyDescription.cs`.

We introduce two **strongly-typed IDs** inheriting from `TypedId<int>` (ADR-0006):

- `CurrencyId` — `sealed record CurrencyId(int Value) : TypedId<int>(Value)`. File: `CurrencyId.cs`.
- `CurrencyRateId` — `sealed record CurrencyRateId(int Value) : TypedId<int>(Value)`. File: `CurrencyRateId.cs`.

We introduce two **domain entities** with private setters and factory methods:

**`Currency`** (`Currency.cs`):
- All properties use `private set`: `Id` (`CurrencyId`), `Code` (`CurrencyCode`), `Description` (`CurrencyDescription`).
- `private Currency()` for EF Core materialization.
- `static Currency Create(string code, string description)` — validates via value objects before construction.
- `void Update(string code, string description)` — replaces public setter mutation.
- `static readonly CurrencyId PlnId = new CurrencyId(1)` — domain-level constant replacing `Application/Constants/CurrencyConstants.PlnId`.
- No navigation properties to Orders, Payments, or Catalog BCs — foreign-key IDs only.

**`CurrencyRate`** (`CurrencyRate.cs`):
- All properties use `private set`: `Id` (`CurrencyRateId`), `CurrencyId` (`CurrencyId`), `Rate` (`decimal`), `CurrencyDate` (`DateTime`).
- `private CurrencyRate()` for EF Core materialization.
- `static CurrencyRate Create(CurrencyId currencyId, decimal rate, DateTime currencyDate)` —
  throws `DomainException` for null `currencyId`, non-positive `rate`, and truncates time from `currencyDate`.
- No `Currency` navigation property — references `CurrencyId` typed ID only.

We introduce two **repository interfaces**:

- `ICurrencyRepository` — async methods: `AddAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`,
  `GetAllAsync()`, `GetAllAsync(int pageSize, int pageNo, string searchString)`, `CountBySearchStringAsync`. File: `ICurrencyRepository.cs`.
- `ICurrencyRateRepository` — async methods: `AddAsync`, `GetRateForDateAsync`. File: `ICurrencyRateRepository.cs`.

Both interfaces do **not** extend `IGenericRepository<T>` because new-style BC repositories are
purpose-built (no `IQueryable` exposure) and own their persistence boundary via `CurrencyDbContext`.

### Infrastructure layer (`ECommerceApp.Infrastructure/Supporting/Currencies/`)

**`CurrencyDbContext`** (`CurrencyDbContext.cs`): a dedicated `DbContext` for this BC.
- Default schema: `"currencies"` (via `builder.HasDefaultSchema(CurrencyConstants.SchemaName)`).
- Exposes `DbSet<Currency> Currencies` and `DbSet<CurrencyRate> CurrencyRates`.
- Scans configurations from `ECommerceApp.Infrastructure.Supporting.Currencies.Configurations` namespace only.

**EF Core configurations** (`Configurations/CurrencyConfiguration.cs`, `Configurations/CurrencyRateConfiguration.cs`):
- `CurrencyId` and `CurrencyRateId` use `HasConversion` with `ValueGeneratedOnAdd`.
- `CurrencyCode` and `CurrencyDescription` use `HasConversion` with appropriate `HasMaxLength`.
- `CurrencyRateConfiguration` defines FK from `CurrencyRate.CurrencyId` to `Currency.Id`
  with no navigation property: `HasOne<Currency>().WithMany().HasForeignKey(cr => cr.CurrencyId)`.
- `CurrencyRateConfiguration` configures `Rate` with `HasPrecision(18, 4)`.

**Repositories** (`internal sealed`):
- `CurrencyRepository` — implements `ICurrencyRepository`; `AsNoTracking()` for all reads.
- `CurrencyRateRepository` — implements `ICurrencyRateRepository`; `AsNoTracking()` for reads.

**DI registration** (`Extensions.cs`): `AddCurrencyInfrastructure(IConfiguration)` registers
`CurrencyDbContext`, `ICurrencyRepository`, and `ICurrencyRateRepository` as scoped.

### Application layer (`ECommerceApp.Application/Supporting/Currencies/`)

**Input DTOs** (records with FluentValidation):
- `CreateCurrencyDto(string Code, string Description)` — validates code length 3, description 3–300 chars.
- `UpdateCurrencyDto(int Id, string Code, string Description)` — same rules plus `Id > 0`.

**Output ViewModels** (implement `IMapFrom<T>` with explicit `ForMember` mapping):
- `CurrencyVm` — maps from `Domain.Supporting.Currencies.Currency`; exposes `Id`, `Code`, `Description`.
- `CurrencyListVm` — paginated container for `List<CurrencyVm>` with `Count`, `PageSize`, `CurrentPage`, `SearchString`.
- `CurrencyRateVm` — maps from `Domain.Supporting.Currencies.CurrencyRate`; exposes `Id`, `CurrencyId`, `Rate`, `CurrencyDate`.

**AutoMapper global converters** (registered in `Application/Mapping/MappingProfile.cs`):
- `CurrencyId → int`, `CurrencyRateId → int`, `CurrencyCode → string`, `CurrencyDescription → string`.

**Services** (`internal sealed`):
- `ICurrencyService` / `CurrencyService` — fully async CRUD and pagination over `ICurrencyRepository`.
- `ICurrencyRateService` / `CurrencyRateService` — fully async NBP integration:
  delegates to `INBPClient.GetCurrencyRateOnDate` with `await`; no `.Result` or `.GetAwaiter().GetResult()`;
  uses `Currency.PlnId` for the PLN short-circuit path.

**DI registration** (`Services/Extensions.cs`): `AddCurrencyServices()` registers
`ICurrencyService` and `ICurrencyRateService` as scoped.

### Parallel change protocol

The new BC is registered **alongside** the legacy services in both
`Application/DependencyInjection.cs` (via `AddCurrencyServices()`) and
`Infrastructure/DependencyInjection.cs` (via `AddCurrencyInfrastructure()`).
Legacy registrations are removed only at atomic switch (Step 7 of bc-implementation.md).
`CurrencyController` still injects the legacy `ICurrencyService` until the switch.

---

## Consequences

### Positive

- `CurrencyCode` enforces the 3-character ISO uppercase rule at construction; invalid codes cannot
  exist in the domain — the duplicated validation in `CurrencyService.Add` and `CurrencyService.Update` is eliminated.
- `CurrencyRate.Create` throws `DomainException` for non-positive rates and null IDs, guaranteeing
  every stored rate is meaningful before it ever reaches the database.
- `CurrencyDbContext` with schema `"currencies"` creates a clear persistence boundary;
  the shared `Context.cs` no longer needs to own `Currency` and `CurrencyRate` data.
- Fully async `CurrencyRateService` unblocks the thread pool during NBP API calls,
  consistent with the `async`/`await` policy in `dotnet-instructions.md §7`.
- `internal sealed` services prevent accidental injection of the wrong implementation and signal
  the correct encapsulation intent to future contributors.
- `Currency.PlnId` centralises the PLN identity constant at the domain level;
  `Application/Constants/CurrencyConstants.cs` can be deleted after the atomic switch.
- 49 new unit tests (all passing) cover value object invariants, entity factory methods,
  service logic, and NBP integration paths with no changes to the 342 pre-existing tests.

### Negative

- Two parallel implementations of the same functionality exist until the atomic switch,
  increasing the surface area of code to maintain during the transition window.
- `CurrencyController.cs` currently uses synchronous `IActionResult` action signatures
  backed by legacy synchronous service methods; the switch will require converting all action
  methods to `async Task<IActionResult>` and adapting to the new DTO/VM contracts.
- The DB migration for the "currencies" schema must be approved and run before integration
  tests or the atomic switch can proceed (per `migration-policy.md`).

### Risks & mitigations

- **Risk**: `ItemService` (legacy) injects `Domain/Interface/ICurrencyRepository` directly.
  Removing the legacy `ICurrencyRepository` registration at switch time will break `ItemService`.
  **Mitigation**: The atomic switch for Currencies must be coordinated with the Catalog BC
  migration (ADR-0007 switch step); `ItemService` must be updated to resolve currency data
  via the new `ICurrencyService` or a dedicated read-model before the switch occurs.
- **Risk**: The "currencies" schema migration runs against the same MSSQL instance as the
  legacy "dbo" tables; a failed migration could leave the schema in a partial state.
  **Mitigation**: Migrations must be reviewed per `migration-policy.md`, run in a transaction,
  and validated against a staging environment before production.
- **Risk**: `EF Core HasConversion` on `CurrencyCode` calls `new CurrencyCode(v)` during
  materialization; an invalid stored value (e.g. a legacy 4-char code) will throw `DomainException`.
  **Mitigation**: The data migration script must ensure all existing currency codes conform to
  the 3-character constraint before the switch. The `CurrencyConfiguration` unique index on
  `Code` prevents future violations at the DB level.

---

## Alternatives considered

- **Reuse `AbstractService<TVm, URepo, EEntity>` for `CurrencyService`** — rejected because
  `AbstractService` is tied to legacy `IGenericRepository<T>` and `Domain/Model` entities.
  The new BC owns its persistence boundary via `CurrencyDbContext`; `AbstractService` cannot
  be parameterised with the new typed repositories without significant changes to the base class.
- **Keep `Currency` and `CurrencyRate` in `Domain/Model/` and add value objects in place** —
  rejected because modifying the legacy domain model would break all existing callers
  (navigation properties are removed, public setters become private) and violates the
  Parallel Change principle required by ADR-0002. The old code must keep working until switch.
- **Merge `ICurrencyRepository` and `ICurrencyRateRepository` into one interface** — rejected
  because `Currency` and `CurrencyRate` have independent lifecycles and query needs.
  The NBP rate service only needs `ICurrencyRateRepository`; the CRUD service only needs
  `ICurrencyRepository`. Merging them would violate the Interface Segregation Principle.
- **Use the shared `Context.cs` with a new EF schema** — rejected because adding a schema
  annotation to the existing `CurrencyConfiguration` in `Infrastructure/Database/Configurations/`
  would affect the running legacy schema and cannot be done without a migration that changes
  the existing table names, breaking backwards compatibility during the parallel period.

---

## Migration plan

**Phase 1 — Parallel implementation (✅ complete)**
All new BC artifacts created: domain, infrastructure, application, unit tests.
New services registered alongside legacy services in DI — no old registrations removed.

**Phase 2 — DB migration (⬜ pending approval)**
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

## Conformance checklist

- [x] All properties on `Domain/Supporting/Currencies/Currency.cs` use `private set`
- [x] All properties on `Domain/Supporting/Currencies/CurrencyRate.cs` use `private set`
- [x] `Currency` has a `private` parameterless constructor for EF Core materialization
- [x] `CurrencyRate` has a `private` parameterless constructor for EF Core materialization
- [x] `Currency.Create(string, string)` is the only public construction path for `Currency`
- [x] `CurrencyRate.Create(CurrencyId, decimal, DateTime)` is the only public construction path for `CurrencyRate`
- [x] `CurrencyRate.Create` throws `DomainException` when `rate <= 0`
- [x] `CurrencyRate.Create` throws `DomainException` when `currencyId` is `null`
- [x] `CurrencyCode` is a `sealed record`; constructor enforces exactly 3 characters and normalises to uppercase
- [x] `CurrencyDescription` is a `sealed record`; constructor enforces non-empty and max 300 characters
- [x] `CurrencyId` and `CurrencyRateId` are `sealed record` types inheriting `TypedId<int>`
- [x] `Domain/Supporting/Currencies/Currency.cs` has no navigation properties to `Payment`, `Order`, or `Item`
- [x] `Domain/Supporting/Currencies/CurrencyRate.cs` has no `Currency` navigation property — `CurrencyId` typed ID only
- [x] `Infrastructure/Supporting/Currencies/CurrencyDbContext.cs` uses default schema `"currencies"`
- [x] `Infrastructure/Supporting/Currencies/Configurations/CurrencyConfiguration.cs` configures `CurrencyId` with `ValueGeneratedOnAdd` and `HasConversion`
- [x] `Infrastructure/Supporting/Currencies/Configurations/CurrencyConfiguration.cs` configures `CurrencyCode` and `CurrencyDescription` with `HasConversion`
- [x] `Infrastructure/Supporting/Currencies/Configurations/CurrencyRateConfiguration.cs` configures `Rate` with `HasPrecision(18, 4)`
- [x] `Infrastructure/Supporting/Currencies/Repositories/CurrencyRepository.cs` is `internal sealed` and uses `AsNoTracking()` for all reads
- [x] `Infrastructure/Supporting/Currencies/Repositories/CurrencyRateRepository.cs` is `internal sealed` and uses `AsNoTracking()` for reads
- [x] `Application/Supporting/Currencies/Services/CurrencyService.cs` is `internal sealed`
- [x] `Application/Supporting/Currencies/Services/CurrencyRateService.cs` is `internal sealed`
- [x] `CurrencyRateService` uses `await` for all `INBPClient` calls — no `.Result` or `.GetAwaiter().GetResult()`
- [x] `CurrencyRateService` uses `Currency.PlnId` for the PLN short-circuit path, not a magic integer
- [x] `Application/Mapping/MappingProfile.cs` contains global converters for `CurrencyId`, `CurrencyRateId`, `CurrencyCode`, and `CurrencyDescription`
- [x] New BC services and repositories are registered **alongside** legacy registrations — legacy registrations are not removed until atomic switch

---

## Implementation Status

| Layer | Status |
|---|---|
| Domain (entities, value objects, typed IDs, repository interfaces) | ✅ Done |
| Infrastructure (CurrencyDbContext, schema, EF configurations, repositories, DI) | ✅ Done |
| Application (DTOs, ViewModels, service interfaces + implementations, validators, DI) | ✅ Done |
| AutoMapper global converters (`MappingProfile.cs`) | ✅ Done |
| Unit tests (value objects, entity factories, services, NBP integration) | ✅ Done |
| DB migration (`InitCurrenciesSchema`) | ⬜ Pending approval |
| Integration tests (`ICurrencyService`, `ICurrencyRateService`) | ⬜ Not started |
| Controller migration (`CurrencyController` → new interfaces) | ⬜ Not started |
| Atomic switch (remove legacy registrations + files) | ⬜ After integration tests |

---

## References

- Related ADRs:
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 — Module Taxonomy and Bounded Context Grouping](0004-module-taxonomy-and-bounded-context-grouping.md)
  - [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](0006-typedid-and-value-objects-as-shared-domain-primitives.md)
  - [ADR-0007 — Catalog BC: Product, Category and Tag Aggregate Design](0007-catalog-bc-product-category-tag-aggregate-design.md)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) §7, §13, §15, §16, §17
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
  - [`.github/instructions/migration-policy.md`](../../.github/instructions/migration-policy.md)
- BC map: [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
