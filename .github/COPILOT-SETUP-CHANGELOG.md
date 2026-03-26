# Copilot Setup — Changelog & Current State

> This file tracks **what changed** in the `.github/` Copilot configuration and serves
> as a snapshot of the current setup. Updated by `@copilot-setup-maintainer` or manually.
>
> Use as a quick reference to see what exists without scanning every file.

---

## Current state summary

| Category                               | Count | Details                                                                        |
| -------------------------------------- | ----- | ------------------------------------------------------------------------------ |
| `copilot-instructions.md`              | 1     | ≤ 4,000 chars (3,724), repo-level policy                                       |
| Instruction files (`.instructions.md`) | 12    | All with `applyTo:` frontmatter (includes copilot-config-sync)                 |
| Prompt files (`.prompt.md`)            | 3     | BC analysis, BC implementation, PR review                                      |
| Agent files                            | 4     | adr-generator, bc-switch, code-reviewer, copilot-setup-maintainer              |
| Skills (`SKILL.md`)                    | 8     | Scaffolding templates for common artifacts                                     |
| Context files                          | 5     | project-state, known-issues, repo-index, future-skills, anti-patterns-critical |

---

## File inventory

### `.github/instructions/` (12 files)

| File                                  | `applyTo:`                                                    | Added                                           |
| ------------------------------------- | ------------------------------------------------------------- | ----------------------------------------------- |
| `dotnet.instructions.md`              | `**/*.cs, **/*.csproj`                                        | Session 1 (renamed)                             |
| `efcore.instructions.md`              | `ECommerceApp.Infrastructure/**/*.cs, **/*.csproj`            | Session 1 (renamed)                             |
| `frontend.instructions.md`            | `ECommerceApp.Web/wwwroot/**, **/*.cshtml`                    | Session 1 (renamed)                             |
| `razorpages.instructions.md`          | `ECommerceApp.Web/**/*.cshtml, **/*.cshtml.cs, **/*.cs`       | Session 1 (renamed)                             |
| `web-api.instructions.md`             | `ECommerceApp.API/**/*.cs`                                    | Session 1 (renamed)                             |
| `testing.instructions.md`             | `ECommerceApp.UnitTests/**, ECommerceApp.IntegrationTests/**` | Session 1 (renamed)                             |
| `migration-policy.instructions.md`    | `ECommerceApp.Infrastructure/Migrations/**`                   | Session 1 (renamed)                             |
| `shared-primitives.instructions.md`   | `ECommerceApp.Domain/Shared/**/*.cs`                          | Pre-existing                                    |
| `safety.instructions.md`              | `**`                                                          | Session 1 (extracted from copilot-instructions) |
| `pre-edit.instructions.md`            | `**`                                                          | Session 1 (extracted from copilot-instructions) |
| `docs-index.instructions.md`          | `**`                                                          | Session 1 (new — docs lookup table)             |
| `copilot-config-sync.instructions.md` | `.github/**`                                                  | Session 11 (new — auto-sync trigger)            |

### `.github/prompts/` (3 files)

| File                          | Added               |
| ----------------------------- | ------------------- |
| `bc-analysis.prompt.md`       | Session 1 (renamed) |
| `bc-implementation.prompt.md` | Session 1 (renamed) |
| `pr-review.prompt.md`         | Session 1 (renamed) |

### `.github/agents/` (4 files)

| File                          | Added                       |
| ----------------------------- | --------------------------- |
| `adr-generator.md`            | Pre-existing (refs updated) |
| `bc-switch.md`                | Pre-existing                |
| `code-reviewer.md`            | Session 3 (new)             |
| `copilot-setup-maintainer.md` | Session 1 (new)             |

### `.github/skills/` (8 skills)

| Skill                     | Description                                    | Added     |
| ------------------------- | ---------------------------------------------- | --------- |
| `create-unit-test`        | xUnit test class (service, aggregate, handler) | Session 2 |
| `create-dbcontext`        | Per-BC DbContext (4-file scaffold)             | Session 2 |
| `create-ef-configuration` | EF Core entity configuration                   | Session 2 |
| `create-di-extension`     | Application + Infrastructure DI extensions     | Session 2 |
| `create-domain-event`     | Cross-BC IMessage + IMessageHandler (3 modes)  | Session 2 |
| `create-integration-test` | Integration test with BaseTest + Shouldly      | Session 2 |
| `create-http-scenario`    | .http file for any API endpoint testing        | Session 2 |
| `create-validator`        | FluentValidation AbstractValidator             | Session 2 |

### `.github/context/` (5 files)

