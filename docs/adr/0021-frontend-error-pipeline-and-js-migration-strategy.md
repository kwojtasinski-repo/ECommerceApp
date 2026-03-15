# ADR-0021: Frontend Error Pipeline and JS Migration Strategy

## Status
Proposed

## Date
2026-03-12

## Context

Three interconnected problems were identified during a cross-cutting review of the frontend JS
modules and backend error serialization pipeline:

**Problem 1 — `BusinessException._codes` silently discarded before reaching HTTP.**
`ErrorMapToResponse.Map()` constructs `ExceptionResponse(ex.Message, BadRequest)` for every
`BusinessException`. The `_codes` list — the structured, internationalized error code system —
is discarded and never serialized into the HTTP response. Only a plain string message is returned.

**Problem 2 — `errors.js:showError()` silently hides all MVC-layer errors.**
`showErrorFromResponse(jqXhrError)` reads `jqXhrError.responseJSON` and passes it to `showError`.
`showError` guards on `!errorsArray.length`. Because `ExceptionResponse` serializes as a plain
object `{ response: "...", statusCode: 400 }` (not an array), `.length` is `undefined`.
The guard evaluates to `true` → `#ErrorContainer` is hidden → the user sees nothing.
Polish error messages (`errors.getError(key, args)`) are never reached for any
MVC-routed `BusinessException`.

**Problem 3 — Two parallel HTTP client patterns without a defined migration path.**
`ajaxRequest.js` wraps jQuery `$.ajax`. New V2 views (`V2Product/Add.cshtml`) use native
`fetch` directly. No team standard exists for which to use in new code, leading to drift and
inconsistency in error-handling callsites.

**Existing JS module inventory:**
- `ajaxRequest.js` — wraps `$.ajax` in a Promise; jQuery-based; FormData handling is broken
- `forms.js` — client validation for runtime modal forms; no equivalent library alternative
- `errors.js` — Polish error code interpolation dictionary; domain-specific, no library can replace it
- `modalService.js` — programmatic Bootstrap 4 modal builder; Bootstrap 4 hard-wired
- `validations.js` — shared validation constants
- `config.js` — boots AMD; `addObjectPropertiesToGlobal` defeats AMD isolation (all modules
  exposed on `window`)

## Decision

### 1. Enrich `ExceptionResponse` with a structured `Codes` list

`ExceptionResponse` will include an optional `Codes` array. `ErrorMapToResponse` will populate
it when mapping a `BusinessException`:

```csharp
// ExceptionResponse — add Codes
public class ExceptionResponse
{
    public string Response { get; }
    public HttpStatusCode StatusCode { get; }
    public IReadOnlyList<ErrorCodeDto>? Codes { get; }

    public ExceptionResponse(string response, HttpStatusCode statusCode,
        IReadOnlyList<ErrorCodeDto>? codes = null)
    { ... }
}

public sealed record ErrorCodeDto(string Code,
    IReadOnlyList<ErrorParameterDto> Parameters);
public sealed record ErrorParameterDto(string Name, string Value);
```

```csharp
// ErrorMapToResponse — propagate _codes
BusinessException ex => new ExceptionResponse(
    ex.Message,
    HttpStatusCode.BadRequest,
    ex.Codes.Select(c => new ErrorCodeDto(
        c.Code,
        c.Parameters?.Select(p => new ErrorParameterDto(p.Name, p.Value)).ToList() ?? []
    )).ToList()),
```

HTTP response shape after this change:
```json
{
  "response": "Nie znaleziono zamówienia o id 42",
  "statusCode": 400,
  "codes": [{ "code": "orderNotFound", "parameters": [{ "name": "id", "value": "42" }] }]
}
```

Non-`BusinessException` responses continue to return `codes: null` (omitted in JSON when null).
Existing API consumers that read only `response` and `statusCode` are unaffected.

### 2. Fix `errors.js:showErrorFromResponse` to handle both shapes

`showErrorFromResponse` is updated to handle both the new structured `codes` path and the
legacy flat `response` fallback:

```javascript
function showErrorFromResponse(error) {
    if (!error.responseJSON) return;
    const data = error.responseJSON;

    // structured codes path (BusinessException with _codes)
    if (Array.isArray(data.codes) && data.codes.length > 0) {
        showError(data.codes);
        return;
    }

    // flat message fallback (non-BusinessException, or codes list empty)
    if (data.response) {
        const errorContainer = document.querySelector('#ErrorContainer');
        const errorValue = document.querySelector('#ErrorValue');
        if (errorContainer && errorValue) {
            errorContainer.style.display = 'block';
            errorValue.textContent = data.response;
        }
    }
}
```

