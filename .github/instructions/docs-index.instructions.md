---
applyTo: "**"
---

# Docs Index — Copilot Routing Table

> **Single entry point** for all Copilot configuration and project documentation.
> Load files **on demand** — read only what the task requires, then follow connections.
> Docs are human-owned — never modify them. Just look up and read.

## Quick navigation

| Your task               | Read first                        | Then chain to                       |
| ----------------------- | --------------------------------- | ----------------------------------- |
| First-time orientation  | ADR-0001                          | `project-state.md`, `repo-index.md` |
| Writing/editing C# code | `dotnet.instructions.md` (auto)   | ADR for the target BC               |
| Creating a BC artifact  | `implementation-patterns.md`      | Matching `create-*` skill           |
| Fixing a bug            | `known-issues.md`                 | `project-state.md`                  |
| BC migration work       | `bounded-context-map.md`          | BC's ADR → `project-state.md`       |
| Adding Copilot config   | This file                         | `@copilot-setup-maintainer`         |
| Frontend/JS changes     | `frontend.instructions.md` (auto) | ADR-0021                            |

## Instruction files (`.github/instructions/`)

Auto-loaded by `applyTo:` globs — Copilot reads these automatically when editing matching files.

| File                                | `applyTo:`                          | Scope                                 |
| ----------------------------------- | ----------------------------------- | ------------------------------------- |
| `dotnet.instructions.md`            | `**/*.cs, **/*.csproj`              | .NET architecture, services, DI, auth |
| `web-api.instructions.md`           | `ECommerceApp.API/**`               | Web API controllers, DTOs             |
| `razorpages.instructions.md`        | `ECommerceApp.Web/**`               | MVC, Views, Razor Pages               |
| `frontend.instructions.md`          | `wwwroot/**, **/*.cshtml`           | LibMan, JS modules, require.js        |
| `efcore.instructions.md`            | `ECommerceApp.Infrastructure/**`    | EF Core tracking, transactions        |
| `migration-policy.instructions.md`  | `Infrastructure/Migrations/**`      | DB migration approval                 |
| `testing.instructions.md`           | `UnitTests/**, IntegrationTests/**` | Unit/integration test patterns        |
| `shared-primitives.instructions.md` | `Domain/Shared/**`                  | TypedId, Money, Price, Quantity       |
| `safety.instructions.md`            | `**`                                | Allowed/disallowed actions            |
| `pre-edit.instructions.md`          | `**`                                | Pre-edit checklist, doc suggestions   |
| `docs-index.instructions.md`        | `**`                                | This file — routing table             |

## Agents (`.github/agents/`)

| Agent            | Invoke                      | When to use                                   |
| ---------------- | --------------------------- | --------------------------------------------- |
| ADR Generator    | `@adr-generator`            | Generating a new Architecture Decision Record |
| BC Switch        | `@bc-switch`                | Executing atomic BC legacy-to-new switch      |
| Code Reviewer    | `@code-reviewer`            | Automated PR review against ADRs and rules    |
| Setup Maintainer | `@copilot-setup-maintainer` | Syncing Copilot config and `.sln` structure   |

## Prompts (`.github/prompts/`)

| Prompt                        | When to use                            |
| ----------------------------- | -------------------------------------- |
| `bc-analysis.prompt.md`       | Analyzing a BC for migration readiness |
| `bc-implementation.prompt.md` | Planning BC implementation steps       |
| `pr-review.prompt.md`         | Reviewing a pull request               |

## ADRs (`docs/adr/`)

Read an ADR **only** when the "When to read" condition matches the files you are editing.

