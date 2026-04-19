## Migration plan

1. New Catalog BC implementation is complete (parallel to legacy `Domain.Model.Item`).
2. Migration (`InitCatalogSchema`) targets `ProductDbContext` and creates `catalog.*` schema.
3. Migration requires explicit approval per `migration-policy.md`.
4. Switch to new Catalog BC in Web/API controllers is a separate step tracked in the
   bounded-context-map refactoring progress tracker.
5. Legacy `Domain.Model.Item`, `Image`, `Tag`, `Brand`, `Type` and related services/repositories
   are removed only after the atomic switch.
