# Deferred job lease and the InMemory test decorator

Read this before changing how `DeferredJobPollerService` picks up deferred job rows, adding a deferred job,
or writing tests that run the poller on an InMemory host. It follows the same pattern as the Outbox lease,
see [ADR-0010: outbox lease](../0010/outbox-claim-and-inmemory-test-decorator.md).

## Why a lease exists

`DeferredJobPollerService` reads due rows with a plain `SELECT` and used to mark them `Running` with a
read-modify-write. `DeferredJobInstance` has no concurrency token, so two poller instances (several hosts on
one database: scale-out, rolling deploy with overlap) could both read the same `Pending` row, both mark it
`Running` (the second write silently wins) and both put it into their own in-process trigger channel, so
both hosts ran the job. This was reproduced against SQL Server before the fix: 12 poller instances released
together dispatched the same row in 25 of 25 repetitions.

`IDeferredJobInstanceRepository.TryAcquireLeaseAsync(id, now, leaseExpiresAt)` closes that gap. A **lease** is
a time-limited, exclusive right to run one row. In production it is one conditional `UPDATE`
(`ExecuteUpdateAsync`) that flips the row to `Running` and sets `LockExpiresAt` only if the row is still
leasable:

- **DUE**: `Pending` and `RunAt <= now` (nobody owns it; `RunAt` is the scheduled time or the retry backoff);
- **ZOMBIE**: `Running` and `LockExpiresAt < now` (the owner's lease ran out without a result being persisted,
  so the owner is presumed dead and the row is taken over).

A `Running` row with a live lease is never leasable. The database serializes concurrent writers on the row,
so exactly one caller gets `true`; every other caller matches zero rows and the poller skips the row without
logging an error. No migration, no row version. It is a lease and not a lock held while the job runs
because a job can be slow and its process can die mid-way: the right to the row must expire on its own,
otherwise one crashed host would freeze the job forever.

The poller reads its candidates untracked (a snapshot) and then, per candidate and in its own scope, acquires
the lease. For a DUE row it writes the trigger request to the channel. For a ZOMBIE row it resets the row to
`Pending` with a retry (`ResetZombie`) and does not run it immediately; only the host that won the lease does
the reset, so two hosts no longer reset the same zombie. The candidate's status, read before the lease,
decides which of the two applies.

The business reasoning for these exact conditions is written on the interface method itself
(`IDeferredJobInstanceRepository.TryAcquireLeaseAsync`), together with a DOC-SYNC list of everything to update
when they change (the candidate query in `DeferredJobPollerService.PollAsync`, `DeferredJobInstanceRepository`,
`InMemoryDeferredJobLeaseDecorator`, the poller, both test classes and this document). The column keeps its
historical name `LockExpiresAt`; it holds the lease expiry.

## Which jobs it protects

Every deferred job goes through this poller, so the lease protects all of them from running on two hosts at
once. Their own tolerance to a second run (from reading the jobs, not from running them):

| Job | If it ran twice |
|-----|-----------------|
| `SoftReservationExpiredJob` | second run may hit a deleted row and report an error; end state correct |
| `PaymentWindowExpiredJob` | `Payment` has a row version, so the loser conflicts and rolls back; no duplicate event |
| `StockAdjustmentJob` | same quantity, but the audit entry and `StockAvailabilityChanged` can be written twice |
| `GuestAccountLinkCheckJob` | duplicate verification code or mail to the user |
| `SnapshotOrderItemsJob` | looked idempotent, not verified |

## What the lease does not cover

- **Recurring jobs (`IScheduledTask`).** `CronSchedulerService` computes the next occurrence from a per-process
  monitor and enqueues each recurring job on every host independently. There is no row to lease, so the lease
  does not apply. That is a separate coordination problem and still open.
- **Redelivery after a crash.** A job that ran but whose result was not persisted becomes a zombie after the
  lease window (5 minutes) and is run again; inherent to at-least-once. Jobs should tolerate a repeat.
