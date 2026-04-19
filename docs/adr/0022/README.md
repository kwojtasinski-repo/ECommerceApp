# ADR-0022: Navbar Two-Tier Redesign

**Status**: Accepted
**BC**: Web (frontend)

## What this decision covers
Top navigation bar (search + category filter + cart badge + user menu) and
secondary nav (Kategorie for guests; management bar for MaintenanceRole).

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0022-navbar-two-tier-redesign.md | Full design: two-tier layout, Razor partials, role-based display | Modifying navigation |
| example-implementation/navbar-razor-partial.md | Razor partial structure for navbar tiers | Editing _Layout.cshtml or navbar partials |

## Key rules
- `_LoginPartial.cshtml` is retired — user menu lives in top navbar
- Management bar visible only to `MaintenanceRole`
- Cart badge uses AJAX polling — do not replace with SSE without a new ADR

## Related ADRs
- ADR-0023 (Bootstrap 5) — navbar uses BS5 classes
