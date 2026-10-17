# Outbox claim and the InMemory test decorator

Terminology: the operation is called a **lease** (`TryAcquireLeaseAsync`). Older names ("claim",
`TryClaimAsync`, `TryClaimForDispatchAsync`) and the file/class names `OutboxClaim*` refer to the same
thing.

Read this before changing how `OutboxPollerService` picks up Outbox rows, adding any new Outbox
operation that must be atomic, or writing tests that run the poller on an InMemory host.

## Why a claim exists

`OutboxPollerService` reads due rows with `GetDueAsync` (a plain `SELECT`) and used to mark them
`Running` with a read-modify-write. Nothing stopped two poller instances (several hosts on one
database, or a poller racing a hand-written publish in a test) from reading the same `Pending` row and
both dispatching it. The `Outbox` table has no concurrency token, so the second write silently won
instead of failing.

`IOutboxRepository.TryAcquireLeaseAsync(id, now, leaseExpiresAt)` closes that gap. A **lease** is a
time-limited, exclusive right to dispatch one row. In production it is one conditional `UPDATE`
(`ExecuteUpdateAsync`) that flips the row to `Running` and sets `LockExpiresAt` only if the row is still
leasable:

- **DUE** — `Pending` and `NextAttemptAt <= now` (nobody owns it, and its retry backoff has elapsed);
- **ZOMBIE** — `Running` and `LockExpiresAt < now` (the owner's lease ran out without
  `MarkDispatched`/`Fail`, so the owner is presumed dead and the row is taken over).

A `Running` row with a live lease is never leasable. The database serializes concurrent writers on the
row, so exactly one caller gets `true`; every other caller matches zero rows and skips the row without
logging an error. No migration, no row version. It is a lease and not a lock held across the dispatch
because dispatch can be slow and the process can die mid-way: the right to the row must expire on its own,
otherwise one crashed poller would freeze the event forever. The poller treats the candidate list as a
snapshot: the lease, not the read, decides who dispatches.

The business reasoning for these exact conditions is written on the interface method itself
(`IOutboxRepository.TryAcquireLeaseAsync`), together with a DOC-SYNC list of everything to update when
they change (`GetDueAsync`, `OutboxRepository`, `InMemoryOutboxClaimDecorator`, `OutboxPollerService`, both
test classes and this document). The lease is specific to the Outbox dispatch transition
(`→ Running`); it is not a generic "lease any row" primitive. `GetDueAsync`, the production lease and the
InMemory stand-in share one predicate and must be changed together. The column keeps its historical name
`LockExpiresAt`; it holds the lease expiry.

## What the claim does not cover

- **Redelivery after a crash.** If the handler ran but the process died before `MarkDispatched`, the row
  becomes a zombie after the lock window (5 minutes) and is delivered again. That is inherent to
  at-least-once delivery. Handlers that apply deltas need Inbox deduplication
  (`IIdAwareMessageHandler`); handlers that recompute from current state are safe on their own.
- **Interleaving with other writers.** The claim stops two pollers from dispatching one row. It does not
  stop a handler from racing a different code path that changes the same data.

## Why InMemory hosts need a decorator

`ExecuteUpdateAsync` is relational-only. On the InMemory provider (`BcWebApplicationFactory`,
`BcBaseTest`, so most of `ECommerceApp.IntegrationTests` and the service-level suites) it throws
`InvalidOperationException`. The poller catches per-message exceptions, so the failure is silent: rows
are never dispatched and tests hang or time out with no obvious cause.

Production code carries only the relational implementation. For InMemory hosts,
`BcWebApplicationFactory` decorates `IOutboxRepository` with
`ECommerceApp.Shared.TestInfrastructure.InMemoryOutboxClaimDecorator`:

- it replaces only `TryAcquireLeaseAsync` (read, check and write under a process-wide gate, then
  detaches the entity so the poller's later `UpdateAsync` does not hit an identity conflict) and
  delegates every other member to the real repository;
- InMemory never leaves the process, so the gate gives the same exactly-one-winner guarantee the database
  gives in production;
- it is registered in `BcWebApplicationFactory.ConfigureWebHost`, after the InMemory swap. Hosts that
  derive directly from `CustomWebApplicationFactory` do not get it, and neither do the SQL Server hosts
  (`E2E.Backend`, `Web.E2E`), which run the real claim.

The decorator needs `InternalsVisibleTo("ECommerceApp.Shared.TestInfrastructure")` on
`ECommerceApp.Infrastructure` because `IMessagingDbContext` is internal.

## Tests

| Test | Project | What it proves |
|------|---------|----------------|
| `OutboxClaimConcurrencyE2ETests` | `ECommerceApp.E2E.Backend` (real SQL Server) | The production claim: exactly one winner out of 12 concurrent attempts, 25 repetitions, for a pending row and for a zombie row; a live lock is not claimed. The only test of the real behaviour. |
| `OutboxClaimInMemoryDecoratorTests` | `ECommerceApp.IntegrationTests` | The decorator is active in InMemory hosts, and the stand-in keeps the exactly-one-winner contract. Does not prove production behaviour. |

Both are written to be repeated: re-run them, or copy the pattern, whenever the claim or the poller
changes. Each carries a class-level note pointing here.

## Rules for anyone adding Outbox functionality

1. A new atomic Outbox operation (anything that must have exactly one winner) goes on `IOutboxRepository`
   as one conditional statement, not as read-then-write.
2. If it uses a relational-only API (`ExecuteUpdate`, `ExecuteDelete`, raw SQL), add the InMemory stand-in
   to `InMemoryOutboxClaimDecorator` (or a sibling decorator) and note it here. Otherwise InMemory hosts
   fail silently.
3. Test the real behaviour in `E2E.Backend` against SQL Server. The InMemory stand-in is not evidence.
4. Keep the claim predicate identical to `GetDueAsync`.
5. When the decorator cannot be applied, `BcWebApplicationFactory` throws at host build time: it requires
   `IOutboxRepository` to be registered by implementation type.

## Known limits

- No test runs several real poller instances against one database; the E2E test calls the claim directly.
- The InMemory stand-in has no concurrency test against a real race (it cannot have one); its test checks
  the contract inside one process only.