| File                                | Added                                             |
| ----------------------------------- | ------------------------------------------------- |
| `project-state.md`                  | Pre-existing                                      |
| `known-issues.md`                   | Pre-existing                                      |
| `repo-index.md`                     | Session 2 (new — full codebase map)               |
| `future-skills.md`                  | Session 2 (new — skills roadmap)                  |
| `anti-patterns-critical.context.md` | Session 3 (renamed from anti-patterns.context.md) |

---

## Change log

### Session 12 — Project status sync (2026-03-26)

| #   | Change                                                                                                                          | Files affected                                              |
| --- | ------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| 1   | Fixed `copilot-instructions.md` §2 instruction file count: 11 → 12 (copilot-config-sync was not counted)                       | `.github/copilot-instructions.md`                           |
| 2   | Fixed `project-state.md` date: `2026-05-27` (typo) → `2026-03-26`; added IAM + refresh token active work row                  | `.github/context/project-state.md`                          |
| 3   | Updated `iam-refresh-token.md`: status Planned → 🟡 In progress; Steps 1–4 marked done (entity, infra, service, unit tests)     | `docs/roadmap/iam-refresh-token.md`                         |
| 4   | Updated `iam-atomic-switch.md`: status expanded; added refresh token + Area controller rows to "already done" table             | `docs/roadmap/iam-atomic-switch.md`                         |
| 5   | Fixed changelog Session 5 date: `2026-06-27` (typo) → `2026-03-17`                                                             | `.github/COPILOT-SETUP-CHANGELOG.md`                        |
| 6   | Updated `repo-index.md`: date, Razor views 153→176, test counts, IAM section (Area controller + RefreshToken), Jobs/TimeManagement section (Area controller) | `.github/context/repo-index.md` |

### Session 11 — Setup audit, anti-patterns rename, trigger phrases (2026-03-28)

| #   | Change                                                                                                        | Files affected                                      |
| --- | ------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| 1   | Renamed `anti-patterns.context.md` → `anti-patterns-critical.context.md` (both guides prescribe split naming) | `.github/context/anti-patterns-critical.context.md` |
| 2   | Updated all references to old filename (code-reviewer, docs-index, copilot-config-sync, maintainer)           | 4 files                                             |
| 3   | Added YAML frontmatter + trigger phrases to `copilot-setup-maintainer.md`                                     | `.github/agents/copilot-setup-maintainer.md`        |
| 4   | Added trigger phrases to all 4 agent YAML descriptions                                                        | `.github/agents/*.md`                               |
| 5   | Fixed instruction file count 11→12 in changelog (copilot-config-sync was missing)                             | `.github/COPILOT-SETUP-CHANGELOG.md`                |
| 6   | Added `copilot-config-sync.instructions.md` to instruction file inventory                                     | `.github/COPILOT-SETUP-CHANGELOG.md`                |

### Session 10 — IAM refresh token roadmap + docs-index sync (2026-03-27)

| #   | Change                                                                                                                                             | Files affected                                    |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| 1   | Created `docs/roadmap/iam-refresh-token.md` — 8-step plan, design decisions settled (DB storage, Jti binding, rotation-on-use, deferred SessionId) | `docs/roadmap/iam-refresh-token.md`               |
| 2   | Added `iam-refresh-token.md` to active roadmaps table in `README.md`                                                                               | `docs/roadmap/README.md`                          |
| 3   | Removed stale "IAM refresh token" entry from Deferred section (now tracked as active roadmap)                                                      | `docs/roadmap/README.md`                          |
| 4   | Removed stale "Bootstrap 5 upgrade" entry from Deferred section (completed — ADR-0023)                                                             | `docs/roadmap/README.md`                          |
| 5   | Added `iam-refresh-token.md` row to docs-index roadmap table                                                                                       | `.github/instructions/docs-index.instructions.md` |
| 6   | Updated changelog with Session 10 entry                                                                                                            | `.github/COPILOT-SETUP-CHANGELOG.md`              |

### Session 9 — HTTP scenarios refresh + .sln sync (2026-03-25)

