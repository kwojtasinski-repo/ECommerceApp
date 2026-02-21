# Copilot Instructions for ECommerceApp

## 1. Project summary
Short description: ECommerceApp — a simple e-commerce web application that allows placing offers on a website and ordering items. Built with ASP.NET Core MVC + Web API using clean/onion architecture, EF Core and ASP.NET Core Identity. Typical projects: `ECommerceApp.Web` (MVC Controllers + Views, Identity Area with Razor Pages), `ECommerceApp.API` (REST endpoints, JWT authentication), `ECommerceApp.Application` (services, DTOs, viewmodels), `ECommerceApp.Infrastructure` (EF Core Context, repositories, migrations), `ECommerceApp.Domain` (domain models/interfaces), plus unit and integration tests.

Contents: UI (MVC Controllers + Views, Identity Razor Pages area), API controllers, application services, repositories using EF Core, migrations under `Infrastructure/Migrations`, ADRs under `/docs/adr`.

Business domain: ECommerceApp is an e-commerce platform covering the following domain areas:
- **Catalog**: products (`Item`) with images, brands, tags, and types.
- **Orders**: customer orders (`Order`, `OrderItem`) with cart and checkout flows.
- **Payments**: payment processing and payment state tracking (`Payment`, `PaymentState`).
- **Refunds**: refund requests and lifecycle (`Refund`).
- **Coupons**: discount coupons with types and usage tracking (`Coupon`, `CouponType`, `CouponUsed`).
- **Customers**: customer profiles with addresses and contact details (`Customer`, `Address`, `ContactDetail`).
- **Currencies**: multi-currency support with external currency rate integration via NBP (National Bank of Poland) API (`Currency`, `CurrencyRate`).
- **Identity & User Management**: ASP.NET Core Identity for authentication, role-based access, and admin user management.

Technology stack:
- **Backend**: ASP.NET Core MVC, Web API, EF Core, LINQ, FluentValidation, AutoMapper.
- **Auth**: ASP.NET Core Identity, JWT (API), Google OAuth (Web).
- **Testing**: xUnit, Moq, FluentAssertions.
- **Database**: MSSQL.

Frontend stack (`ECommerceApp.Web`):
- **Bootstrap** — layout and responsive UI components.
- **Bootstrap Select** — enhanced dropdown selects.
- **Font Awesome** — icon library (solid and brands).
- **jQuery** — DOM manipulation and AJAX requests.
- **jQuery Validation + Unobtrusive** — client-side form validation integrated with ASP.NET Core tag helpers.
- **Globalize + CLDR.js + jquery-validation-globalize** — locale-aware number, date, and currency formatting and validation.
- **require.js** — module loader for custom JavaScript.
- Custom JS modules under `wwwroot/js/`: `ajaxRequest.js` (AJAX abstraction), `modalService.js`, `dialogTemplate.js`, `buttonTemplate.js`, `forms.js`, `validations.js`, `errors.js`, `config.js`, `site.js`.

Frontend notes (important for agents):
- UI navigation labels are partially in Polish (e.g., `Koszyk`, `Moje zamówienia`, `Przedmioty`) — do not translate or change UI text without explicit request.
- Role-based navigation visibility is controlled by `UserPermissions.Roles` (`Administrator`, `Manager`, `Service`, `User`) directly in `_Layout.cshtml`.
- Cart item count is loaded dynamically via AJAX on every page using `ajaxRequest.js`.
- Front-end libraries are managed via `libman.json` (LibMan); do not add or update libraries without checking `libman.json` first.

> Authoritative instructions: this file (`.github/copilot-instructions.md`) is the repository-level policy for AI agents and automation. Do NOT assume another canonical file elsewhere; follow this file and the per-stack instructions under `.github/instructions/` (when present).