`showError(errorsArray)` is left unchanged — it already handles `[{code, parameters}]` arrays
correctly once the array is properly populated.

### 3. JS HTTP client migration strategy

**New code** (any view or controller introduced after this ADR): use native `fetch` directly.
No `ajaxRequest.js` in new views.

**Existing code**: `ajaxRequest.js` remains in place. No mass rewrite. Bug fixes to
`ajaxRequest.js` (e.g., `FormData` handling) are applied as targeted PRs, not as a migration.

**`forms.js`**: kept as-is with targeted bug fixes only. There is no drop-in library
replacement for runtime modal form validation in ASP.NET Core MVC with Bootstrap modals.

**`errors.js`**: kept as-is; only `showErrorFromResponse` is updated (§ 2 above).

The two patterns (`ajaxRequest.js` and native `fetch`) will coexist until existing legacy views
are naturally rewritten. No flag-day migration. The boundary is: V2-prefix controllers and
views use `fetch`; legacy controllers and views continue to use `ajaxRequest.js`.

### 4. Bootstrap modal upgrade path

`modalService.js` is Bootstrap 4 hard-wired (`data-dismiss`, `data-backdrop`, `$().modal()`
jQuery plugin). It will be rewritten as part of the Bootstrap 5 upgrade milestone (not now).
The rewrite will target Bootstrap 5 data attributes and may replace the jQuery plugin calls
with the native `<dialog>` HTML element or BS5 `Modal` class API. No action is taken on
`modalService.js` outside of targeted bug fixes until that milestone is scheduled.

`config.js:addObjectPropertiesToGlobal` (AMD isolation defeat) is tracked as tech debt.
Removal is planned for the same milestone as the BS5 upgrade, when views using the old globals
will already require a rewrite pass.

## Consequences

### Positive
- Polish error messages from `BusinessException._codes` reach the UI end-to-end for the
  first time — no more silent error swallowing for MVC-layer domain exceptions.
- The HTTP error contract is backwards-compatible — existing API clients reading only
  `response` and `statusCode` are unaffected.
- `showErrorFromResponse` handles both structured and flat error shapes, making
  the frontend tolerant of mixed backend error types during transition.
- New views have a clear standard (`fetch`-first) with no ambiguity about which client to use.
- `modalService.js` Bootstrap 4 coupling is explicitly documented and deferred to a
  scheduled milestone rather than left as implicit tech debt.

### Negative
- Two HTTP client patterns (`ajaxRequest.js` and `fetch`) coexist indefinitely in the codebase
  until legacy views are rewritten.
- `ExceptionResponse` grows a new field; integration test assertions that snapshot the full
  response body must be updated.
- `ErrorMapToResponse` change must be applied consistently — if new exception types are added
  without updating the switch, their codes will be silently discarded again.

### Risks & mitigations
- **Risk**: `BusinessException` is thrown with an empty `_codes` list. `Codes` array in
  response will be empty; `showErrorFromResponse` falls through to the flat `response` fallback.
  **Mitigation**: fallback path in `showErrorFromResponse` always displays `data.response` text.
- **Risk**: New code written with `fetch` handles `Response` objects; `errors.js` still expects
  jQuery jqXHR shape (`error.responseJSON`). `showErrorFromResponse` is not compatible with a
  native `fetch` rejection.
  **Mitigation**: new views using `fetch` call `response.json()` directly and pass the parsed
  body to `showError([...])` or display `data.response` inline. `showErrorFromResponse` is
  a legacy bridge for jQuery-based callsites only — it is not extended to cover `fetch`.
- **Risk**: Bootstrap 5 upgrade proceeds without `modalService.js` rewrite.
  **Mitigation**: BS5 upgrade ADR must list `modalService.js` rewrite as a mandatory prerequisite.

## Alternatives considered

- **Serialize `BusinessException` as a `[{code, parameters}]` top-level array** — rejected.
  Breaks all existing API consumers that read `response` and `statusCode`. The envelope
  approach (`{ response, statusCode, codes? }`) is backwards-compatible.
- **Fix `showError` to handle a plain object as a single-message fallback** — rejected in
  isolation. Without the backend change, `codes` is never populated and the Polish message
  lookup is still bypassed. Both changes are required together.
