# ADR-0024: Controller Routing Strategy

**Status**: Accepted
**BC**: Web (routing rule for all BCs)

## What this decision covers
Convention for `[Area]`, `[Route]`, and `[HttpGet/Post]` attributes across all BC controllers.
All new BC controllers live under `Web/Areas/{BC}/Controllers/`.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0024-controller-routing-strategy.md | Full routing rules: area convention, attribute routing, action naming | Creating a new controller or action |
| example-implementation/area-routing-examples.md | Route attribute examples for standard CRUD actions | Writing new controller actions |

## Key rules
- All BC controllers: `[Area("{BC}")]` + `[Route("[area]/[controller]/[action]")]`
- Action names follow: Index, Create, Edit, Details, Delete convention
- No legacy `Controllers/` folder controllers remain

## Related ADRs
- ADR-0003 (folder structure) — where controllers live
