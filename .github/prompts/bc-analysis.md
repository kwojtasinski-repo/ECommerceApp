# Bounded Context Analysis Prompt

> **Usage**: Reference this file in Copilot Chat with `#file:.github/prompts/bc-analysis.md`
> then append: `Analyze the [BC_NAME] bounded context.`
>
> Example: `#file:.github/prompts/bc-analysis.md Analyze the Orders bounded context.`

---

## Core principle — Parallel Change, no breaking behavior

**The existing system must keep working at all times.**

- Do NOT modify existing files to fix coupling, naming, or structure during analysis.
- The new BC is built **in parallel** in the new folder structure (per ADR-0003 + ADR-0004).
- The switch from old to new happens **atomically per BC** — only when the new implementation is complete and tested.
- Cross-BC issues (e.g. `ApplicationUser` in `Order.cs`) are resolved as part of the **owning BC's migration**, not as standalone piecemeal fixes.
- Analysis output is a **migration plan**, not a list of immediate edits.

---

You are analyzing a bounded context in ECommerceApp for migration toward the target architecture
defined in ADR-0002, ADR-0003, and ADR-0004. Follow this analysis structure exactly.

## Step 1 — Inventory existing artifacts

Scan the codebase and list every file that belongs to this BC, grouped by type:

| Type | Current location | File |
|---|---|---|
| Domain model | `Domain/Model/` | |
| Repository interface | `Domain/Interface/` | |
| Service | `Application/Services/<BC>/` | |
| Handler | `Application/Services/<BC>/` | |
| ViewModels | `Application/ViewModels/<BC>/` | |
| DTOs | `Application/DTO/` | |
| Repository impl | `Infrastructure/Repositories/` | |
| EF Configuration | `Infrastructure/Database/Configurations/` | |
| Unit tests | `UnitTests/Services/<BC>/` | |
| Integration tests | `IntegrationTests/Services/` or `IntegrationTests/API/` | |

## Step 2 — Domain model health check

For each domain model found, assess:

| Model | Has behavior? | Public setters? | Nav props to other BCs? | ApplicationUser leak? | Verdict |
|---|---|---|---|---|---|
| `ModelName` | yes/no | yes/no | list them | yes/no | ✅ healthy / 🟡 needs work / 🔴 anemic |

## Step 3 — Coupling hotspots

List every place this BC reaches into another BC directly:

| Location | Reaches into | How | Problem |
|---|---|---|---|
| `ServiceX.MethodY()` | `BC_NAME` | direct service call / nav prop / DbSet | describe |

## Step 4 — Reuse vs change assessment

| Artifact | Action | Reason |
|---|---|---|
| `FileName.cs` | ✅ Reuse as-is | reference domain, no behavior |
| `FileName.cs` | 🟡 Minor fix | remove public setter / remove nav prop |
| `FileName.cs` | 🔴 Refactor | anemic model, needs factory + private setters |
| `FileName.cs` | 🗑️ Delete/merge | duplicate or obsolete |

## Step 5 — New folder mapping (per ADR-0003 + ADR-0004)

Show where each file would live after migration:

```
Current: Domain/Model/[Model].cs
Target:  Domain/[Group]/[BcName]/[Model].cs

Current: Application/Services/[BC]/[Service].cs
Target:  Application/[Group]/[BcName]/Services/[Service].cs

Current: Infrastructure/Repositories/[Repo].cs
Target:  Infrastructure/[Group]/[BcName]/Repositories/[Repo].cs
```

## Step 6 — Missing artifacts

List what needs to be created that doesn't exist yet:

| Missing artifact | Why needed |
|---|---|
| `Domain/[Group]/[BC]/I[Aggregate]Repository.cs` | per ADR-0003 convention |
| Factory method on `[Model]` | per dotnet-instructions.md § 16 |
| Domain method `[Model].[Transition]()` | state transition belongs on aggregate |

## Step 7 — Recommended implementation order

Given the findings, suggest a safe parallel-change implementation order.
**No existing file is touched until the new BC is complete and ready to switch.**

1. Create target folder structure (`Domain/<Group>/<BcName>/`, `Application/...`, `Infrastructure/...`)
2. Create new domain model with private setters + factory methods (new file in new folder)
3. Create new repository interface in new folder
4. Create new repository implementation in new folder
5. Create new service / facade in new folder
6. Register new components in DI **alongside** existing ones (do not remove old registrations yet)
7. Write tests against the new implementation
8. Verify existing tests still pass (nothing broken)
9. **Switch**: update controllers/callers to use new implementation, remove old registrations
10. Delete old files (only after switch is verified green)

## Step 8 — Risk assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Breaking existing tests during parallel build | low | high | new code is additive — existing files untouched until switch |
| DI conflict (old + new registered simultaneously) | medium | medium | use different interface name for new BC until switch is ready |
| EF Core tracking issues after switch | low | medium | verify `Context` configuration for new entity mappings before switching |
| Incomplete parallel implementation switched too early | medium | high | only switch when all new tests pass and old tests still pass |
