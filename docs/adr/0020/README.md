# ADR-0020: Backoffice BC

**Status**: Accepted
**BC**: Backoffice

## What this decision covers
9 read-only aggregation services delegating to per-BC services,
9 controllers in `Areas/Backoffice`, 21 Razor views, `ManagingRole` authorization.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0020-backoffice-bc-design.md | Full design: service aggregation pattern, controller list, authorization | Working with backoffice features |

## Key rules
- Backoffice services have NO domain model — they delegate to per-BC services only
- All controllers: `[Authorize(Roles = ManagingRole)]`
- Read-only — no write operations in Backoffice BC

## Related ADRs
- ADR-0024 (routing) — Areas/Backoffice controller routing