| ADR  | Title                                                 | When to read                                                                     |
| ---- | ----------------------------------------------------- | -------------------------------------------------------------------------------- |
| 0001 | Project overview and technology stack                 | First-time context; project-wide tech decisions                                  |
| 0002 | Post-event-storming architectural evolution strategy  | Any BC migration or parallel-change work                                         |
| 0003 | Feature folder organization for new BC code           | Creating new folders/namespaces for a BC                                         |
| 0004 | Module taxonomy and bounded context grouping          | BC grouping, module naming, namespace decisions                                  |
| 0005 | AccountProfile BC — UserProfile aggregate design      | Editing AccountProfile domain or services                                        |
| 0006 | TypedId and value objects as shared domain primitives | Editing `Domain/Shared/**` (TypedId, Money, Price)                               |
| 0007 | Catalog BC — Product, Category, Tag aggregate design  | Editing Catalog domain or services                                               |
| 0008 | Supporting/Currencies BC design                       | Editing Currency, CurrencyRate, NBP integration                                  |
| 0009 | Supporting/TimeManagement BC design                   | Editing time-related domain logic                                                |
| 0010 | In-memory message broker for cross-BC communication   | Adding domain events or cross-BC messaging                                       |
| 0011 | Inventory/Availability BC design                      | Editing Inventory domain or stock logic                                          |
| 0012 | Presale/Checkout BC design                            | Editing checkout flow, cart logic                                                |
| 0013 | Per-BC DbContext interfaces                           | Adding new DbContext or per-BC data access                                       |
| 0014 | Sales/Orders BC design                                | Editing Order, OrderItem domain or services                                      |
| 0015 | Sales/Payments BC design                              | Editing Payment, PaymentState domain or services                                 |
| 0016 | Sales/Coupons BC design                               | Editing Coupon, CouponType, CouponUsed                                           |
| 0017 | Sales/Fulfillment BC design                           | Editing fulfillment or shipping logic                                            |
| 0018 | Supporting/Communication BC design                    | Editing notification or messaging features                                       |
| 0019 | Identity/IAM BC design                                | Editing Identity, roles, authentication                                          |
| 0020 | Backoffice BC design                                  | Editing admin/backoffice features                                                |
| 0021 | Frontend error pipeline and JS migration strategy     | Editing `wwwroot/js/**`, error handling in Views                                 |
| 0022 | Navbar two-tier redesign                              | Editing navbar, `_Layout.cshtml`, navigation structure                           |
| 0023 | Bootstrap 5 upgrade                                   | Editing `_Layout.cshtml`, `modalService.js`, or any view with BS data attributes |

## Architecture docs (`docs/architecture/`)

| File                     | When to read                                                                                        |
| ------------------------ | --------------------------------------------------------------------------------------------------- |
| `bounded-context-map.md` | Before adding cross-BC dependencies, proposing new aggregates, or checking BC implementation status |

## Pattern templates (`docs/patterns/`)

| File                         | When to read                                                                                                          |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `implementation-patterns.md` | When implementing a new aggregate, value object, repository, facade, or handler — contains 14 reusable code templates |

## Roadmaps (`docs/roadmap/`)

| File                        | When to read                                                             |
| --------------------------- | ------------------------------------------------------------------------ |
| `README.md`                 | Before any BC implementation — shows dependency order and phase overview |
| `orders-atomic-switch.md`   | Working on Sales/Orders BC (highest-priority unblocking item)            |
| `payments-atomic-switch.md` | Working on Sales/Payments BC (blocked by Orders)                         |
| `iam-atomic-switch.md`      | Working on Identity/IAM BC (coordinate with Orders switch)               |
| `presale-slice2.md`         | Working on Presale/Checkout Slice 2 (blocked by Orders)                  |
| `frontend-pipeline.md`      | Working on frontend JS/error pipeline (ADR-0021 phases)                  |

## Reports (`docs/reports/`)

| File                                    | When to read                                                                       |
| --------------------------------------- | ---------------------------------------------------------------------------------- |
| `cross-bc-integration-test-report.md`  | Checking cross-BC integration flow coverage or planning new integration test scope |

## Context files (`.github/context/`)

| File                       | When to read                                                              |
| -------------------------- | ------------------------------------------------------------------------- |
| `project-state.md`         | **Always** before editing BC-related code — check if BC is blocked        |
| `known-issues.md`          | **Always** before fixing any bug — check if already tracked               |
| `repo-index.md`            | When you need to locate code across the repo — full codebase map          |
| `future-skills.md`         | When creating skills, adding cross-BC events, or planning automation      |
| `anti-patterns.context.md` | Loaded by `@code-reviewer` — consolidated "never do" rules (BLOCKS MERGE) |

## Skills (`.github/skills/`)

Read a skill **before** scaffolding the corresponding artifact.

| Skill                     | When to read                                                     |
| ------------------------- | ---------------------------------------------------------------- |
| `create-unit-test`        | Scaffolding a new xUnit test class (service, aggregate, handler) |
| `create-dbcontext`        | Creating a per-BC DbContext (4-file set)                         |
| `create-ef-configuration` | Adding an EF Core entity configuration                           |
| `create-di-extension`     | Adding Application or Infrastructure DI extension methods        |
| `create-domain-event`     | Creating cross-BC IMessage events and/or IMessageHandler         |
| `create-integration-test` | Scaffolding an integration test with BaseTest<T> + Shouldly      |
| `create-http-scenario`    | Creating a .http file for any API endpoint testing               |
| `create-validator`        | Adding a FluentValidation AbstractValidator<T>                   |
