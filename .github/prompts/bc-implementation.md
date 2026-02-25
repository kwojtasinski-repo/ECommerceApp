# Bounded Context Implementation Prompt

> **Usage**: Reference this file in Copilot Chat with `#file:.github/prompts/bc-implementation.md`
> then append: `Implement the [BC_NAME] bounded context.`
>
> Example:
> ```
> #file:.github/prompts/bc-implementation.md
> #file:.github/prompts/bc-analysis.md
> Implement the Inventory/Availability bounded context.
> ```
>
> Always run `bc-analysis.md` first to understand what exists before implementing.

---

## Core principles — read before writing any code

1. **Parallel Change — never break existing behavior.**
   Existing code is not touched. New BC is built completely in parallel.
   The switch from old to new happens atomically per BC when the new implementation is complete.

2. **Full OOP — rich domain model.**
   Every behavioral aggregate gets: private setters, factory methods, state transition methods,
   domain events returned from methods. No anemic models. No external state mutation.

3. **Folder-first — create structure before writing code.**
   Create the target folder structure (per ADR-0003 + ADR-0004) as the very first step.

4. **Patient implementation — one artifact at a time.**
   Follow the order below. Do not skip steps. Verify build after each group of changes.

5. **Reference patterns — use `docs/patterns/implementation-patterns.md`.**
   Every pattern (Aggregate, Value Object, Facade, Repository, etc.) has a template there.
   Adapt to this codebase — do not copy blindly.

---

## Implementation order (follow exactly)

### Step 1 — Create folder structure

Create empty folders for the BC under all three layers.
No code yet — folders only.

```
ECommerceApp.Domain/<Group>/<BcName>/
ECommerceApp.Application/<Group>/<BcName>/Services/
ECommerceApp.Application/<Group>/<BcName>/ViewModels/
ECommerceApp.Application/<Group>/<BcName>/DTOs/
ECommerceApp.Infrastructure/<Group>/<BcName>/Repositories/
ECommerceApp.Infrastructure/<Group>/<BcName>/Configurations/
```

If a dedicated ADR exists for this BC (e.g. `ADR-00XX`), add a `## Implementation Status` table
at the end of that ADR now — all rows start as `⬜ Not started`. Update each row to `✅ Done`
as each step below is completed. The standard rows are:

| Layer | Status |
|---|---|
| Domain (aggregate, value objects, domain events, repository interface) | ⬜ Not started |
| Infrastructure (DbContext, schema, EF configs, repository, DI) | ⬜ Not started |
| Application (DTOs, ViewModels, service interface + impl, validators, DI) | ⬜ Not started |
| Unit tests | ⬜ Not started |
| DB migration | ⬜ Pending approval |
| Integration tests | ⬜ Not started |
| Controller migration | ⬜ Not started |
| Atomic switch | ⬜ After integration tests |

### Step 2 — Domain layer (pure, no dependencies)

Create in order — each depends on the previous:

1. **Value objects** (if any) — `record` types, immutable, self-validating
2. **Strongly-typed IDs** (if any) — `record` wrapping `int` or `Guid`
3. **Aggregate** — private setters, `private` parameterless ctor for EF Core,
   `static Create(...)` factory, state transition methods returning domain events
4. **Domain events** — `record` types, past tense, immutable
5. **Repository interface** — extends `IGenericRepository<T>`, BC-specific queries only

Rules for this step:
- No `ApplicationUser` navigation properties — `string UserId` only
- No navigation properties to aggregates from other BCs — use IDs (int/Guid)
- No Law of Demeter chains — pass values as parameters
- All state transitions return a domain event or `void` — never `void` for mutations
  that other code needs to react to

### Step 3 — Infrastructure layer

1. **EF Core configuration** — `IEntityTypeConfiguration<T>` with full fluent API config
   (table name, column names, constraints, value converters for strongly-typed IDs,
   `RowVersion` for optimistic locking if needed)
2. **Repository implementation** — extends `GenericRepository<T>`, implements BC interface,
   `internal sealed`, `AsNoTracking()` for reads