| #   | Change                                                                                                    | Files affected                                        |
| --- | --------------------------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| 1   | Deleted 9 stale `*-v2.http` scenario files (routes were `api/v2/...`)                                     | `ECommerceApp.API/HttpScenarios/`                     |
| 2   | Created `payments.http` — `GET api/payments/{paymentId:guid}` (own-scoped by Guid token)                  | `ECommerceApp.API/HttpScenarios/payments.http`        |
| 3   | Created `presale.http` — `api/cart`, `api/checkout` (removed price-changes step; confirm returns orderId) | `ECommerceApp.API/HttpScenarios/presale.http`         |
| 4   | Created `sales-orders.http` — `GET api/orders/{id}` with paymentUrl in response                           | `ECommerceApp.API/HttpScenarios/sales-orders.http`    |
| 5   | Created `catalog.http` — `api/items` published-only GET list + GET {id}                                   | `ECommerceApp.API/HttpScenarios/catalog.http`         |
| 6   | Created `account-profile.http` — `api/customers` + `api/addresses` (POST/PUT/DELETE)                      | `ECommerceApp.API/HttpScenarios/account-profile.http` |
| 7   | Created `refunds.http` — `api/refunds` GET {id} + POST (removed admin approve/reject)                     | `ECommerceApp.API/HttpScenarios/refunds.http`         |
| 8   | Created `currencies.http` — kept `api/v2/currencies` (admin controller, not part of BC switch)            | `ECommerceApp.API/HttpScenarios/currencies.http`      |
| 9   | Created `inventory.http` — kept `api/v2/inventory` (admin controller, not part of BC switch)              | `ECommerceApp.API/HttpScenarios/inventory.http`       |
| 10  | Created `jobs.http` — kept `api/v2/jobs` (admin controller, not part of BC switch)                        | `ECommerceApp.API/HttpScenarios/jobs.http`            |
| 11  | Added `docs\reports\web-ui-views-report.md` to `reports` solution folder                                  | `ECommerceApp.sln`                                    |
| 12  | Added `HttpScenarios` solution folder with all 9 `.http` files as solution items                          | `ECommerceApp.sln`                                    |

### Session 8 — Full audit & repo-index sync (2026-03-23)

| #   | Change                                                                                    | Files affected                       |
| --- | ----------------------------------------------------------------------------------------- | ------------------------------------ |
| 1   | Added `docs\reference\endpoint-map.md` (and new `reference` solution folder) to solution  | `ECommerceApp.sln`                   |
| 2   | Updated repo-index At a Glance: Test files 156→161 (integration 60→65)                    | `.github/context/repo-index.md`      |
| 3   | Updated repo-index Solution Projects: UnitTests 76→96 files, IntegrationTests 42→65 files | `.github/context/repo-index.md`      |
| 4   | Updated repo-index section headings: UnitTests 76→96, IntegrationTests 42→65              | `.github/context/repo-index.md`      |
| 5   | Updated changelog with Session 8 entry                                                    | `.github/COPILOT-SETUP-CHANGELOG.md` |

### Session 7 — Full audit & trim (2026-03-22)

| #   | Change                                                                                                            | Files affected                       |
| --- | ----------------------------------------------------------------------------------------------------------------- | ------------------------------------ |
| 1   | Trimmed `copilot-instructions.md` from 5,506 → 3,724 chars: condensed §2 bullets, §4 BC rule, §6 arch rule, §7–§9 | `.github/copilot-instructions.md`    |
| 2   | Fixed ADR count 23 → 25 in §2 of `copilot-instructions.md`                                                        | `.github/copilot-instructions.md`    |
| 3   | Added ADR-0025 to `adr` solution folder                                                                           | `ECommerceApp.sln`                   |
| 4   | Updated repo-index At a Glance: ADRs 24→25, Razor views 134→153, JS modules 10→11                                 | `.github/context/repo-index.md`      |
| 5   | Updated changelog current state summary and added Session 7 entry                                                 | `.github/COPILOT-SETUP-CHANGELOG.md` |

### Session 6 — ADR-0024 + full audit (2026-03-22)

| #   | Change                                                                                 | Files affected                                    |
| --- | -------------------------------------------------------------------------------------- | ------------------------------------------------- |
| 1   | Created ADR-0024 (controller routing strategy: Areas for Web, in-place swap for API)   | `docs/adr/0024-controller-routing-strategy.md`    |
| 2   | Added ADR-0024 row to docs-index ADR table                                             | `.github/instructions/docs-index.instructions.md` |
| 3   | Added ADR-0024 routing strategy note to roadmap README                                 | `docs/roadmap/README.md`                          |
| 4   | Updated `orders-atomic-switch.md` Steps 3–4 to Area-based approach + ADR-0024 ref      | `docs/roadmap/orders-atomic-switch.md`            |
| 5   | Updated `payments-atomic-switch.md` Step 3 to Area-based approach + ADR-0024 ref       | `docs/roadmap/payments-atomic-switch.md`          |
| 6   | Added routing strategy as key architectural invariant                                  | `.github/context/project-state.md`                |
| 7   | ADR-0024 added to `ECommerceApp.sln` `adr` solution folder (auto by VS on file create) | `ECommerceApp.sln`                                |
| 8   | Full audit (Workflow 6 + Workflow 8): repo-index At a Glance metrics updated           | `.github/context/repo-index.md`                   |
| 9   | Updated changelog counts and added Session 6 entry                                     | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 5 — Full audit & .sln sync (2026-03-17)

