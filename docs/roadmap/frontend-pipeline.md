# Roadmap: Frontend Error Pipeline & JS Migration

> ADR: [ADR-0021 — Frontend Error Pipeline and JS Migration Strategy](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md)
> Status: ⬜ Not started

---

## Phase 1 — Error pipeline fix
**Estimate:** ~1 day · **Priority:** Highest — errors are silently swallowed today

### Root cause (summary)
`ErrorMapToResponse.Map()` discards `BusinessException._codes` and only passes `ex.Message`
into `ExceptionResponse`. The HTTP response shape `{ response: "...", statusCode: 400 }`
is a plain object with no `.length`. `errors.js:showError()` guards on `!errorsArray.length`
→ `undefined` → `true` → `#ErrorContainer` hidden. Polish error messages never display.

### Steps

| # | File | Change |
|---|---|---|
| 1 | `Application/Exceptions/` | Add `ErrorCodeDto(string Code, IReadOnlyList<ErrorParameterDto> Parameters)` and `ErrorParameterDto(string Name, string Value)` records |
| 2 | `Application/ViewModels/ExceptionResponse.cs` | Add `IReadOnlyList<ErrorCodeDto>? Codes` property; update constructor to accept it; update `ToString()` to include `codes` in JSON (omit key when null, camelCase) |
| 3 | `Application/Exceptions/ErrorMapToResponse.cs` | For `BusinessException`: map `ex.Codes` → `IReadOnlyList<ErrorCodeDto>` and pass to `ExceptionResponse` constructor; non-BusinessException paths pass `codes: null` |
| 4 | `wwwroot/js/errors.js` | Rewrite `showErrorFromResponse`: if `data.codes` is a non-empty array → `showError(data.codes)`; else if `data.response` → show flat message in `#ErrorContainer` directly |
| 5 | Integration tests | Update any test that asserts on the full `ExceptionResponse` JSON body to include the `codes` field |

### Acceptance criteria
- [ ] `BusinessException` with `AddCode("orderNotFound").AddCode(new ErrorCode("id", orderId))` thrown in any MVC controller → `#ErrorContainer` shows the Polish string from `errors.js:values.orderNotFound` with `{id}` substituted
- [ ] `BusinessException` with empty `_codes` list → `#ErrorContainer` shows `ex.Message` plain string (flat fallback path)
- [ ] Non-BusinessException (500) → `#ErrorContainer` shows `data.response` via flat fallback
- [ ] Existing integration tests that assert `ExceptionResponse` body pass after update
- [ ] No change to `showError(errorsArray)` signature or existing call sites

---

## Phase 2 — Targeted bug fixes
**Estimate:** ~4 hours · **Priority:** High — one causes silent data loss, others are correctness issues

### Bug list

| # | File | Bug | Severity | Fix |
|---|---|---|---|---|
| 1 | `wwwroot/js/ajaxRequest.js` | `FormData` passed as `{ formData }` to `$.ajax`; result treated as `fetch` Response (`.ok`, `.text()`, `.status`) — image upload in `EditItem.cshtml` is silently broken | 🔴 High | Detect `data instanceof FormData`; set `processData: false`, `contentType: false`; update `EditItem.cshtml` to pass raw `formData` and handle plain response body |
| 2 | `wwwroot/js/modalService.js` | `denyAction` fires when info modal close button is clicked — unintended rejection | 🟡 Medium | Guard `close()` to not invoke `denyAction` when the active modal is informational |
| 3 | `wwwroot/js/buttonTemplate.js` | `"type"` string literal passed as HTML `type` attribute value (invalid HTML; should be `"button"`) | 🟡 Low | Replace with `"button"` string constant |
| 4 | `wwwroot/js/validations.js` | 4000-character RFC 2822 email regex — ReDoS risk on pathological input | 🟡 Medium | Replace with `/^[^\s@]+@[^\s@]+\.[^\s@]+$/` |

### Acceptance criteria
- [ ] Uploading a new image in `EditItem.cshtml` completes without JS runtime error; server receives `FormData` correctly
- [ ] Clicking close (✕) on an informational modal does not trigger a Promise rejection or `denyAction` callback
- [ ] Button HTML rendered by `buttonTemplate.js` has `type="button"` not `type="type"`
- [ ] Email validation in `validations.js` does not catastrophically backtrack on long malformed strings

---

## Phase 3 — New-code standard
**Estimate:** Ongoing · **Priority:** Medium — establishes the forward direction

### Rule (enforced in code review)
All controllers and views introduced after ADR-0021 acceptance use **native `fetch`** directly.
No `ajaxRequest.js` in new files. Error handling in new views uses `response.json()` +
`showError([...])` or inline `data.response` display — `showErrorFromResponse` is a legacy
bridge for jQuery callsites only.

**Boundary:** V2-prefix controllers/views and all files created after 2026-03-12 follow this rule.
Legacy controllers/views (`Item/`, `Order/`, `Payment/`, etc.) continue to use `ajaxRequest.js`
until their natural rewrite.

### Acceptance criteria
- [ ] Code review checklist item: "New view uses native `fetch`, not `ajaxRequest.js`"
- [ ] No new `ajaxRequest.js` `require()` or `define()` dependencies in files created after this ADR

---

## Phase 4 — Bootstrap 5 milestone
**Estimate:** ~2 days · **Priority:** Deferred — not started until Bootstrap upgrade is scheduled

### Scope
- Rewrite `modalService.js` for Bootstrap 5 data attributes (`data-bs-dismiss`, `data-bs-backdrop`); remove `$().modal()` jQuery plugin calls; target BS5 `Modal` class API or native `<dialog>`
- Remove `addObjectPropertiesToGlobal` from `config.js`; convert all views to explicit AMD `require([...])` dependency declarations

### Gate
This phase cannot start until a Bootstrap 5 upgrade ADR is accepted. That ADR must list
`modalService.js` rewrite as a **mandatory prerequisite** of the BS5 upgrade.

### Acceptance criteria
- [ ] `modalService.js` contains no `data-dismiss`, `data-backdrop`, `data-keyboard` Bootstrap 4 attributes
- [ ] `modalService.js` contains no `$().modal()` calls
- [ ] `config.js:addObjectPropertiesToGlobal` removed
- [ ] All views declare their AMD dependencies explicitly — no implicit `window.*` module access

---

*Last reviewed: 2026-03-12 · ADR: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md)*
