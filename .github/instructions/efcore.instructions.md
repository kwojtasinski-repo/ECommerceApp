---
description: "EF Core guidance for ECommerceApp.Infrastructure including tracking, transactions, and migrations policy." 
applyTo: "ECommerceApp.Infrastructure/**/*.cs, ECommerceApp.Infrastructure/**/*.csproj"
---

# EF Core Guidelines for ECommerceApp

Purpose
- Safe EF Core usage patterns and migration guidelines for `ECommerceApp.Infrastructure`.

Change tracking & detach
- Use `AsNoTracking()` for read-only queries to improve performance.
- When updating entities fetched in the same context, use `DetachEntity()` to avoid tracking conflicts or use the repository Update methods.
- Prefer repository-level detach semantics instead of direct `DbContext` manipulation in services.

Transactions
- For multi-entity operations spanning multiple repositories, use explicit transactions via `_context.Database.BeginTransactionAsync()` and commit/rollback accordingly.
- Ensure `SaveChangesAsync()` is called before publishing integration events or using outbox.

Migrations policy (summary)
- Migration files live under `ECommerceApp.Infrastructure/Migrations/`.
- Do NOT modify existing migration files once applied to shared environments.
- Creating new migrations requires:
  - Unit/integration tests covering schema changes where feasible.
  - Review and approval from maintainers.
  - A DB migration plan in the PR (impact, downtime, rollback commands).

Outbox & events
- For any domain events that must be published reliably, implement the outbox pattern (persist event in DB within the same transaction, use background publisher).

Performance
- Use projection queries where possible to avoid unnecessary data loading (select only required columns).
- Use pagination for large queries (Skip/Take) and ensure proper indexing.

Seeding
- Use `DatabaseInitializer` or `Seed` classes. Seeding logic must be idempotent and only run in dev/test contexts unless explicitly needed in production.

Bulk operations
- For bulk deletes/updates prefer batched SQL or EF Core `ExecuteSqlRawAsync` with caution; wrap in transaction and test thoroughly.

