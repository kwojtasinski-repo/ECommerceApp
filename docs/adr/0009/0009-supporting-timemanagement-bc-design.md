# ADR-0009: Supporting/TimeManagement BC — Scheduled and Deferred Job Design

## Status
Accepted — Amended 2026-02-26 (see § Amendment below)

## Date
2026-02-26

## Context

ECommerceApp has no background job infrastructure today. All work is request-driven:
the NBP currency rate is fetched lazily on the first HTTP request that needs it, payment
expiration is never enforced automatically, and there is no mechanism for deferred work
triggered by domain events (e.g. "expire this order if unpaid in 15 minutes").

As the codebase matures toward the target architecture (ADR-0002), several BCs need
time-based processing:

| BC | Need | Type |
|---|---|---|
| Currencies | Fetch today's NBP rates daily | Recurring |
| Payments | Expire unpaid payments periodically | Recurring |
| Orders | Cancel abandoned carts | Recurring |
| Orders / Payments | Timeout a specific payment N minutes after checkout | Deferred per-instance |
| Identity | Clean up expired tokens | Recurring |

Two fundamentally different job categories emerged during design:

**Recurring jobs** — defined once in code, fire on a cron schedule forever.
No per-instance data needed. Examples: currency rate sync, payment expiration scan.

**Deferred job instances** — created at runtime by a domain event (e.g. checkout).
Each instance carries entity-specific data (`entityId`) and a concrete `RunAt` timestamp.
Must survive app restarts — cannot live only in memory.

The following design constraints were identified:

- **Zero startup coupling to DB** — a DB outage must not prevent the app from starting
  or prevent recurring jobs from running (they are code-configured).
- **All job definitions owned by TimeManagement** — no other BC should configure cron
  expressions, retries, or timezones. Other BCs register only the handler (what to do).
- **Lazy DB record creation** — `ScheduledJob` rows are created on first execution, not
  at startup. Code is the source of truth for recurring job definitions.
- **Async dispatch** — the cron scheduler must not block waiting for a job to complete.
  A `Channel<T>` decouples scheduling from execution.
- **Module reports its own result** — after execution, the task uses a provided
  `JobExecutionContext` to report success, failure, or progress. TimeManagement records it.
- **Admin-only management UI** — administrators must be able to list all jobs, view
  execution history, enable/disable a job, and manually trigger or retry a job.

The `Supporting/TimeManagement` slot was reserved in ADR-0004's module taxonomy.
This ADR defines its concrete implementation.
### 1–10. Implementation guidance

The job taxonomy, plugin contract, dispatch pipeline, lazy persistence, deferred scheduling,
schema, admin surface, folder layout, and Cronos usage are implementation guidance. The
maintained details are in [TimeManagement implementation guide](example-implementation/job-engine.md).

The architectural decisions remain here: TimeManagement owns job definitions; recurring and
deferred jobs are distinct; task execution reports through `JobExecutionContext`; scheduling is
asynchronously decoupled from execution; and startup must not depend on the database.

---

## Consequences

### Positive

- TimeManagement is a self-contained, independently deployable module — a DB outage does not
  prevent recurring jobs from being scheduled (scheduling loop reads only in-memory config).
- Other BCs are completely decoupled from scheduling infrastructure — they implement one interface
  (`IScheduledTask`) and register one line in DI. No cron knowledge leaks outside TimeManagement.
- `JobExecutionContext` provides a structured, testable reporting mechanism: tasks can be
  unit-tested by asserting `context.Outcome` without any DB or channel involvement.
- Lazy DB record creation eliminates startup overhead and startup/DB coupling across all
  normal restarts (no writes unless config changes or first-ever run).
- `Channel<T>` async dispatch means the 30-second cron tick returns immediately regardless
  of how long individual tasks take; no timer drift or blocking.
- Admin UI works from day 1 of a fresh deploy: job list comes from in-memory `IScheduleConfig`;
  DB data fills in as jobs run.
- The `IDeferredJobScheduler` contract cleanly separates the checkout/order flow from the
  scheduling mechanism — `OrderService` never imports infrastructure.

### Negative

- `Cronos` is a new NuGet dependency for `ECommerceApp.Infrastructure`.
- Jobs that have never run (fresh deploy) are invisible in the DB until their first execution.
  DB-based admin queries cannot show them; the merged in-memory/DB view in `IJobManagementService`
  is required to display the full list correctly.
- Three `BackgroundService` instances run concurrently — this must be considered in integration
  test setup (`CustomWebApplicationFactory` may need hosted services disabled for non-job tests).
- `DeferredJobInstances` table grows unboundedly without a cleanup job; a `DeferredInstanceCleanup`
  recurring job should be added in a follow-up.

### Risks & mitigations

- **Risk**: Cron tick fires at startup before `JobDispatcherService` channel reader is ready.
  **Mitigation**: `Channel.CreateUnbounded` buffers writes; the dispatcher processes them in order
  once it starts. No messages are lost.
- **Risk**: Multiple app instances (future) both poll `DeferredJobInstances` and double-execute.
  **Mitigation**: `UPDATE ... WHERE Status = Pending` optimistic guard in `DeferredJobPollerService`
  — only one instance wins the status transition to `Running`.
- **Risk**: A `IScheduledTask` implementation has a name that doesn't match any registered config.
  **Mitigation**: `JobDispatcherService` logs a warning and skips; no exception propagates.
- **Risk**: DB migration for `time_management` schema fails mid-deploy.
  **Mitigation**: migration must be reviewed per `migration-policy.md`, run in a transaction,
  and validated on staging before production. Recurring jobs still function without the schema
  (they degrade to in-memory-only mode; deferred jobs are unavailable until migration completes).

---

## Alternatives considered

- **Hangfire** — rejected because it introduces a paid/licensed dependency with its own DB schema
  and dashboard middleware. The built-in `BackgroundService` + `Channel<T>` covers all requirements
  without additional packages (beyond Cronos).
- **Quartz.NET** — rejected as over-engineered for a single-instance monolith; its clustering and
  persistence model adds operational overhead not justified at current scale.
- **`System.Timers.Timer` per job** — rejected; timer `Elapsed` callback runs on a thread-pool
  thread with `async void`-like semantics that swallow exceptions and can leak timers on disposal.
  `BackgroundService` + `Task.Delay` loop is safer and observable.
- **Startup DB sync** — rejected; syncing all code configs to DB on every restart creates DB
  startup coupling, multi-instance write contention, and unnecessary latency. Lazy creation
  achieves the same result without these costs.
- **Async event bus for job dispatch** — rejected for Phase 1; no message bus exists in the
  codebase (ADR-0002 defers this). `Channel<T>` provides equivalent in-process async decoupling.
  Migration to event-based dispatch is possible later by changing only `IScheduledTask`
  implementations — the scheduler and `JobDispatcherService` are unaffected.
- **Config in each BC** — rejected; scattering `AddCronJob<T>` calls across Currencies,
  Payments, etc. makes it impossible to reason about the full job schedule from a single
  location. TimeManagement must own all job definitions.

---

## References

- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../0002/0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0004 — Module Taxonomy and Bounded Context Grouping](../0004/0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0008 — Supporting/Currencies BC Design](../0008/0008-supporting-currencies-bc-design.md)
- [`docs/architecture/bounded-context-map.md`](../../architecture/bounded-context-map.md)
- [`docs/patterns/implementation-patterns.md`](../../patterns/implementation-patterns.md)
- [`.github/instructions/dotnet.instructions.md`](../../../.github/instructions/dotnet.instructions.md) § 16, § 17
- Issues / PRs: <!-- link -->
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
