---
description: "Razor Pages and MVC view guidance for ECommerceApp.Web"
applyTo: "ECommerceApp.Web/**/*.cshtml, ECommerceApp.Web/**/*.cshtml.cs, ECommerceApp.Web/**/*.cs"
---

# Razor Pages & MVC Views Guidelines

Purpose
- Provide UI rules for `ECommerceApp.Web` which primarily uses MVC Controllers + Views and includes ASP.NET Core Identity Razor Pages for auth.

PageModel & Controller rules
- Prefer MVC controllers and Views for main UI flows; Identity area uses Razor Pages and `PageModel` patterns.
- Keep `PageModel` and `Controller` classes thin: only UI state, input binding, and calls to application services.
- Do not place business logic in `PageModel` or Views (`.cshtml`). Delegate to application services.

Forms & validation
- Use tag helpers and validation attributes for forms.
- Rely on `ModelStateFilter` for model validation; do not manually check `ModelState` in actions.
- For client-side validation, ensure `jquery.validate.unobtrusive.js` and `jquery.validate.globalize` are included where needed.

Anti-forgery
- All POST endpoints in views must include anti-forgery tokens (`<form asp-antiforgery="true">` or `@Html.AntiForgeryToken()`).

Localization & UI text
- UI text is partially in Polish; avoid translating UI labels automatically. Ask team before changing labels.

Scripts & layout
- Use `_Layout.cshtml` as the global layout. Do not duplicate site-wide scripts in individual views. Add page-specific scripts in `@section Scripts`.
- Use `asp-append-version="true"` for site scripts to ensure cache busting when using static file versioning.

Error handling in MVC controllers
- Exception handling pipeline: see [`dotnet-instructions.md §4`](../instructions/dotnet-instructions.md).
- MVC controllers may catch `BusinessException` for flows that need to surface errors via redirects or query params.
- Use `MapExceptionAsRouteValues(exception)` from `BaseController` when redirecting with error codes.
- Use `BuildErrorModel(exception).AsQueryCollection()` from `BaseController` when setting error on the current request context before returning a view.
- Do NOT use raw `BadRequest(exception.Message)` in MVC controllers — use `BadRequest(BuildErrorModel(exception).Codes)` for AJAX-style responses.

Partial views & templates
- Use partial views for reusable UI fragments; prefer tag helpers where appropriate.

Testing & UI
- For critical UI flows, add integration tests using the `CustomWebApplicationFactory` and verify end-to-end behavior.

