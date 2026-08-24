# ADR-0032: Cross-Schema Transactional Outbox via Explicit Shared Connection

## Status
Accepted — monolith-scoped, revisit trigger defined in §ExitPath

## Date
2026-08-24

## Context

The transactional outbox pattern requires that a business-state write and its corresponding
outbox row commit **atomically** — either both happen or neither does. Every BC unit of work
(`InventoryUnitOfWork`, `OrdersUnitOfWork`, `CouponsUnitOfWork`, `PaymentsUnitOfWork`,
`CatalogUnitOfWork`, `FulfillmentUnitOfWork`, `SagaUnitOfWork`) needs this guarantee.

The Outbox table itself, however, does not live inside each BC's own schema. It is centralized
in one schema, owned by `MessagingDbContext`:

```csharp
// ECommerceApp.Infrastructure/Messaging/MessagingDbContext.cs
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.HasDefaultSchema(MessagingConstants.SchemaName);   // e.g. "messaging"
    ...
}
```

So a write like `RefundApprovedHandler` updating `StockItem` (schema: `availability`) and
enqueuing a `RefundStockReturned` outbox row (schema: `messaging`) touches **two different
`DbContext` instances**. EF Core has no built-in mechanism for one ambient transaction to span
two separate `DbContext` objects — `context.Database.BeginTransactionAsync()` on one context
knows nothing about a transaction opened on another. Something has to explicitly bridge them.

Today's mechanism, `CrossContextTransactionScope`, bridges them by sharing the physical ADO.NET
connection and `DbTransaction` object between the two contexts:

```csharp
// ECommerceApp.Infrastructure/Database/CrossContextTransactionScope.cs
public TSecondaryContext CreateSecondaryContext<TSecondaryContext>() where TSecondaryContext : DbContext
{
    var sharedConnection = _primary.Database.GetDbConnection();
    var optionsBuilder = new DbContextOptionsBuilder<TSecondaryContext>().UseSqlServer(sharedConnection);
    var secondaryContext = (TSecondaryContext)Activator.CreateInstance(typeof(TSecondaryContext), optionsBuilder.Options)!;
    secondaryContext.Database.UseTransaction(_primary.Database.CurrentTransaction!.GetDbTransaction());
    return secondaryContext;
}
```

`OutboxWriter.EnqueueAsync(message, transaction, ct)` calls `CreateSecondaryContext<MessagingDbContext>()`
to get a `MessagingDbContext` instance that writes on the *same connection and transaction* as
whatever BC opened it. The caller must therefore hand the writer an explicit `IOutboxTransaction`
— there is no way for `OutboxWriter` to discover "the current ambient transaction" on its own,
because it belongs to a different `DbContext` entirely.

This only works because ECommerceApp is, today, **one physical SQL Server database** — all BC
schemas and the messaging schema sit on the same connection. That precondition is the load-bearing
assumption of this entire ADR.

## Decision

Keep the explicit-parameter mechanism as-is:

1. Each BC's `IXxxUnitOfWork.BeginTransactionAsync()` returns an `IOutboxTransaction` wrapping a
   `CrossContextTransactionScope` over that BC's primary `DbContext`.
2. Any write that must reach the `messaging` schema atomically — i.e. any outbox enqueue — is
   done via `IOutboxWriter.EnqueueAsync(message, transaction, ct)`, passing that same
   `IOutboxTransaction` down explicitly through the call stack.
3. Domain/application services that are called *underneath* an already-open transaction (e.g.
   `StockService` invoked by `RefundApprovedHandler`) must never open a second transaction of
   their own. They receive the caller's `IOutboxTransaction` through a dedicated overload instead.
   (This half of the decision is a plain unit-of-work ownership rule, not a cross-schema concern —
   see the companion fix logged as `[KI-014]` in `.github/context/known-issues.md`. It is called
   out here only because the same `IOutboxTransaction` parameter carries both concerns and it is
   easy to conflate them.)

No ambient/ThreadLocal/scoped-DI "current transaction" accessor is introduced. Callers that need
a transaction must be handed one explicitly, by an ancestor in the same call stack that opened it.

## Consequences

### Positive
- No implicit control flow: reading a method signature tells you whether it can run inside a
  caller's transaction or always owns its own. This matters most in exactly the class of bug this
  ADR's companion fix addresses — nested-transaction crashes are easy to introduce silently with
  ambient state, and this codebase has already been bitten by exactly that (see `[KI-014]`).
- Avoids `System.Transactions.TransactionScope` entirely, and with it MSDTC (Distributed
  Transaction Coordinator) promotion, which triggers the moment a `TransactionScope` spans two
  physical connections/contexts. MSDTC is typically unavailable in Linux containers, which would
  break the Testcontainers-based E2E suite (`ECommerceApp.E2E.Backend`) outright.
- Matches the existing "avoid service locator, prefer constructor/parameter injection" convention
  already followed elsewhere in this codebase.

