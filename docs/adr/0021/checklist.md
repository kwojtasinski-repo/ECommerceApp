## Conformance checklist

- [x] `ExceptionResponse` has `IReadOnlyList<ErrorCodeDto>? Codes` property
- [x] `ErrorMapToResponse` maps `BusinessException._codes` into `ExceptionResponse.Codes`
- [x] Non-`BusinessException` mappings pass `codes: null` — no regression for `500` responses
- [x] `ExceptionResponse.ToString()` serializes `codes` with camelCase; omits key when null
- [x] `errors.js:showErrorFromResponse` handles both `data.codes` (array) and `data.response` (flat fallback)
- [x] `showError(errorsArray)` is unchanged — still iterates `[{code, parameters}]` array
- [x] Integration tests asserting `ExceptionResponse` body are updated for the new shape
- [ ] No new views introduce `ajaxRequest.js` after this ADR is accepted
- [ ] `modalService.js` Bootstrap 4 rewrite is listed as prerequisite in any Bootstrap 5 upgrade ADR
