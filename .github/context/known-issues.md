# Known Issues

> Confirmed bugs found during code review. Not yet fixed.
> Update this file when a fix is merged (move to "Resolved" or delete the entry).
> Each entry links to the ADR or roadmap step that tracks the fix.

---

## Critical

### [KI-001] `BusinessException._codes` silently discarded — no Polish error messages displayed
- **Severity**: 🔴 Critical
- **Location**: `ECommerceApp.Application/Exceptions/ErrorMapToResponse.cs` (backend) + `ECommerceApp.Web/wwwroot/js/errors.js` (frontend)
- **Symptom**: Any `BusinessException` thrown in the MVC layer shows no error message to the user. `#ErrorContainer` is hidden. Errors are silently swallowed.
- **Root cause**: `ErrorMapToResponse.Map()` discards `BusinessException._codes` and only passes `ex.Message` as a plain string. `ExceptionResponse` serializes as `{ response: "...", statusCode: 400 }`. `errors.js:showError()` guards on `.length`; a plain object has no `.length` → guard is `true` → container hidden.
- **Fix tracked in**: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) Phase 1 · [Roadmap](../roadmap/frontend-pipeline.md#phase-1--error-pipeline-fix)

---

## High

### [KI-002] `ajaxRequest.js` image upload silently broken in `EditItem.cshtml`
- **Severity**: 🟠 High
- **Location**: `ECommerceApp.Web/wwwroot/js/ajaxRequest.js` · `ECommerceApp.Web/Views/Item/EditItem.cshtml`
- **Symptom**: Uploading a product image in `EditItem.cshtml` fails silently or throws a JS runtime error. The image is not saved.
- **Root cause**: `ajaxRequest.send()` is called with `{ formData }` (an object wrapping `FormData`, not `FormData` directly). jQuery `$.ajax` with `{ formData }` does not configure `processData: false` / `contentType: false`, corrupting the multipart body. The resolved value of `$.ajax` is then treated as a `fetch` Response object (`.ok`, `.text()`, `.status` accessed) — jQuery does not return a `Response`; those properties are `undefined`.
- **Fix tracked in**: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) Phase 2 bug #1 · [Roadmap](../roadmap/frontend-pipeline.md#phase-2--targeted-bug-fixes)

---

## Medium

### [KI-003] `modalService.js` — `denyAction` fires on info modal close
- **Severity**: 🟡 Medium
- **Location**: `ECommerceApp.Web/wwwroot/js/modalService.js`
- **Symptom**: Clicking the close button (✕) on an informational modal triggers the `denyAction` handler (Promise rejection), causing unexpected behavior in callers that expect no rejection on close.
- **Root cause**: `close()` calls `denyAction` unconditionally without checking whether the active modal is a confirmation or informational type.
- **Fix tracked in**: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) Phase 2 bug #2 · [Roadmap](../roadmap/frontend-pipeline.md#phase-2--targeted-bug-fixes)

### [KI-004] `validations.js` email regex — ReDoS risk
- **Severity**: 🟡 Medium
- **Location**: `ECommerceApp.Web/wwwroot/js/validations.js` — `emailRegex`
- **Symptom**: Catastrophic backtracking on pathological email strings (e.g., very long strings with invalid characters). Can freeze the browser tab.
- **Root cause**: 4000-character RFC 2822 regex pattern with nested quantifiers. Standard `/^[^\s@]+@[^\s@]+\.[^\s@]+$/` is sufficient and safe.
- **Fix tracked in**: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) Phase 2 bug #4 · [Roadmap](../roadmap/frontend-pipeline.md#phase-2--targeted-bug-fixes)

---

## Low

### [KI-005] `buttonTemplate.js` — invalid HTML button `type` attribute
- **Severity**: 🔵 Low
- **Location**: `ECommerceApp.Web/wwwroot/js/buttonTemplate.js`
- **Symptom**: Buttons rendered by `createButton()` have `type="type"` — not a valid HTML button type. Browsers default to `type="submit"`, which may cause unintended form submissions inside modals.
- **Root cause**: The string `"type"` is passed as the value of the `type` attribute instead of `"button"`.
- **Fix tracked in**: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) Phase 2 bug #3 · [Roadmap](../roadmap/frontend-pipeline.md#phase-2--targeted-bug-fixes)

---

## Resolved

### [KI-001] `BusinessException._codes` silently discarded — no Polish error messages displayed ✅
- **Fix**: `ExceptionResponse` now carries `IReadOnlyList<ErrorCodeDto>? Codes`. `ErrorMapToResponse.Map()` maps `BusinessException._codes` → `ErrorCodeDto` list. `errors.js:showErrorFromResponse` handles structured `data.codes` array + flat `data.response` fallback. `_Layout.cshtml:setGlobalError` missing `return` fixed.
- **Files changed**: `ErrorCodeDto.cs` (new), `ExceptionResponse.cs`, `ErrorMapToResponse.cs`, `errors.js`, `_Layout.cshtml`

### [KI-005] `buttonTemplate.js` — invalid HTML button `type` attribute ✅
- **Fix**: Changed `"type"` literal to `"button"` in `modalService.js` confirmation modal button creation.
- **Files changed**: `modalService.js`

---

*Last reviewed: 2026-03-15*