3. **Register** in `Infrastructure/DependencyInjection.cs`

### Step 4 — Application layer

1. **DTOs** — input/output contracts (`record` or `class`, depends on usage)
2. **ViewModels** — MVC view models (`class`, implements `IMapFrom<T>`)
3. **AutoMapper profile** — if ViewModels use `IMapFrom<T>`, register in existing
   `MappingProfile` or create a new profile for this BC
4. **FluentValidation validators** — for all input DTOs/VMs that come from the user
5. **Service interface** — public contract
6. **Service implementation** — `internal sealed`, orchestrates repository + aggregate methods,
   maps to VM/DTO, throws `BusinessException` for violations
7. **Register** in `Application/DependencyInjection.cs`

### Step 5 — Build verification

Run build. Fix all compilation errors before proceeding.
The build must be green at this point — existing tests must still pass.

### Step 6 — Tests

**Unit tests** (`ECommerceApp.UnitTests/<Group>/<BcName>/`):
- Test aggregate methods directly (not through the service)
- Test value object validation
- Test domain event is returned correctly
- Use `Method_Conditions_ExpectedResult` naming — e.g. `MarkAsPaid_AlreadyPaidOrder_ShouldThrowBusinessException()`
- Use `FluentAssertions`

**Integration tests** (`ECommerceApp.IntegrationTests/`):
- Test service methods via `BaseTest<TService>` (service-level)
  or via `CustomWebApplicationFactory` (API-level)
- Cover happy path + domain violation cases

### Step 7 — Switch (only when ready)

Only perform this step when:
- All new unit tests pass
- All new integration tests pass
- All existing tests still pass

Switch steps:
1. Update controllers / API controllers to inject the new service interface
2. Remove old DI registrations (old service + old repository)
3. Run full test suite
4. Update `## Implementation Status` table in the BC's ADR (mark controller migration and atomic switch as ✅ Done)
5. Update `docs/architecture/bounded-context-map.md` progress tracker — move BC from **Completed (switch pending)** if fully switched

---

## Checklist

### Domain layer
- [ ] Folders created under `Domain/<Group>/<BcName>/`
- [ ] Aggregate rules followed — see [`dotnet-instructions.md §16`](../instructions/dotnet-instructions.md)
- [ ] Repository interface extends `IGenericRepository<T>`

### Infrastructure layer
- [ ] Folders created under `Infrastructure/<Group>/<BcName>/`
- [ ] EF Core configuration file created
- [ ] Repository registered — see [`dotnet-instructions.md §13`](../instructions/dotnet-instructions.md)

### Application layer
- [ ] Folders created under `Application/<Group>/<BcName>/`
- [ ] Service implementation is `internal sealed`
- [ ] Service never returns domain entities — maps to VM/DTO
- [ ] Service registered — see [`dotnet-instructions.md §13`](../instructions/dotnet-instructions.md)

### Tests
- [ ] Unit tests for aggregate methods
- [ ] Unit tests for value object edge cases
- [ ] Integration test for service happy path
- [ ] Integration test for domain violation (BusinessException)
- [ ] All existing tests still pass

### Switch
- [ ] Controllers updated to use new service
- [ ] Old registrations removed
- [ ] `## Implementation Status` in BC ADR updated (all rows ✅ Done)
- [ ] Progress tracker updated in `bounded-context-map.md`

---

## References

- Pattern templates: [`docs/patterns/implementation-patterns.md`](../../docs/patterns/implementation-patterns.md)
- Folder/namespace rules: [ADR-0003](../../docs/adr/0003-feature-folder-organization-for-new-bounded-context-code.md)
- Module taxonomy: [ADR-0004](../../docs/adr/0004-module-taxonomy-and-bounded-context-grouping.md)
- Rich domain model rules: [`.github/instructions/dotnet-instructions.md`](../instructions/dotnet-instructions.md) § 16
- BC map + progress tracker: [`docs/architecture/bounded-context-map.md`](../../docs/architecture/bounded-context-map.md)
