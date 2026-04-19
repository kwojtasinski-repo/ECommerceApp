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
13. ✅ Remove `addObjectPropertiesToGlobal` from `config.js`; pass modules as `DOMInitialized`
    event data (`$(document).trigger('DOMInitialized', [modules])`). All views destructure
    required modules from the event argument. `window.PagerClick` kept as explicit global
    for inline `onclick` HTML attributes in paginated list views.
