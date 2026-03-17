---
description: "Frontend rules for ECommerceApp.Web: LibMan, JS modules, localization, and UI text rules."
applyTo: "ECommerceApp.Web/wwwroot/**, ECommerceApp.Web/**/*.cshtml"
---

# Frontend Guidelines for ECommerceApp.Web

Purpose
- Rules for modifying frontend assets (JS, CSS) and using LibMan in the repository.

Bootstrap
- Project targets **Bootstrap 5** (5.3.x). Do not use Bootstrap 4 APIs or class names.
- Use `data-bs-*` attributes (e.g. `data-bs-toggle`, `data-bs-target`, `data-bs-dismiss`). Never use the BS4 `data-toggle`/`data-target`/`data-dismiss` equivalents.
- Use BS5 utility classes: `ms-*`/`me-*` (not `ml-*`/`mr-*`), `ps-*`/`pe-*` (not `pl-*`/`pr-*`), `text-start`/`text-end` (not `text-left`/`text-right`), `float-start`/`float-end`, `text-bg-{color}` (not `badge-{color}`), `table-light` (not `thead-light`), `fw-bold` (not `font-weight-bold`), `btn-close` (not `close`).
- `btn-block` (BS4 full-width button helper) is removed. Use the `btn` base class plus `w-100` for full-width buttons (`class="btn btn-outline-secondary w-100"`). Do not omit the `btn` base class.
- Grid offsets: use `offset-md-*` (not `col-md-offset-*`, which was removed in BS4).
- `input-group-append` / `input-group-prepend` wrappers are removed; children are direct siblings inside `input-group`.
- `form-inline` is removed; use `d-flex` with flex utilities.
- Use `form-select` for `<select>` elements (not `form-control`).
- For tooltips, use `new bootstrap.Tooltip(el)`. Do not use jQuery `$().tooltip()`.
- For modals created in JavaScript, use `bootstrap.Modal` class API (`new bootstrap.Modal(el, options)`). Do not use jQuery `$().modal()`.
- `modalService.js` manages modal instances via `_modalInstance`. All modal hiding is done through the JS handler — never use `data-bs-dismiss` on programmatically created modal buttons.
- `showInformationModal` uses `closeOnlyHandler` (no deny action). `showConfirmationModal` uses `closeButtonHandler` (invokes deny action). See ADR-0023 §3.

Tom Select
- **Tom Select** (v2.4.1) is used for enhanced `<select>` elements (multi-select with search, remove buttons).
- Loaded globally via `_Layout.cshtml` (`tom-select.complete.min.js` + `tom-select.bootstrap5.min.css`).
- Initialize in page `@section Scripts` using: `new TomSelect('#elementId', { plugins: ['remove_button'], placeholder: '...' })`.
- Currently used on `AddItem` and `EditItem` for tag multi-select (`#ItemTagsSelect`).

Search forms
- Legacy list pages use BS5 `input-group` for inline search: `<div class="input-group mb-3">` with `form-control` input and `btn btn-outline-secondary` button.

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