- **Interleaving with other writers.** The lease stops two pollers from running one row, not a job from racing a
  different code path that changes the same data.
- **Frequency in production.** The reproduction releases all pollers at the same instant. In production the
  pollers run every 10 seconds with independent phase, so the window is hit less often. Whether production runs
  more than one host on one database was not verified.

## Why InMemory hosts need a decorator

`ExecuteUpdateAsync` is relational-only. On the InMemory provider (`BcWebApplicationFactory`, `BcBaseTest`) it
throws `InvalidOperationException`. The poller catches per-candidate exceptions, so the failure is silent: no
deferred job ever runs and tests hang or time out with no obvious cause.

Production code carries only the relational implementation. For InMemory hosts `BcWebApplicationFactory`
decorates `IDeferredJobInstanceRepository` with
`ECommerceApp.Shared.TestInfrastructure.InMemoryDeferredJobLeaseDecorator`:

- it replaces only `TryAcquireLeaseAsync` (read, check and write under a process-wide gate, then detaches the
  entity so a later load of the row in the same scope reads fresh state) and delegates every other member;
- InMemory never leaves the process, so the gate gives the same exactly-one-winner guarantee the database gives
  in production;
- it is registered in `BcWebApplicationFactory.ConfigureWebHost`, after the InMemory swap, next to the Outbox
  decorator. Hosts that derive directly from `CustomWebApplicationFactory` and the SQL Server hosts
  (`E2E.Backend`, `Web.E2E`) do not get it; the SQL Server hosts run the real lease.

The decorator can see the internal `ITimeManagementDbContext` through the existing
`InternalsVisibleTo("ECommerceApp.Shared.TestInfrastructure")` on `ECommerceApp.Infrastructure`.

## Tests

| Test | Project | What it proves |
|------|---------|----------------|
| `DeferredJobLeaseConcurrencyE2ETests` | `ECommerceApp.E2E.Backend` (real SQL Server) | The production lease: exactly one winner out of 12 concurrent attempts, 25 repetitions, for a due row and a zombie row; a live lease and a not-yet-due row are not leased; and at poller level, 8 real `DeferredJobPollerService` instances racing for one due row dispatch it exactly once. The only test of the real behaviour. |
| `DeferredJobLeaseInMemoryDecoratorTests` | `ECommerceApp.IntegrationTests` | The decorator is active in InMemory hosts, and the stand-in keeps the exactly-one-winner contract. Does not prove production behaviour. |

Both are written to be repeated: re-run them, or copy the pattern, whenever the lease or the poller changes.
Each carries a class-level note pointing here.

## Rules for anyone adding deferred job functionality

1. A new atomic operation on `DeferredJobQueue` rows (anything that must have exactly one winner) goes on
   `IDeferredJobInstanceRepository` as one conditional statement, not as read-then-write.
2. If it uses a relational-only API (`ExecuteUpdate`, `ExecuteDelete`, raw SQL), add the InMemory stand-in to
   `InMemoryDeferredJobLeaseDecorator` (or a sibling decorator) and note it here. Otherwise InMemory hosts fail
   silently.
3. Test the real behaviour in `E2E.Backend` against SQL Server. The InMemory stand-in is not evidence.
4. Keep the lease predicate identical to the poller's candidate query (DUE or ZOMBIE).
5. A new deferred job should tolerate a repeat run (at-least-once, zombie recovery).
6. `BcWebApplicationFactory` throws at host build time if `IDeferredJobInstanceRepository` is not registered by
   implementation type, because the decorator cannot be applied otherwise.

## Known limits

- No test runs the whole host with several real `JobDispatcherService` instances; the poller-level test stops at
  the trigger channel.
- The InMemory stand-in has no concurrency test against a real race (it cannot have one); its test checks the
  contract inside one process only.
- The zombie path is covered at repository level (one lease winner), not at poller level.
