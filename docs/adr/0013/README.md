# ADR-0013: Per-BC DbContext Interfaces

**Status**: Accepted
**BC**: All BCs (infrastructure rule)

## What this decision covers
Each BC exposes an `IXxxDbContext` interface in the Infrastructure layer.
Repositories depend on the interface, not the concrete `DbContext`.
Enables easy testing and decoupling.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0013-per-bc-dbcontext-interfaces.md | Full design: interface shape, DI alias registration, 27 repos updated | Creating a new repository |
| example-implementation/dbcontext-interface-pattern.md | IXxxDbContext definition + DI alias registration | Adding a new BC repository |

## Key rules
- Repositories MUST inject `IXxxDbContext`, never the concrete class
- DI alias: `services.AddScoped<IXxxDbContext>(sp => sp.GetRequiredService<XxxDbContext>())`
- All 10 BC DbContexts have interfaces — completed

## Related ADRs
- ADR-0003 (folder structure) — where interfaces live