- **Migrate `ajaxRequest.js` to `fetch` in a single PR across all views** — rejected.
  High blast radius, forces simultaneous update of all error-handling callsites. The
  new-code-only standard achieves the same long-term outcome incrementally.
- **Replace `modalService.js` with Alpine.js or a headless modal library** — rejected for
  now. Requires replacing Bootstrap's modal system across all existing views. Deferred to the
  BS5 upgrade milestone where views already need changes.

## Migration plan

**Phase 1 — Error pipeline (highest priority, 1 day):**
1. Add `ErrorCodeDto`, `ErrorParameterDto` records to `Application/Exceptions/` or
   `Application/ViewModels/`.
2. Update `ExceptionResponse` constructor to accept `IReadOnlyList<ErrorCodeDto>?`.
3. Update `ExceptionResponse.ToString()` serialization to include `codes` (omit if null).
4. Update `ErrorMapToResponse.Map()` to map `BusinessException._codes` → `ErrorCodeDto` list.
5. Update `showErrorFromResponse` in `errors.js` per § 2 above.
6. Update integration tests that assert on the full `ExceptionResponse` body shape.

**Phase 2 — Targeted bug fixes (0.5 day, separate PRs):**
7. Fix `ajaxRequest.js` `FormData` handling (`processData: false`, `contentType: false`).
8. Fix `modalService.js` `denyAction` trigger on info modal close.
9. Fix `buttonTemplate.js` `"type"` literal passed as HTML button `type` attribute value.
10. Replace `validations.js` email regex with a non-ReDoS alternative.

**Phase 3 — New-code standard (ongoing):**
11. All new controllers and views (V2-prefix and beyond) use native `fetch`.
    Establish in team code-review checklist: no `ajaxRequest.js` in new files.

**Phase 4 — Bootstrap 5 milestone (scheduled separately):**
12. Rewrite `modalService.js` for BS5 data attributes; remove `$().modal()` jQuery plugin calls.
13. Remove `addObjectPropertiesToGlobal` from `config.js`; convert views to explicit AMD
    `require([...])` declarations.

## Conformance checklist

- [x] `ExceptionResponse` has `IReadOnlyList<ErrorCodeDto>? Codes` property
- [x] `ErrorMapToResponse` maps `BusinessException._codes` into `ExceptionResponse.Codes`
- [x] Non-`BusinessException` mappings pass `codes: null` — no regression for `500` responses
- [x] `ExceptionResponse.ToString()` serializes `codes` with camelCase; omits key when null
- [x] `errors.js:showErrorFromResponse` handles both `data.codes` (array) and `data.response` (flat fallback)
- [x] `showError(errorsArray)` is unchanged — still iterates `[{code, parameters}]` array
- [ ] Integration tests asserting `ExceptionResponse` body are updated for the new shape
- [ ] No new views introduce `ajaxRequest.js` after this ADR is accepted
- [ ] `modalService.js` Bootstrap 4 rewrite is listed as prerequisite in any Bootstrap 5 upgrade ADR

## References

- [ADR-0001 — Technology Stack](./0001-project-overview-and-technology-stack.md)
- [`ECommerceApp.Application/Middlewares/ExceptionMiddleware.cs`](../../ECommerceApp.Application/Middlewares/ExceptionMiddleware.cs)
- [`ECommerceApp.Application/Exceptions/ErrorMapToResponse.cs`](../../ECommerceApp.Application/Exceptions/ErrorMapToResponse.cs)
- [`ECommerceApp.Application/ViewModels/ExceptionResponse.cs`](../../ECommerceApp.Application/ViewModels/ExceptionResponse.cs)
- [`ECommerceApp.Application/Exceptions/BusinessException.cs`](../../ECommerceApp.Application/Exceptions/BusinessException.cs)
- [`ECommerceApp.Web/wwwroot/js/errors.js`](../../ECommerceApp.Web/wwwroot/js/errors.js)
- [`ECommerceApp.Web/wwwroot/js/ajaxRequest.js`](../../ECommerceApp.Web/wwwroot/js/ajaxRequest.js)
- [`ECommerceApp.Web/wwwroot/js/modalService.js`](../../ECommerceApp.Web/wwwroot/js/modalService.js)
- [`.github/instructions/frontend-instructions.md`](../../.github/instructions/frontend-instructions.md)
