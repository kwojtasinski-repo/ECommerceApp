# Copilot Setup — Changelog & Current State

> This file tracks **what changed** in the `.github/` Copilot configuration and serves
> as a snapshot of the current setup. Updated by `@copilot-setup-maintainer` or manually.
>
> Use as a quick reference to see what exists without scanning every file.

---

## Current state summary

| Category                               | Count | Details                                                               |
| -------------------------------------- | ----- | --------------------------------------------------------------------- |
| `copilot-instructions.md`              | 1     | ≤ 4,000 chars, repo-level policy                                      |
| Instruction files (`.instructions.md`) | 11    | All with `applyTo:` frontmatter                                       |
| Prompt files (`.prompt.md`)            | 3     | BC analysis, BC implementation, PR review                             |
| Agent files                            | 4     | adr-generator, bc-switch, code-reviewer, copilot-setup-maintainer     |
| Skills (`SKILL.md`)                    | 8     | Scaffolding templates for common artifacts                            |
| Context files                          | 5     | project-state, known-issues, repo-index, future-skills, anti-patterns |

---

## File inventory

### `.github/instructions/` (11 files)

| File                                | `applyTo:`                                                    | Added                                           |
| ----------------------------------- | ------------------------------------------------------------- | ----------------------------------------------- |
| `dotnet.instructions.md`            | `**/*.cs, **/*.csproj`                                        | Session 1 (renamed)                             |
| `efcore.instructions.md`            | `ECommerceApp.Infrastructure/**/*.cs, **/*.csproj`            | Session 1 (renamed)                             |
| `frontend.instructions.md`          | `ECommerceApp.Web/wwwroot/**, **/*.cshtml`                    | Session 1 (renamed)                             |
| `razorpages.instructions.md`        | `ECommerceApp.Web/**/*.cshtml, **/*.cshtml.cs, **/*.cs`       | Session 1 (renamed)                             |
| `web-api.instructions.md`           | `ECommerceApp.API/**/*.cs`                                    | Session 1 (renamed)                             |
| `testing.instructions.md`           | `ECommerceApp.UnitTests/**, ECommerceApp.IntegrationTests/**` | Session 1 (renamed)                             |
| `migration-policy.instructions.md`  | `ECommerceApp.Infrastructure/Migrations/**`                   | Session 1 (renamed)                             |
| `shared-primitives.instructions.md` | `ECommerceApp.Domain/Shared/**/*.cs`                          | Pre-existing                                    |
| `safety.instructions.md`            | `**`                                                          | Session 1 (extracted from copilot-instructions) |
| `pre-edit.instructions.md`          | `**`                                                          | Session 1 (extracted from copilot-instructions) |
| `docs-index.instructions.md`        | `**`                                                          | Session 1 (new — docs lookup table)             |

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

| File                       | Added                                        |
| -------------------------- | -------------------------------------------- |
| `project-state.md`         | Pre-existing                                 |
| `known-issues.md`          | Pre-existing                                 |
| `repo-index.md`            | Session 2 (new — full codebase map)          |
| `future-skills.md`         | Session 2 (new — skills roadmap)             |
| `anti-patterns.context.md` | Session 3 (new — consolidated anti-patterns) |

---

## Change log

### Session 6 — ADR-0024 + full audit (2026-03-22)

| #   | Change                                                                                  | Files affected                                                      |
| --- | --------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| 1   | Created ADR-0024 (controller routing strategy: Areas for Web, in-place swap for API)   | `docs/adr/0024-controller-routing-strategy.md`                      |
| 2   | Added ADR-0024 row to docs-index ADR table                                              | `.github/instructions/docs-index.instructions.md`                   |
| 3   | Added ADR-0024 routing strategy note to roadmap README                                  | `docs/roadmap/README.md`                                            |
| 4   | Updated `orders-atomic-switch.md` Steps 3–4 to Area-based approach + ADR-0024 ref      | `docs/roadmap/orders-atomic-switch.md`                              |
| 5   | Updated `payments-atomic-switch.md` Step 3 to Area-based approach + ADR-0024 ref       | `docs/roadmap/payments-atomic-switch.md`                            |
| 6   | Added routing strategy as key architectural invariant                                   | `.github/context/project-state.md`                                  |
| 7   | ADR-0024 added to `ECommerceApp.sln` `adr` solution folder (auto by VS on file create) | `ECommerceApp.sln`                                                  |
| 8   | Full audit (Workflow 6 + Workflow 8): repo-index At a Glance metrics updated            | `.github/context/repo-index.md`                                     |
| 9   | Updated changelog counts and added Session 6 entry                                      | `COPILOT-SETUP-CHANGELOG.md`                                        |

### Session 5 — Full audit & .sln sync (2026-06-27)

| #   | Change                                                             | Files affected                                    |
| --- | ------------------------------------------------------------------ | ------------------------------------------------- |
| 1   | Added ADR-0022 (Navbar) and ADR-0023 (Bootstrap 5) to sln adr folder | `ECommerceApp.sln`                             |
| 2   | Added `code-reviewer.md` to sln agents folder                      | `ECommerceApp.sln`                                |
| 3   | Added `anti-patterns.context.md` to sln context folder             | `ECommerceApp.sln`                                |
| 4   | Created `reports` solution folder; added `cross-bc-integration-test-report.md` | `ECommerceApp.sln`                   |
| 5   | Added `docs/reports/` section to docs-index                        | `.github/instructions/docs-index.instructions.md` |
| 6   | Updated changelog with Session 5                                   | `COPILOT-SETUP-CHANGELOG.md`                      |

### Session 4 — Drift audit & sync (2026-03-17)

| #   | Change                                            | Files affected                                    |
| --- | ------------------------------------------------- | ------------------------------------------------- |
| 1   | Fixed ADR count 21 → 23 in copilot-instructions   | `.github/copilot-instructions.md`                 |
| 2   | Added missing ADR-0022 row to docs-index           | `.github/instructions/docs-index.instructions.md` |
| 3   | Updated changelog with Session 4                   | `COPILOT-SETUP-CHANGELOG.md`                      |

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