| #   | Change                                                                         | Files affected                                    |
| --- | ------------------------------------------------------------------------------ | ------------------------------------------------- |
| 1   | Added ADR-0022 (Navbar) and ADR-0023 (Bootstrap 5) to sln adr folder           | `ECommerceApp.sln`                                |
| 2   | Added `code-reviewer.md` to sln agents folder                                  | `ECommerceApp.sln`                                |
| 3   | Added `anti-patterns.context.md` to sln context folder                         | `ECommerceApp.sln`                                |
| 4   | Created `reports` solution folder; added `cross-bc-integration-test-report.md` | `ECommerceApp.sln`                                |
| 5   | Added `docs/reports/` section to docs-index                                    | `.github/instructions/docs-index.instructions.md` |
| 6   | Updated changelog with Session 5                                               | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 4 — Drift audit & sync (2026-03-17)

| #   | Change                                          | Files affected                                    |
| --- | ----------------------------------------------- | ------------------------------------------------- |
| 1   | Fixed ADR count 21 → 23 in copilot-instructions | `.github/copilot-instructions.md`                 |
| 2   | Added missing ADR-0022 row to docs-index        | `.github/instructions/docs-index.instructions.md` |
| 3   | Updated changelog with Session 4                | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 3 — Code reviewer & anti-patterns (2026-03-17)

| #   | Change                                               | Files affected                                    |
| --- | ---------------------------------------------------- | ------------------------------------------------- |
| 1   | Created consolidated anti-patterns context file      | `.github/context/anti-patterns.context.md`        |
| 2   | Created code-reviewer agent                          | `.github/agents/code-reviewer.md`                 |
| 3   | Added repo-index verification workflow to maintainer | `.github/agents/copilot-setup-maintainer.md`      |
| 4   | Updated docs-index with new agent and context file   | `.github/instructions/docs-index.instructions.md` |
| 5   | Updated agent count in copilot-instructions          | `.github/copilot-instructions.md`                 |
| 6   | Updated changelog counts and inventory               | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 2 — Skills & codebase index (2026-03-15)

| #   | Change                                          | Files affected                     |
| --- | ----------------------------------------------- | ---------------------------------- |
| 1   | Created full codebase index                     | `.github/context/repo-index.md`    |
| 2   | Created skills roadmap                          | `.github/context/future-skills.md` |
| 3   | Created 8 SKILL.md files                        | `.github/skills/` (8 folders)      |
| 4   | Added skills section to docs-index              | `docs-index.instructions.md`       |
| 5   | Added skills listing to copilot-instructions    | `copilot-instructions.md` §2       |
| 6   | Updated changelog to include current state      | `COPILOT-SETUP-CHANGELOG.md`       |
| 7   | Added changelog maintenance to setup-maintainer | `copilot-setup-maintainer.md`      |

### Session 1 — Initial setup overhaul

| #   | Change                                                                   | Files affected                                                                         |
| --- | ------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| 1   | Renamed 7 instruction files to `.instructions.md`                        | `dotnet`, `efcore`, `frontend`, `razorpages`, `web-api`, `testing`, `migration-policy` |
| 2   | Renamed 3 prompt files to `.prompt.md`                                   | `bc-analysis`, `bc-implementation`, `pr-review`                                        |
| 3   | Trimmed `copilot-instructions.md` from 11,689 → 3,984 chars              | `.github/copilot-instructions.md`                                                      |
| 4   | Created `safety.instructions.md` (extracted from copilot-instructions)   | `.github/instructions/safety.instructions.md`                                          |
| 5   | Created `pre-edit.instructions.md` (extracted from copilot-instructions) | `.github/instructions/pre-edit.instructions.md`                                        |
| 6   | Created `docs-index.instructions.md` (docs lookup table)                 | `.github/instructions/docs-index.instructions.md`                                      |
| 7   | Updated usage hints in prompt files                                      | All 3 `.prompt.md` files                                                               |
| 8   | Fixed all cross-references (20+ broken filenames)                        | Multiple files                                                                         |
| 9   | Created `@copilot-setup-maintainer` agent                                | `.github/agents/copilot-setup-maintainer.md`                                           |

---

## Risks & rollback

- **All changes are additive** — no application code touched.
- **Git rollback**: `git checkout HEAD -- .github/` reverts everything.
- **IDE requirements**: `applyTo:` frontmatter requires VS Code ≥ 1.99 or Visual Studio 17.14+.
