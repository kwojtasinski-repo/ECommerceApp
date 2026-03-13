# Copilot Instructions for ECommerceApp

> This file is the **repo-level policy** for AI agents and Copilot. Per-stack details are auto-loaded from `.github/instructions/` via `applyTo:` globs. See `docs-index.instructions.md` for the docs lookup table.

## 1. Project summary

ECommerceApp — ASP.NET Core MVC + Web API e-commerce platform. Clean/onion architecture, EF Core, ASP.NET Core Identity.

**Projects**: `Web` (MVC + Views + Identity Razor Pages), `API` (REST + JWT), `Application` (services, DTOs, VMs), `Infrastructure` (EF Core, repos, migrations), `Domain` (models, interfaces), plus unit/integration tests.

**Domain areas**: Catalog, Orders, Payments, Refunds, Coupons, Customers, Currencies (NBP API), Identity & User Management.

**Tech**: ASP.NET Core, EF Core, FluentValidation, AutoMapper, xUnit, Moq, FluentAssertions, MSSQL. Frontend: Bootstrap, jQuery, require.js, LibMan (`libman.json`). UI labels are partially in Polish — do not translate without explicit request.

## 2. Instruction files (auto-loaded by path)

Per-stack files under `.github/instructions/` (check for new additions):

- `dotnet.instructions.md` — .NET architecture, services, handlers, DI, auth (`**/*.cs, **/*.csproj`).
- `web-api.instructions.md` — Web API controllers, DTOs, integration tests (`ECommerceApp.API/**`).
- `razorpages.instructions.md` — MVC controllers, Views, Razor Pages (`ECommerceApp.Web/**`).
- `frontend.instructions.md` — LibMan, JS modules, require.js (`wwwroot/**, **/*.cshtml`).
- `efcore.instructions.md` — EF Core tracking, transactions, seeding (`ECommerceApp.Infrastructure/**`).
- `migration-policy.instructions.md` — DB migration approval (`Infrastructure/Migrations/**`).
- `testing.instructions.md` — Unit/integration test patterns (`UnitTests/**, IntegrationTests/**`).
- `shared-primitives.instructions.md` — TypedId, Money, Price, Quantity (`Domain/Shared/**`).
- `safety.instructions.md` — Allowed/disallowed actions (`**`).
- `pre-edit.instructions.md` — Pre-edit checklist + doc/ADR suggestions (`**`).
- `docs-index.instructions.md` — Docs lookup table: ADRs, architecture, patterns, roadmaps (`**`).

Prompts (`.github/prompts/`): `bc-analysis.prompt.md`, `bc-implementation.prompt.md`, `pr-review.prompt.md`.
Agents (`.github/agents/`): `adr-generator` (`@adr-generator`), `bc-switch` (`@bc-switch`), `copilot-setup-maintainer` (`@copilot-setup-maintainer`).

## 3. AI developer profile

- Act as a senior .NET developer experienced with DDD, SOLID, and pragmatic TDD.
- Be concise and technical; prefer code-first responses.
- Ask clarifying questions when requirements are ambiguous.
- Always add or update tests for behavioral changes.

## 4. Key rules (do not bypass)

- Read applicable ADRs in `docs/adr/` before design changes. Use `docs-index.instructions.md` to find the right ADR.
- When creating a new ADR, copy `.github/templates/adr.template.md`. Save to `docs/adr/XXXX-short-title.md`.
- Read applicable per-stack instructions before writing code for that stack.
- Detailed rules for AbstractService, Handler pattern, ExceptionMiddleware, IFileStore, NBP API → see `dotnet.instructions.md`.

## 5. Communication & PRs

- PRs must explain what changed, why, tests added/updated, and rollback steps for risky changes.
- Tag `@team/architecture` for ADR-impacting PRs.

## 6. Project context (read before implementation)

**BC changes rule**: Before editing BC-related code, MUST read `.github/context/project-state.md` and verify the BC is not blocked. If blocked, STOP and explain the blocker.

**Bug fix rule**: Before fixing any bug, MUST read `.github/context/known-issues.md` to check if already tracked.

**Clarification rule**: If scope, BC ownership, or blocker status are unclear, ask a clarifying question BEFORE writing code.

Context: `project-state.md`, `known-issues.md`. Roadmaps: `docs/roadmap/README.md`. BC map: `bounded-context-map.md`.
