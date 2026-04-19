# ADR-0003: Feature Folder Organization for New BC Code

**Status**: Accepted
**BC**: All BCs (structural rule)

## What this decision covers
Canonical folder layout for any new bounded context: where Domain, Application,
Infrastructure, and Web layers live, and how to name things.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0003-feature-folder-organization-for-new-bounded-context-code.md | Folder rules, naming conventions, layer boundaries | Creating any new BC file |
| example-implementation/bc-folder-structure-example.md | Concrete folder tree for a sample BC | Setting up a new BC |

## Key rules
- All new BC code goes in `Domain/{BC}/`, `Application/{BC}/`, `Infrastructure/{BC}/`, `Web/Areas/{BC}/`
- No cross-BC folder references — each BC is a silo

## Related ADRs
- ADR-0004 (module taxonomy) — which BCs exist and their grouping
- ADR-0013 (DbContext interfaces) — Infrastructure layer rules
