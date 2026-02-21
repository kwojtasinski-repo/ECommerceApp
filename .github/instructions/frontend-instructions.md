---
description: "Frontend rules for ECommerceApp.Web: LibMan, JS modules, localization, and UI text rules."
applyTo: "ECommerceApp.Web/wwwroot/**, ECommerceApp.Web/**/*.cshtml"
---

# Frontend Guidelines for ECommerceApp.Web

Purpose
- Rules for modifying frontend assets (JS, CSS) and using LibMan in the repository.

Dependency management
- Front-end libraries are managed via LibMan (`libman.json`). Do not introduce new libraries or update versions without approval and CI validation.
- Prefer using CDN for non-critical third-party libraries only when approved.

JavaScript patterns
- Project uses `require.js` as module loader. Follow existing AMD module patterns in `wwwroot/js/config.js`.
- Reuse custom modules (`ajaxRequest`, `modalService`, `dialogTemplate`, `buttonTemplate`, `validations`, `errors`, `site`) rather than creating new global functions.
- Avoid polluting global namespace; register modules via `require.js`.
- All AJAX calls must use `ajaxRequest` helper to ensure consistent headers, error handling, and CSRF token usage.

Validation & globalization
- Use `jquery-validation` + `jquery-validation-unobtrusive` for forms; ensure `Globalize` integration is correctly configured for number/date/currency formats.
- Do not change validation formats without checking `Globalize` configuration and tests.

UI text & localization
- UI contains Polish strings; do NOT modify translation or wording without product/team approval.
- If adding new UI text, follow existing language and consult the team for translations.

Performance & caching
- Use `asp-append-version="true"` on script/style references in `_Layout.cshtml`.
- Minify and bundle assets in production builds; ensure no blocking scripts degrade page load.

Accessibility
- Ensure ARIA attributes and keyboard accessibility for interactive components (modals, dropdowns, forms).

Testing UI
- Add integration tests for critical JS-driven flows using Playwright or Selenium if available; otherwise use server-side integration tests that exercise endpoints used by UI.