## 2. Purpose & scope
This file sets repository-level rules for automation agents, Copilot, and contributors.
- Purpose: ensure safe, consistent, and reviewable changes performed by humans or automation.
- Scope: guidance for code changes, CI, tests, ADRs, and upgrade procedures relevant to this .NET MVC project.
- Per-stack detailed instructions live under `.github/instructions/`. Current files (always check for new additions):
- `.github/instructions/dotnet-instructions.md` — .NET architecture, services, handlers, testing, DI, auth.
- `.github/instructions/web-api-instructions.md` — Web API controllers, DTOs, error handling, integration tests.
- `.github/instructions/razorpages-instructions.md` — MVC controllers, Views, Razor Pages (Identity area), forms.
- `.github/instructions/frontend-instructions.md` — LibMan, JS modules, require.js, UI text rules.
- `.github/instructions/efcore-instructions.md` — EF Core tracking, transactions, migrations, seeding.
- `.github/instructions/migration-policy.md` — DB migration approval process and checklist.
- `.github/instructions/testing-instructions.md` — Unit and integration test patterns, BaseTest, Flurl, Shouldly.

## 3. AI developer profile (expected behavior)
- Act as a senior .NET developer experienced with DDD, SOLID, and pragmatic TDD.
- Be concise and technical; prefer code-first responses and short rationales.
- Ask clarifying questions when requirements are ambiguous.
- Explain trade-offs when multiple valid approaches exist.
- Always add or update tests for behavioral changes.

## 4. Key authoritative rules (do not bypass)
- Always read and follow applicable ADRs in `/docs/adr` before making design or architecture changes.
- When creating a new ADR, always copy and fill `.github/templates/adr.template.md` — never create ADRs from scratch. Save to `/docs/adr/XXXX-short-title.md`.
- Always read applicable per-stack instructions under `.github/instructions/` (if they exist) before writing code for that stack.
- Never assume or hard-code framework or package versions. If a change requires a specific SDK/package version, ask the human for confirmation.
- Do not perform destructive actions or operations against production systems.
- Services for reference/lookup domains (`Brand`, `Tag`, `Type`, `Currency`, `Coupon`, `Address`, `ContactDetail`, etc.) must inherit from `AbstractService` base class. For behavioral aggregates (`Order`, `Payment`, `Refund`, `OrderItem`), apply rich domain model patterns per `dotnet-instructions.md` § 16 — state transitions belong on the aggregate, not in standalone service classes. See [ADR-0002](../docs/adr/0002-post-event-storming-architectural-evolution-strategy.md).
- Complex domain operations use the **Handler pattern** (`CouponHandler`, `PaymentHandler`, `ItemHandler`) — do not duplicate this logic in controllers or plain services.
- All exceptions must flow through `ExceptionMiddleware` + `BusinessException` pipeline — do not add raw try/catch blocks in controllers.
- File operations (images) must use `IFileStore` / `IFileWrapper` abstractions — do not use raw `System.IO` directly.
- Currency rates are fetched from the external **NBP API** via `CurrencyRateService` + `NBPResponseUtils` — do not hardcode rates or bypass this integration.

## 5. Allowed / disallowed actions (high-level)
Allowed (without separate approval):
- Small, focused code changes and refactors requested by humans that include tests and pass CI.
- Add or update documentation, ADRs, and test-only helper code.
- Non-destructive CI and doc changes.

Disallowed without explicit human approval:
- Any edits to files under `Infrastructure/Migrations/` or running production DB migrations.
- Introducing or committing secrets or credentials.
- Upgrading to preview SDKs or preview major package versions.
- Large API-breaking changes, cross-service contract changes, or mass refactors without an accepted ADR and sign-off.
- Any automated change that directly affects production systems.

## 6. Pre-edit checklist (mandatory steps before any edit)
Before proposing or committing changes, perform and document the steps below in the PR description:
- Read the entire target file(s) and relevant related files (Controllers, services, repository code, tests).
- Read applicable ADRs under `/docs/adr` and the relevant per-stack instructions under `.github/instructions/`.
- Search for usages and migration impact (references, database migrations, API clients) and list affected areas.
- Run local validations: `dotnet restore`, `dotnet build`, and `dotnet test` (or explain why not possible).
- Include tests for any behavioral change and a short rollback/mitigation plan for risky changes.
- Open a pull request for review; do not merge without human approval unless explicitly asked.

## 7. Communication and PR expectations
- PRs must explain what changed, why, which tests were added/updated, and rollback steps for risky changes.
- Tag `@team/architecture` or maintainers for ADR-impacting PRs.
