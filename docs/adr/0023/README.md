# ADR-0023: Bootstrap 5 Upgrade

**Status**: Accepted
**BC**: Web (frontend)

## What this decision covers
Migration from Bootstrap 4 to Bootstrap 5.3.3, TomSelect 2.4.1 installation,
`modalService` rewrite for BS5 API, and removal of BS4 jQuery plugin calls.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0023-bootstrap-5-upgrade.md | Full migration: BS5 changes, modalService rewrite, TomSelect | Writing new views or editing existing BS components |
| example-implementation/bs5-component-migration.md | Before/after BS4→BS5 component examples | Migrating a remaining BS4 component |

## Key rules
- No BS4 `data-toggle`, `data-dismiss`, `data-target` attributes — use `data-bs-*`
- `modalService` uses BS5 `bootstrap.Modal` API — not jQuery `.modal()`
- TomSelect replaces Select2 for all `<select>` enhancements

## Related ADRs
- ADR-0021 (frontend pipeline) — modalService error handling
- ADR-0022 (navbar) — navbar uses BS5 classes
