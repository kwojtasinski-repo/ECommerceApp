## Migration plan

The migration is incremental and follows the Strangler Fig pattern:

1. All **new BCs** (Availability, Fulfillment, Communication, etc.) are created directly in the
   new feature-folder structure — no migration needed.
2. **Existing BCs** are migrated one at a time, only when a dedicated follow-up ADR is accepted
   for that BC's refactoring (per ADR-0002 refactoring progress tracker in
   `docs/architecture/bounded-context-map.md`).
3. The `Context.cs` `DbSet` registrations and `DependencyInjection.cs` registrations are updated
   incrementally as each BC migrates — no single large change.
