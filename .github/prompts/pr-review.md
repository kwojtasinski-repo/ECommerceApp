# PR Review Prompt

> **Usage**: Reference this file in Copilot Chat with `#file:.github/prompts/pr-review.md`
> then append: `Review this PR.` or `Review the changes in [area].`
>
> Example:
> ```
> #file:.github/prompts/pr-review.md
> Review the changes in the Catalog BC.
> ```
>
> This prompt loads only the ADRs and instruction files relevant to the PR scope.
> Do NOT load all ADRs blindly — identify scope first (Step 1), then load (Step 2).

---

## Core principle — Review for conformance, not style

- The goal is to catch **architectural violations** and **missing tests**, not reformat code.
- Do NOT suggest moving existing files for structural reasons — that requires an explicit ADR.
- Do NOT propose changes outside the PR scope.
- Flag violations with the specific rule source (ADR number or instruction file section).

---

## Review steps (follow in order)

### Step 1 — Identify PR scope

Scan the changed files and determine:

1. **Which layers are touched?**
   - `Domain/` — domain model changes
   - `Application/` — service / DTO / ViewModel changes
   - `Infrastructure/` — repository / EF config / migration changes
   - `Web/` — MVC controller / View / Razor Pages changes
   - `API/` — Web API controller changes
   - `UnitTests/` / `IntegrationTests/` — test-only changes

2. **Which bounded contexts are touched?**
   Map changed paths to BC names using the table below:

   | Changed path prefix | BC |
   |---|---|
   | `Domain/Catalog/`, `Application/Catalog/`, `Infrastructure/Catalog/` | Catalog |
   | `Domain/AccountProfile/`, `Application/AccountProfile/`, `Infrastructure/AccountProfile/` | AccountProfile |
   | `Domain/Identity/`, `Application/Identity/`, `Infrastructure/Identity/` | Identity/IAM |
   | `Domain/Shared/` | Shared primitives |
   | `Domain/Model/`, `Application/Services/`, `Infrastructure/Repositories/` | Legacy horizontal code |
   | `Infrastructure/Migrations/` | DB migration |

3. **Is this a parallel-change PR or a switch PR?**
   - Parallel-change: adds new code alongside existing, does not remove old registrations
   - Switch: removes old DI registrations, updates controllers to use new service

### Step 2 — Load relevant ADRs

Load **only** the ADRs that apply to the scope identified in Step 1:

| Condition | Load |
|---|---|
| Any new code in feature-folder structure | [ADR-0003](../../docs/adr/0003-feature-folder-organization-for-new-bounded-context-code.md) |
| Any BC boundary change or parallel-change PR | [ADR-0002](../../docs/adr/0002-post-event-storming-architectural-evolution-strategy.md) |
| BC grouping or namespace question | [ADR-0004](../../docs/adr/0004-module-taxonomy-and-bounded-context-grouping.md) |
| AccountProfile BC touched | [ADR-0005](../../docs/adr/0005-accountprofile-bc-userprofile-aggregate-design.md) |
| `Domain/Shared/` or typed IDs / VOs touched | [ADR-0006](../../docs/adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md) |
| Catalog BC touched | [ADR-0007](../../docs/adr/0007-catalog-bc-product-category-tag-aggregate-design.md) |
| `Infrastructure/Migrations/` touched | [`migration-policy.md`](../instructions/migration-policy.md) |

### Step 3 — Architectural conformance

**3a. Tick the ADR conformance checklist first.**
For each ADR loaded in Step 2, read its `## Conformance checklist` section and verify every item
using `search/textSearch` or `read/readFile` on the changed files.
Report each item as `[OK]` or `[VIOLATION]` using the format in the Review output section below.
If the ADR has no `## Conformance checklist` section, fall back to 3b.

**3b. Generic architectural checks (apply when no ADR-specific checklist exists).**

Check the following for all changed files:

**Parallel change rules** (applies to any BC-touching PR):
- New code is additive — no existing files deleted or moved in this PR
- Old DI registrations not removed before switch is ready
- If this is a switch PR: all new tests pass and are verified before old code is removed

**Folder and namespace rules** (ADR-0003):
- New code lives under `Domain/<Group>/<BcName>/`, `Application/<Group>/<BcName>/`, `Infrastructure/<Group>/<BcName>/`
- Namespace matches folder path exactly
- No new files added to legacy horizontal folders (`Domain/Model/`, `Application/Services/`, `Infrastructure/Repositories/`) unless explicitly continuing existing legacy work

