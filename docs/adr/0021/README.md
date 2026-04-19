# ADR-0021: Frontend Error Pipeline and JS Migration Strategy

**Status**: Accepted
**BC**: Web (frontend)

## What this decision covers
`ExceptionResponse` + `errors.js` pipeline, fetch-first new-code policy,
AMD module cleanup, `addObjectPropertiesToGlobal` removal, and `DOMInitialized` event-data pattern.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0021-frontend-error-pipeline-and-js-migration-strategy.md | Full design: error pipeline phases 1–4, JS migration rules | Writing new JS or handling AJAX errors |
| example-implementation/fetch-first-pattern.md | How to write fetch-first JS (replacing jQuery AJAX) | Writing new frontend JS |

## Key rules
- New JS code uses `fetch` + `errors.js` pipeline — no new jQuery AJAX calls
- `showErrorFromResponse` handles both structured `data.codes` and flat `data.response`
- Phase 4 complete: BS5 modalService rewritten, AMD cleanup done

## Related ADRs
- ADR-0023 (Bootstrap 5) — modalService depends on BS5 API
