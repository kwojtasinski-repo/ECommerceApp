## Conformance checklist

- [ ] No existing files deleted
- [ ] New BC code lives in feature-folder structure (`Domain/<Group>/<BcName>/`) per ADR-0003
- [ ] Legacy `Context.cs` `DbSet` registrations unchanged unless a BC switch ADR is accepted
- [ ] Legacy `DependencyInjection.cs` registrations unchanged unless a BC switch ADR is accepted
- [ ] Each new per-BC `DbContext` uses a named schema (not `dbo`)