### Negative
- **This mechanism cannot survive a real service extraction.** The moment any BC schema and the
  `messaging` schema are no longer on the same physical connection — because they are now separate
  databases behind separate services — `CreateSecondaryContext`'s `UseTransaction(...)` call has
  nothing to share. There is no substitute "shared transaction" between two different processes.
  This ADR's mechanism is monolith-only scaffolding, not a design that generalizes.
- Every service method that must be transaction-joinable needs two overloads: one that owns its
  transaction, one that accepts the caller's. This is boilerplate that must be applied
  *consistently* — a method missing its transaction-accepting overload, called from a handler that
  already has one open, reproduces the exact nested-transaction bug this ADR's companion fix
  closed for `RefundApprovedHandler`/`OrderPlacedHandler`/`OrderShippedHandler`/
  `ShipmentStockOutcomeHandlerBase`. Any new mutating method on a BC service needs its
  transaction-accepting twin added at the same time it is written, not retrofitted after a crash.

### Risks & mitigations
- **Risk**: a new handler opens a transaction, then calls a service method that still only has
  the transaction-owning overload → runtime `InvalidOperationException` ("connection already in a
  transaction"), discoverable only against a real relational provider (unit tests with mocked
  `IStockService`/`IInventoryUnitOfWork` cannot catch it — the InMemory EF provider doesn't
  enforce transaction semantics either).
  **Mitigation**: code review checklist item — any handler that opens `_unitOfWork.BeginTransactionAsync()`
  and then calls a service method must use that service's transaction-accepting overload, never the
  parameterless one. Backend E2E tests against real SQL Server (`ECommerceApp.E2E.Backend`,
  Testcontainers) are the only test tier that actually exercises this — extend E2E coverage
  whenever a new handler is added in this shape.

## Alternatives considered

- **Ambient transaction via scoped DI accessor** (`ICurrentTransactionAccessor` set on
  `BeginTransactionAsync`, read implicitly by `OutboxWriter.EnqueueAsync(message, ct)` and by every
  BC's `BeginTransactionAsync` to decide whether to open a new transaction or join the existing
  one). Rejected for now: it removes the overload-duplication boilerplate, but reintroduces
  implicit control flow — a call site with no visible transaction parameter could silently join
  an unrelated ambient transaction opened several frames up the stack, which is a harder class of
  bug to spot in review than a missing overload. It also does not remove the underlying
  cross-schema coupling — it only hides where it is. Worth reconsidering only together with a
  concrete need to cut boilerplate, not as a way to avoid item 2 below.
- **`System.Transactions.TransactionScope`** — rejected. True ambient transaction, zero explicit
  plumbing, but escalates to MSDTC as soon as it spans two physical connections/contexts, which
  this cross-schema write always does. MSDTC is often unavailable in containerized CI (the
  Testcontainers-based SQL Server used by `ECommerceApp.E2E.Backend` runs on Linux); relying on it
  would make the E2E suite this codebase has invested heavily in unreliable or unrunnable in CI.
- **Per-schema local Outbox table** (each BC's own `DbContext` owns its own `Outbox` table, written
  in the same ordinary same-schema transaction as its business data; the dispatcher polls N tables
  instead of one central `messaging.Outbox`). This is the architecturally correct target — it
  removes the cross-schema transaction-sharing requirement at the root, and it is also the *only*
  shape that survives a real service extraction unchanged (each extracted service already owns its
  local outbox table and local transaction; nothing to migrate away from). **Deferred, not
  rejected** — see §Exit path. Not done now because it is a materially larger change (dispatcher
  polling logic, per-BC migrations, per-schema EF configuration) that is not justified while every
  BC still lives in one physical database.

## Exit path

Revisit this ADR — and do the per-schema Outbox migration described above — at the point where
extracting any bounded context into a separately deployable service becomes a concrete, scheduled
piece of work, **before** starting that extraction. Do not attempt to extract a BC while its
handlers still depend on `CrossContextTransactionScope` reaching into another schema's tables —
there will be nothing left to share a connection with. Until that point, the mechanism described
here is the accepted trade-off and should not be re-litigated without a concrete extraction plan
driving it.

## References

- [ADR-0010 — In-Memory Message Broker for Cross-BC Communication](../0010/0010-in-memory-message-broker-for-cross-bc-communication.md)
- [ADR-0011 — Inventory/Availability BC Design](../0011/0011-inventory-availability-bc-design.md) (first concrete user of this mechanism, via `StockService`)
- [ADR-0013 — Per-BC DbContext Interfaces](../0013/0013-per-bc-dbcontext-interfaces.md) (companion compiler-enforced schema isolation)
- [`.github/context/known-issues.md`](../../../.github/context/known-issues.md) — `[KI-014]` (companion nested-transaction fix)
- `ECommerceApp.Infrastructure/Database/CrossContextTransactionScope.cs`
- `ECommerceApp.Infrastructure/Messaging/OutboxWriter.cs`

## Reviewers

- @team/architecture
