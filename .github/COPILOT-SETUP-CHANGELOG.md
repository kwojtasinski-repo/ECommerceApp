# Copilot Setup — Changelog & Current State

> This file tracks **what changed** in the `.github/` Copilot configuration and serves
> as a snapshot of the current setup. Updated by `@copilot-setup-maintainer` or manually.
>
> Use as a quick reference to see what exists without scanning every file.

---

## Current state summary

| Category                               | Count | Details                                                |
| -------------------------------------- | ----- | ------------------------------------------------------ |
| `copilot-instructions.md`              | 1     | ≤ 4,000 chars, repo-level policy                       |
| Instruction files (`.instructions.md`) | 11    | All with `applyTo:` frontmatter                        |
| Prompt files (`.prompt.md`)            | 3     | BC analysis, BC implementation, PR review              |
| Agent files                            | 3     | adr-generator, bc-switch, copilot-setup-maintainer     |
| Skills (`SKILL.md`)                    | 8     | Scaffolding templates for common artifacts             |
| Context files                          | 4     | project-state, known-issues, repo-index, future-skills |

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

### `.github/agents/` (3 files)

| File                          | Added                       |
| ----------------------------- | --------------------------- |
| `adr-generator.md`            | Pre-existing (refs updated) |
| `bc-switch.md`                | Pre-existing                |
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

### `.github/context/` (4 files)

| File               | Added                               |
| ------------------ | ----------------------------------- |
| `project-state.md` | Pre-existing                        |
| `known-issues.md`  | Pre-existing                        |
| `repo-index.md`    | Session 2 (new — full codebase map) |
| `future-skills.md` | Session 2 (new — skills roadmap)    |

---

## Change log

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
