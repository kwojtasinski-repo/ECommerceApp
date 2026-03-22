# Copilot Instructions for ECommerceApp

> Repo-level policy for AI agents. Per-stack details auto-load via `applyTo:` globs. Full routing table → `docs-index.instructions.md`.

## 1. Project summary

ECommerceApp — ASP.NET Core MVC + Web API e-commerce platform. Clean/onion architecture, EF Core, ASP.NET Core Identity.

**Projects**: `Web` (MVC + Identity), `API` (REST + JWT), `Application` (services, DTOs), `Infrastructure` (EF Core, repos), `Domain` (models), plus unit/integration tests.

**Domain areas**: Catalog, Orders, Payments, Refunds, Coupons, Customers, Currencies (NBP API), Identity & User Management.

**Tech**: ASP.NET Core, EF Core, FluentValidation, AutoMapper, xUnit, Moq, FluentAssertions, MSSQL. Frontend: Bootstrap, jQuery, require.js, LibMan (`libman.json`). UI labels are partially in Polish — do not translate without explicit request.

## 2. Configuration map

`docs-index.instructions.md` is the **single routing table** for all Copilot config. It indexes:

- **11 instruction files** — per-stack rules, auto-loaded by `applyTo:` globs
- **3 prompts** — `bc-analysis`, `bc-implementation`, `pr-review`
- **4 agents** — `@adr-generator`, `@bc-switch`, `@code-reviewer`, `@copilot-setup-maintainer`
- **8 skills** — scaffolding templates (unit test, dbcontext, ef-config, DI, domain event, integration test, http scenario, validator)
- **23 ADRs**, architecture docs, patterns, roadmaps, context files

Read `docs-index.instructions.md` to find the right file for any task. Follow its “When to read” columns.

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
- **BC changes rule**: Before editing BC-related code, MUST read `.github/context/project-state.md` and verify the BC is not blocked. If blocked, STOP and explain the blocker. No atomic switch for any BC is performed until 80–95% of the overall BC migration implementation is complete. Atomic switches are always deferred to the end of the migration programme, not done after each individual BC.

## 5. Communication & PRs

- PRs must explain what changed, why, tests added/updated, and rollback steps for risky changes.
- Tag `@team/architecture` for ADR-impacting PRs.

## 6. Project context (read before implementation)

**Bug fix rule**: Before fixing any bug, MUST read `.github/context/known-issues.md` to check if already tracked.

**Clarification rule**: If scope, BC ownership, or blocker status are unclear, ask a clarifying question BEFORE writing code.

Context: `project-state.md`, `known-issues.md`, `repo-index.md`. Roadmaps: `docs/roadmap/README.md`. BC map: `bounded-context-map.md`.

**Architecture suggestion rule**: `pre-edit.instructions.md` defines when to proactively suggest ADR, BC map, roadmap, or project-state updates. Always follow its triggers after completing implementation work.

## 7. Coupons Configuration

- Coupons BC Slice 2: The maximum number of coupons per order is set to a default of 5 (industry standard), with a hard ceiling of 10. This limit is configurable via `CouponsOptions.MaxCouponsPerOrder`.

## 8. .NET 8+ Upgrade Rule

- **FluentAssertions → AwesomeAssertions**: When upgrading the project to .NET 8 or later, replace the `FluentAssertions` NuGet package with `AwesomeAssertions` (Apache 2.0 community fork). Replace all `using FluentAssertions;` → `using AwesomeAssertions;`. No assertion syntax changes needed. See [KI-008](context/known-issues.md) for details. Do NOT perform this replacement while the project targets .NET 7 or earlier.

## 9. API Purchase Limit Configuration

- **Max 5 units per product per API order line**: The API enforces a maximum of 5 units of a single product per cart line (`ApiPurchaseOptions.MaxQuantityPerOrderLine = 5`). This is an API-only limit enforced via `MaxApiQuantityFilter` — the domain (`Shared.Quantity`) stays pure. The Web storefront uses a separate validator (`AddToCartDtoValidator`, limit 99 per line).
- **Future**: Both API and Web limits become backoffice-configurable — two independent settings (`ApiMaxQuantityPerOrderLine`, `WebMaxQuantityPerOrderLine`) stored in `backoffice.PurchaseLimitSettings`, loaded into `IMemoryCache` on startup, resolved via `IApiPurchaseLimitsService`. Until the Backoffice BC is live, the constants in `ApiPurchaseOptions` are authoritative.
- **Do NOT add a max quantity cap to `Shared.Quantity`** — that value object is channel-agnostic and must stay pure.
- **Trusted API user**: `TrustedApiUser` policy = authenticated AND (`api:purchase` claim OR role `Service`/`Manager`/`Administrator`). The `User` role alone does not grant purchase flow access via API.
- See [ADR-0025](docs/adr/0025-api-tiered-access-trusted-purchase-policy.md) and [DD-002](context/known-issues.md).