**Domain model rules** (for behavioral aggregates — `dotnet-instructions.md §16`):
- All properties use `private set`
- Static `Create(...)` factory method present
- `private` parameterless constructor present for EF Core
- State transitions are methods on the aggregate, not external mutations
- No `ApplicationUser` navigation property — `string UserId` only
- No cross-BC navigation properties — IDs only

**Service rules** (`dotnet-instructions.md §2–§3`):
- Reference/lookup services inherit `AbstractService`
- Complex domain operations use the Handler pattern — not duplicated in controllers
- Services are `internal sealed`; exposed only via interface

### Step 4 — Per-stack code quality

Load and check **only** the instruction file matching the changed stack:

| Changed files | Check against |
|---|---|
| `ECommerceApp.API/**/*.cs` | [`web-api-instructions.md`](../instructions/web-api-instructions.md) |
| `ECommerceApp.Web/**/*.cs`, `**/*.cshtml` | [`razorpages-instructions.md`](../instructions/razorpages-instructions.md) |
| `ECommerceApp.Infrastructure/**/*.cs` | [`efcore-instructions.md`](../instructions/efcore-instructions.md) |
| `wwwroot/**`, `*.cshtml` (frontend assets) | [`frontend-instructions.md`](../instructions/frontend-instructions.md) |
| Any `.cs` file | [`dotnet-instructions.md`](../instructions/dotnet-instructions.md) §4–§15 |

### Step 5 — Tests

- [ ] Unit tests added for any new aggregate method or value object
- [ ] Integration tests added or updated for any new service method
- [ ] No existing tests broken (check test project for compilation errors)
- [ ] Test naming follows `Method_Conditions_ExpectedResult` — see [`testing-instructions.md`](../instructions/testing-instructions.md)
- [ ] API integration tests use Flurl + Shouldly, NOT `BaseTest<T>`

### Step 6 — Migrations (if `Infrastructure/Migrations/` is touched)

- [ ] Migration created via `dotnet ef migrations add` — not hand-edited
- [ ] Migration class has a header comment describing intent and data impact
- [ ] SQL preview (`dotnet ef migrations script`) included in PR description
- [ ] Rollback plan documented in PR description
- [ ] Tagged `@team/architecture` and DB owners for approval
- [ ] Integration tests cover the schema change where feasible

---

## Review output format

For each violation found, report:

```
[VIOLATION] <short description>
File: <path>
Rule: <ADR number or instruction file + section>
Suggestion: <what to change>
```

For items that look correct, confirm:

```
[OK] <area> — conforms to <ADR/rule source>
```

---

## Checklist

### Architecture
- [ ] No existing files moved without an explicit ADR
- [ ] New code uses feature-folder structure (ADR-0003)
- [ ] BC grouping matches module taxonomy (ADR-0004)
- [ ] Parallel change intact — old registrations not removed before switch

### Domain model (behavioral aggregates only)
- [ ] Private setters on all properties
- [ ] Static `Create(...)` factory method
- [ ] State transitions are aggregate methods
- [ ] No `ApplicationUser` nav prop; no cross-BC nav props

### Application & Infrastructure
- [ ] Services are `internal sealed` behind interface
- [ ] No domain entities returned from services — VMs/DTOs only
- [ ] `AsNoTracking()` on all read-only queries
- [ ] DI registered in layer `DependencyInjection.cs`, not `Startup.cs`

### Tests
- [ ] Unit tests for new aggregate/VO behavior
- [ ] Integration tests for new service paths
- [ ] All existing tests still pass
- [ ] Naming follows `Method_Conditions_ExpectedResult`

### Migrations (if applicable)
- [ ] Approval checklist complete (see migration-policy.md)
- [ ] Rollback plan in PR description

---

## References

- Parallel change strategy: [ADR-0002](../../docs/adr/0002-post-event-storming-architectural-evolution-strategy.md)
- Folder organization: [ADR-0003](../../docs/adr/0003-feature-folder-organization-for-new-bounded-context-code.md)
- Module taxonomy: [ADR-0004](../../docs/adr/0004-module-taxonomy-and-bounded-context-grouping.md)
- Domain model rules: [`dotnet-instructions.md §16`](../instructions/dotnet-instructions.md)
- BC map: [`bounded-context-map.md`](../../docs/architecture/bounded-context-map.md)
