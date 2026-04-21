# Copilot Instructions for ECommerceApp

> Repo-level policy for AI agents. Per-stack details auto-load via `applyTo:` globs. Full routing table → `docs-index.instructions.md`.

## 1. Project summary

ECommerceApp — ASP.NET Core MVC + Web API e-commerce platform. Clean/onion architecture, EF Core, ASP.NET Core Identity.

**Projects**: `Web` (MVC + Identity), `API` (REST + JWT), `Application`, `Infrastructure` (EF Core, repos), `Domain`, plus unit/integration tests.

**Domain areas**: Catalog, Orders, Payments, Refunds, Coupons, Customers, Currencies (NBP API), Identity & User Management.

**Tech**: ASP.NET Core, EF Core, FluentValidation, AutoMapper, xUnit, Moq, FluentAssertions, MSSQL. Frontend: Bootstrap, jQuery, require.js, LibMan. UI labels are partially in Polish — do not translate without explicit request.

## 2. Configuration map

`docs-index.instructions.md` is the **single routing table** for all Copilot config. It indexes:

- **12 instruction files**, **4 prompts**, **8 agents**, **8 skills**, **26 ADRs**, architecture docs, patterns, roadmaps, **6 context files**, **`AGENT-PIPELINE.md`**

Read `docs-index.instructions.md` to find the right file for any task. Follow its “When to read” columns. Human-facing docs start at `docs/README.md`.

## 3. AI developer profile

- Act as a senior .NET developer experienced with DDD, SOLID, and pragmatic TDD.
- Be concise and technical; prefer code-first responses.
- Ask clarifying questions when requirements are ambiguous.
- Always add or update tests for behavioral changes.

## 4. Key rules (do not bypass)

- Read applicable ADRs in `docs/adr/` before design changes. Use `docs-index.instructions.md` to find the right ADR.
- New ADR → copy `.github/templates/adr.template.md` → save to `docs/adr/XXXX/XXXX-short-title.md` + `docs/adr/XXXX/README.md` router.
- Read applicable per-stack instructions before writing code for that stack.
- Detailed rules for AbstractService, Handler pattern, ExceptionMiddleware, IFileStore, NBP API → `dotnet.instructions.md`.
- **BC changes rule**: Before editing BC code, MUST read `.github/context/project-state.md`. If blocked, STOP. Atomic switches deferred until 80–95% migration is complete.
- **Feed-forward rule**: When docs/ADR meaning changes, update `.github` in the same task. When code and docs diverge, suggest a new ADR only if the decision is new; otherwise update the existing ADR/docs.

## 5. Communication & PRs

- PRs must explain what changed, why, tests added/updated, and rollback steps for risky changes.
- Tag `@team/architecture` for ADR-impacting PRs.## 6. Project context (read before implementation)

**Bug fix rule**: Before fixing any bug, MUST read `.github/context/known-issues.md` to check if already tracked.

**Agent memory rule**: Before non-trivial agent work, MUST skim `.github/context/agent-decisions.md` for prior corrections in the area. Append a new entry after every meaningful correction (see `pre-edit.instructions.md`).

**Clarification rule**: If scope, BC ownership, or blocker status are unclear, ask a clarifying question BEFORE writing code.

Context: `project-state.md`, `known-issues.md`, `agent-decisions.md`, `repo-index.md`. Roadmaps: `docs/roadmap/README.md`. BC map: `bounded-context-map.md`.

**Architecture suggestion rule**: Follow `pre-edit.instructions.md` triggers to suggest ADR, BC map, roadmap, or project-state updates after implementation.

## 7. Coupons

- Max coupons/order: default 5, ceiling 10 (`CouponsOptions.MaxCouponsPerOrder`). See ADR-0016.

## 8. .NET 8+ Upgrade

- Replace `FluentAssertions` → `AwesomeAssertions` on .NET 8+ upgrade (drop-in, no syntax changes). Do NOT on .NET 7. See [KI-008](context/known-issues.md).

## 9. API Purchase Limits

- Max 5 units/line via `MaxApiQuantityFilter` (`ApiPurchaseOptions`); Web max 99 (`AddToCartDtoValidator`). Never cap `Shared.Quantity`.
- `TrustedApiUser` = authenticated + `api:purchase` claim OR `Service`/`Manager`/`Administrator` role. See [ADR-0025](../docs/adr/0025/README.md).
