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

---

## Decision

We implement the **Supporting/TimeManagement** bounded context as a greenfield BC following
the Parallel Change strategy (ADR-0002). No existing code is touched.

---

### 1. Job type taxonomy

| Type | Config source | DB record | Per-instance data |
|---|---|---|---|
| **Recurring** | Code (`AddTimeManagement`) | Lazy — created on first run | None |
| **Deferred** | Code (job definition only) | Required — created at checkout/event time | `EntityId`, `RunAt` |

---

### 2. Ownership rule — all definitions in TimeManagement

All job definitions (cron expression, timezone, max retries) are registered exclusively
inside TimeManagement's DI extension:

```csharp
// Infrastructure/Supporting/TimeManagement/Extensions.cs
services.AddTimeManagement(jobs =>
{
    jobs.AddRecurring("CurrencyRateSync",  cron: "15 12 * * *", maxRetries: 3);
    jobs.AddRecurring("PaymentExpiration", cron: "*/5 * * * *",  maxRetries: 3);
    jobs.AddDeferred("PaymentTimeout",                           maxRetries: 2);
});
```

Each other BC registers **only** its `IScheduledTask` implementation. No cron expressions,
no schedule configuration, no DB knowledge.

```csharp
// Application/Supporting/Currencies/Services/Extensions.cs
services.AddScoped<IScheduledTask, CurrencyRateSyncTask>();

// Application/Sales/Payments/Services/Extensions.cs
services.AddScoped<IScheduledTask, PaymentExpirationTask>();
services.AddScoped<IScheduledTask, PaymentTimeoutTask>();
```

The **job name** is the only coupling between TimeManagement (scheduler) and each BC (handler).
`IScheduledTask.TaskName` must match the name registered via `AddRecurring` / `AddDeferred`.

---

### 3. `IScheduledTask` — the plugin contract

```csharp
// Application/Supporting/TimeManagement/IScheduledTask.cs
public interface IScheduledTask
{
    string TaskName { get; }
    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}
```

`JobExecutionContext` is TimeManagement's tool injected into every execution. Tasks use it
to communicate their outcome — no return value, no exceptions required for normal flow:

```csharp
// Application/Supporting/TimeManagement/JobExecutionContext.cs
public sealed class JobExecutionContext
{
    public string? EntityId { get; }
    public string ExecutionId { get; }
    public DateTime StartedAt { get; }

    public void ReportSuccess(string? message = null);
    public void ReportFailure(string error);
    public void ReportProgress(string message);

    internal JobOutcome Outcome { get; }  // read by dispatcher after ExecuteAsync returns
}
```

Rules:
- Task must call either `ReportSuccess` or `ReportFailure` before returning.
- If the task throws without reporting, the dispatcher records it as failed (safety net).
- For recurring tasks, `EntityId` is `null`. For deferred tasks, it carries the entity reference
  (e.g. `"42"` for `OrderId`, set by `IDeferredJobScheduler.ScheduleAsync`).

---

### 4. Infrastructure components

#### 4a. `CronSchedulerService` (BackgroundService)

- Reads all `IScheduleConfig` registrations from DI (in-memory, always available).
- Ticks every **30 seconds** (half-minute granularity for standard cron minute precision).
- For each enabled config: calls `CronExpression.GetNextOccurrence(lastRunAt ?? appStartTime)`.
  If `nextOccurrence <= now`, writes a `JobTriggerRequest` to the `Channel`.
- Never calls a task directly — only writes to the channel.
- Does not access the DB. If the DB is unavailable, the scheduler continues unaffected.
- Uses **Cronos** (`Cronos` NuGet package) for cron expression parsing.

```
Tick every 30s
  foreach IScheduleConfig where IsEnabled:
    next = cron.GetNextOccurrence(lastRunAt ?? startedAt, timeZone)
    if next <= now: channel.WriteAsync(trigger)   ← non-blocking
```

#### 4b. `DeferredJobPollerService` (BackgroundService)

- Polls `DeferredJobInstances` in `TimeManagementDbContext` every **10 seconds**.
- Fetches rows `WHERE Status = Pending AND RunAt <= NOW()` ordered by `RunAt ASC`.
- For each: atomically sets `Status = Running` (optimistic guard against double-execution
  in future multi-instance scenarios), then writes a `JobTriggerRequest` to the channel.

#### 4c. `JobDispatcherService` (BackgroundService)

- Single reader of `Channel<JobTriggerRequest>`.
- For each trigger:
  1. Resolves `IScheduledTask` from a new `IServiceScope` by matching `TaskName`.
  2. Creates a `JobExecutionContext` with a new `ExecutionId`.
  3. Calls `await task.ExecuteAsync(context, ct)`.
  4. Reads `context.Outcome` — persists a `JobExecution` row and updates `ScheduledJob.LastRunAt` / `NextRunAt`.
  5. For deferred: updates `DeferredJobInstance.Status` to `Completed` or `Failed`.
  6. On `RetryCount < MaxRetries` and `Failed`: re-inserts a new `DeferredJobInstance` with
     `RunAt = now + backoff` for deferred jobs; for recurring jobs, the next cron tick handles retries.
- Exceptions from `ExecuteAsync` are caught, recorded as `Failed`, and never propagate to the host.

#### 4d. `InMemoryJobStatusMonitor` (Singleton)

- Maintains a circular buffer of the last 50 `JobExecution` records per job (in-memory).
- Used by `IJobManagementService` for the admin list view — no DB query needed for the list.
- Written by `JobDispatcherService` after every execution alongside the DB write.

#### 4e. `JobTriggerChannel` (Singleton wrapper)

- Wraps `Channel.CreateUnbounded<JobTriggerRequest>(new UnboundedChannelOptions { SingleReader = true })`.
- Shared between `CronSchedulerService`, `DeferredJobPollerService`, and `IJobTrigger`.

---

### 5. Lazy DB record creation

`ScheduledJob` rows are **not** created at startup. They are created by `JobDispatcherService`
the first time a job fires:

```
First execution of "CurrencyRateSync":
  SELECT ScheduledJob WHERE Name = "CurrencyRateSync"
  NOT FOUND → INSERT (CronExpression from code config, IsEnabled = true, defaults)
  FOUND, config changed → UPDATE CronExpression/MaxRetries from code (code wins)
  FOUND, config same → skip (zero writes)
  → proceed with execution → INSERT JobExecution row
```

**Code always wins** for `CronExpression`, `TimeZoneId`, and `MaxRetries`.
**DB wins** for `IsEnabled` — admin toggles are preserved across restarts.

Consequence: on a fresh deploy, recurring jobs have no DB record until their first fire.
The admin UI shows them from the in-memory `IScheduleConfig` registrations with status
"Never run" until a DB record exists.

---

### 6. `IDeferredJobScheduler` — runtime scheduling for domain events

```csharp
// Application/Supporting/TimeManagement/IDeferredJobScheduler.cs
public interface IDeferredJobScheduler
{
    Task ScheduleAsync(string jobName, string entityId,
        DateTime runAt, CancellationToken ct = default);
    Task CancelAsync(string jobName, string entityId,
        CancellationToken ct = default);
}
```

Called at checkout / order placement time (future `OrderService`):

```csharp
await _deferredJobScheduler.ScheduleAsync(
    jobName:  "PaymentTimeout",
    entityId: order.Id.ToString(),
    runAt:    DateTime.UtcNow.AddMinutes(15));
```

`ScheduleAsync` implementation:
1. `SELECT ScheduledJob WHERE Name = jobName` — creates the row if absent (same lazy logic).
2. `INSERT DeferredJobInstance (ScheduledJobId, EntityId, RunAt, Status = Pending, RetryCount = 0)`.

`CancelAsync` sets `Status = Cancelled` on any `Pending` instance matching `(jobName, entityId)`.

---

### 7. DB schema (`time_management` schema)

```
ScheduledJobs
  Id (int, PK, identity), Name (nvarchar 100, unique), JobType (tinyint: 0=Recurring, 1=Deferred),
  CronExpression (nvarchar 100, nullable), TimeZoneId (nvarchar 100, nullable),
  IsEnabled (bit, default 1), MaxRetries (int, default 3),
  LastRunAt (datetime2, nullable), NextRunAt (datetime2, nullable),
  ConfigHash (nvarchar 64, nullable)   ← SHA-256 of CronExpression+MaxRetries+TimeZoneId

DeferredJobInstances
  Id (int, PK, identity), ScheduledJobId (int, FK → ScheduledJobs),
  EntityId (nvarchar 200), RunAt (datetime2), Status (tinyint: 0=Pending 1=Running 2=Completed 3=Failed 4=Cancelled),
  RetryCount (int, default 0), ErrorMessage (nvarchar max, nullable), CreatedAt (datetime2)

JobExecutions
  Id (int, PK, identity), ScheduledJobId (int, FK → ScheduledJobs),
  DeferredInstanceId (int, nullable FK → DeferredJobInstances),
  Source (tinyint: 0=Scheduled 1=Manual 2=Deferred), ExecutionId (nvarchar 36),
  StartedAt (datetime2), CompletedAt (datetime2, nullable),
  Succeeded (bit), Message (nvarchar max, nullable)
```

`ConfigHash` avoids redundant UPDATE calls — dispatcher only writes if the hash has changed.

---

### 8. Admin management UI

```
Web/Controllers/JobManagementController.cs
  [Authorize(Roles = "Administrator")]

  GET  /JobManagement           — list all jobs (merged: IScheduleConfig + DB + InMemoryMonitor)
  GET  /JobManagement/History/{jobName} — execution history (DB-backed, paginated)
  POST /JobManagement/Trigger/{jobName} — manual trigger → writes to Channel via IJobTrigger
  POST /JobManagement/Enable/{jobName}  — sets ScheduledJob.IsEnabled = true
  POST /JobManagement/Disable/{jobName} — sets ScheduledJob.IsEnabled = false
```

`IJobManagementService` merges:
- All `IScheduleConfig` registrations (always present — never empty even before first run).
- `ScheduledJob` DB records (may not exist yet for jobs that never ran).
- Latest `JobExecutionRecord` from `InMemoryJobStatusMonitor` (fast, no DB).

---

### 9. Folder structure

```
ECommerceApp.Domain/Supporting/TimeManagement/
  ScheduledJob.cs                    ← aggregate root (Enable, Disable, RecordRun, SyncConfig)
  ScheduledJobId.cs                  ← TypedId<int>
  DeferredJobInstance.cs             ← entity (Schedule, MarkRunning, Complete, Fail, Cancel)
  DeferredJobInstanceId.cs           ← TypedId<int>
  JobExecution.cs                    ← entity (record of one run)
  JobExecutionId.cs                  ← TypedId<int>
  JobType.cs                         ← enum: Recurring | Deferred
  JobStatus.cs                       ← enum: Enabled | Disabled
  DeferredJobStatus.cs               ← enum: Pending | Running | Completed | Failed | Cancelled
  IScheduledJobRepository.cs
  IDeferredJobInstanceRepository.cs
  IJobExecutionRepository.cs
  Events/
    JobExecutionCompleted.cs         ← record (past-tense domain event)
    JobExecutionFailed.cs            ← record
  ValueObjects/
    CronSchedule.cs                  ← sealed record, validates via Cronos
    JobName.cs                       ← sealed record, non-empty, max 100

ECommerceApp.Application/Supporting/TimeManagement/
  IScheduledTask.cs                  ← public plugin contract for all BCs
  IDeferredJobScheduler.cs           ← public scheduling API for domain events
  IJobTrigger.cs                     ← public manual trigger API (admin UI)
  IJobManagementService.cs           ← public admin facade
  JobExecutionContext.cs             ← tool injected into each task execution
  Models/
    JobTriggerSource.cs              ← enum: Scheduled | Manual | Deferred
    JobOutcome.cs                    ← sealed class: Success / Failure / Progress
    JobStatusSummary.cs              ← merged view model for admin list
    JobExecutionRecord.cs            ← in-memory execution record
  Services/
    JobManagementService.cs          ← internal sealed
    Extensions.cs                    ← AddTimeManagementServices()

ECommerceApp.Infrastructure/Supporting/TimeManagement/
  TimeManagementDbContext.cs         ← schema: "time_management"
  TimeManagementConstants.cs        ← SchemaName = "time_management"
  JobTriggerChannel.cs              ← Singleton Channel<JobTriggerRequest> wrapper
  CronSchedulerService.cs           ← BackgroundService (tick every 30s → channel)
  DeferredJobPollerService.cs       ← BackgroundService (poll DB every 10s → channel)
  JobDispatcherService.cs           ← BackgroundService (channel reader → execute → record)
  InMemoryJobStatusMonitor.cs       ← Singleton circular buffer
  ScheduleConfig.cs                 ← internal: IScheduleConfig implementation
  Repositories/
    ScheduledJobRepository.cs       ← internal sealed
    DeferredJobInstanceRepository.cs ← internal sealed
    JobExecutionRepository.cs       ← internal sealed
  Configurations/
    ScheduledJobConfiguration.cs
    DeferredJobInstanceConfiguration.cs
    JobExecutionConfiguration.cs
  Extensions.cs                     ← AddTimeManagementInfrastructure()

ECommerceApp.Web/Controllers/
  JobManagementController.cs        ← [Authorize(Roles = "Administrator")]

ECommerceApp.Web/Views/JobManagement/
  Index.cshtml                      ← job list with last result + manual trigger button
  History.cshtml                    ← paginated execution log per job
```

---

### 10. Dependency: Cronos NuGet

`CronExpression.Parse` is provided by the `Cronos` NuGet package
(`Cronos`, MIT license, zero transitive dependencies).
Added to `ECommerceApp.Infrastructure.csproj`.

`CronSchedule` value object calls `Cronos.CronExpression.Parse(value)` in its constructor
to validate the expression at construction time, consistent with ADR-0006 VO invariant rules.

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

## Migration plan

1. Create folder structure across all three layers (Domain, Application, Infrastructure).
2. Implement Domain layer: value objects, aggregates, domain events, repository interfaces.
3. Implement Infrastructure layer: `TimeManagementDbContext`, EF configs, repositories,
   `CronSchedulerService`, `DeferredJobPollerService`, `JobDispatcherService`,
   `InMemoryJobStatusMonitor`, `Extensions.cs`.
4. Implement Application layer: `IScheduledTask`, `IDeferredJobScheduler`, `IJobTrigger`,
   `JobExecutionContext`, `IJobManagementService` / `JobManagementService`, `Extensions.cs`.
5. Implement `CurrencyRateSyncTask` in `Application/Supporting/Currencies/` as first real task.
6. Register via `AddTimeManagement(...)` in `Infrastructure/DependencyInjection.cs` and
   `AddTimeManagementServices()` in `Application/DependencyInjection.cs`.
7. DB migration for `time_management` schema — requires approval per `migration-policy.md`.
8. Implement `JobManagementController` + Views in `ECommerceApp.Web`.
9. Write unit tests for aggregates, value objects, and `JobManagementService`.
10. Write integration tests for `CurrencyRateSyncTask` via `BaseTest<T>`.

No existing code is removed or modified until Step 5 (first task) at the earliest.
Existing `CurrencyRateService` lazy-fetch behaviour remains unchanged until the atomic switch.

---

---

## Amendment — Design Revisions (2026-02-26)

The following five decisions supersede or extend the original sections above.
All five were reached during post-implementation design review.

---

### A1 — DB-first recurring job configuration

**Supersedes:** Sections 2 and 5.

**Decision:** Job definitions for recurring jobs are seeded into the `ScheduledJobs` table via a
dedicated migration. The `AddTimeManagement(jobs => {...})` code-config builder,
`IScheduleConfig`, `ScheduleConfig`, and `TimeManagementBuilder` are **removed**.

- `ScheduledJobs.Schedule` column (nvarchar 100) stores the cron expression, validated by the
  existing `CronSchedule` value object.
- DB is the authoritative source for `Schedule`, `MaxRetries`, `IsEnabled`, and `TimeZoneId`.
- Admins may edit `Schedule` and `MaxRetries` via the admin UI — changes take effect on the next tick.
- `CronSchedulerService` reads `ScheduledJob WHERE JobType = Recurring AND IsEnabled = 1` from DB
  on each tick (replacing in-memory `IScheduleConfig` iteration).
- `ConfigHash` column and lazy-create logic are **removed** (no longer needed).

**Accepted trade-off:** `CronSchedulerService` now requires DB access on each tick. A DB outage
will pause recurring job scheduling until connectivity is restored. For the current single-instance
deployment this is acceptable — the scheduler recovers automatically when the DB returns.

---

### A2 — Table split: `ScheduledJobs` (recurring only) + `DeferredJobQueue` (no FK)

**Supersedes:** Section 7 DB schema.

**Decision:** `ScheduledJobs` stores recurring job definitions only. `DeferredJobInstances` is
renamed to `DeferredJobQueue` and decoupled from `ScheduledJobs`:

```
ScheduledJobs          ← recurring definitions only
  Id, Name (unique), Schedule (nvarchar 100, cron), TimeZoneId (nvarchar 100, nullable),
  IsEnabled (bit, default 1), MaxRetries (int, default 3),
  LastRunAt (datetime2, nullable), NextRunAt (datetime2, nullable)

DeferredJobQueue       ← ephemeral active-timer queue, no FK
  Id (int, PK), JobName (nvarchar 100),
  EntityId (nvarchar 200), RunAt (datetime2),
  Status (tinyint: 0=Pending 1=Running 2=Failed 3=DeadLetter),
  RetryCount (int, default 0), LockExpiresAt (datetime2, nullable),
  ErrorMessage (nvarchar max, nullable), CreatedAt (datetime2)

JobExecutions          ← permanent audit log, INSERT only
  Id (int, PK), JobName (nvarchar 100),
  DeferredQueueId (int, nullable),
  Source (tinyint: 0=Scheduled 1=Manual 2=Deferred), ExecutionId (nvarchar 36),
  StartedAt (datetime2), CompletedAt (datetime2, nullable),
  Succeeded (bit), Message (nvarchar max, nullable)
```

`DeferredJobQueue.JobName` is a plain string — no join to `ScheduledJobs`, zero lookup overhead
on every `ScheduleAsync` call.

**Row lifecycle for `DeferredJobQueue`:**

| Outcome | Action |
|---|---|
| Success | DELETE row |
| Cancel (`CancelAsync`) | DELETE row |
| Failure, `RetryCount < MaxRetries` | UPDATE `Status=Pending`, `RunAt=backoff`, `RetryCount++` |
| Failure, `RetryCount >= MaxRetries` | UPDATE `Status=DeadLetter` (admin must intervene) |

---

### A3 — `DeferredJobStatus` simplification

**Supersedes:** `DeferredJobStatus` enum in Domain.

**Decision:** Remove `Completed` and `Cancelled` — those rows are deleted, not transitioned.

```csharp
public enum DeferredJobStatus : byte
{
    Pending    = 0,
    Running    = 1,
    Failed     = 2,
    DeadLetter = 3
}
```

---

### A4 — Proportional retry backoff + zombie detection (`LockExpiresAt`)

**Extends:** Section 4b and 4c.

**Retry timing:** Fixed intervals are inappropriate when jobs have widely different deadlines.
Retry delay is proportional to the original job delay:

```
OriginalDelay = RunAt - CreatedAt
backoff       = min(OriginalDelay × 0.1 × RetryCount, MaxBackoffCap = 60 min)
RetryRunAt    = FailedAt + backoff
```

| Job | OriginalDelay | Retry 1 | Retry 2 (dead letter) |
|---|---|---|---|
| PaymentTimeout | 15 min | +1.5 min | +3 min |
| OrderExpiration | 48 h | +4.8 h | +9.6 h (capped at 60 min) |

`ComputeRetryRunAt(DateTime failedAt, TimeSpan maxCap)` is a domain method on `DeferredJobInstance`.

**Zombie detection:** App crashes leave `Status = Running` indefinitely.
`LockExpiresAt` is set to `now + MaxExecutionWindow (5 min)` when the dispatcher begins execution.
Poller query becomes:

```sql
WHERE (Status = 0 AND RunAt <= NOW())
   OR (Status = 1 AND LockExpiresAt < NOW())  -- zombie recovery
ORDER BY RunAt ASC
```

A zombie row is reset to `Pending`, `RetryCount++`, `RunAt = ComputeRetryRunAt(...)`.

---

### A5 — `CronSchedulerService` tick alignment

**Supersedes:** Section 4a tick description.

**Decision:** `CronSchedulerService` aligns its first tick to the next 30-second clock boundary
before entering the loop. Each subsequent delay is recalculated to absorb tick execution time
and maintain boundary alignment.

```
App start: 13:14:07
Alignment delay: (30 - 7) × 1000 - ms = ~23 000 ms
First tick: 13:14:30  ← aligned
Ticks: 13:15:00, 13:15:30, 13:16:00, ...  ← exact boundaries
```

Without alignment, the startup second-offset carries forward to every job fire (e.g., every job
fires 7 s late if the app started 7 s into a 30-second window). Alignment eliminates this drift.

Pseudo-code:
```
ms = (30 - now.Second % 30) * 1000 - now.Millisecond
await Task.Delay(ms, ct)                     // align once
while not cancelled:
    await TickAsync(ct)
    ms = (30 - now.Second % 30) * 1000 - now.Millisecond
    await Task.Delay(ms, ct)                 // re-align each iteration
```

---

## Conformance checklist

- [ ] All `ScheduledJob`, `DeferredJobInstance`, `JobExecution` properties use `private set`
- [ ] `ScheduledJob.cs` has a `private ScheduledJob()` parameterless constructor for EF Core
- [ ] `DeferredJobInstance.Schedule(...)` is a `static` factory method
- [ ] `CronSchedule` value object calls `Cronos.CronExpression.Parse(value)` in its constructor
- [ ] `JobName` value object enforces non-empty and max length 100
- [ ] Domain files live under `Domain/Supporting/TimeManagement/`
- [ ] `IScheduledTask` and `IDeferredJobScheduler` are in `Application/Supporting/TimeManagement/`
- [ ] No `IScheduledTask` implementation in `Application/Supporting/TimeManagement/` itself
  (only the contract lives here — task implementations belong to their own BCs)
- [ ] `TimeManagementDbContext` uses schema `"time_management"`
- [ ] `JobManagementService`, all repositories are `internal sealed`
- [ ] `JobManagementController` is decorated with `[Authorize(Roles = "Administrator")]`
- [ ] `CronSchedulerService`, `DeferredJobPollerService`, `JobDispatcherService` registered
  via `TryAddHostedService` to prevent duplicate registration
- [ ] (A1) `IScheduleConfig`, `ScheduleConfig`, `TimeManagementBuilder` do not exist in the codebase
- [ ] (A1) `CronSchedulerService` reads from `ScheduledJob WHERE JobType=Recurring` — no `IScheduleConfig` in constructor
- [ ] (A1) `ScheduledJobs` table has `Schedule` column (cron), seeded via migration
- [ ] (A2) `DeferredJobQueue` has no FK column to `ScheduledJobs`; has `JobName nvarchar(100)` column
- [ ] (A2) `DeferredJobScheduler.ScheduleAsync` performs a single INSERT — no prior SELECT
- [ ] (A2) `JobDispatcherService` DELETEs `DeferredJobQueue` row on success
- [ ] (A3) `DeferredJobStatus` enum has exactly: `Pending | Running | Failed | DeadLetter`
- [ ] (A4) `DeferredJobInstance` has `ComputeRetryRunAt(DateTime failedAt, TimeSpan maxCap)` method
- [ ] (A4) `DeferredJobQueue` has `LockExpiresAt (datetime2, nullable)` column
- [ ] (A4) `DeferredJobPollerService` includes zombie recovery in its query
- [ ] (A5) `CronSchedulerService.ExecuteAsync` computes initial alignment delay before first tick

---

## Implementation Status

| Layer | Status |
|---|---|
| Domain (aggregates, value objects, domain events, repository interfaces) | ✅ Done — needs A3 (`DeferredJobStatus` enum) + A4 (`ComputeRetryRunAt`, `LockExpiresAt`) |
| Infrastructure (`TimeManagementDbContext`, EF configs, repositories, BackgroundServices, DI) | ✅ Done — needs A1 (remove builder, DB-read in scheduler) + A2 (queue restructure) + A4 (zombie query, lock) + A5 (tick alignment) |
| Application (`IScheduledTask`, `IDeferredJobScheduler`, `IJobTrigger`, `JobExecutionContext`, `IJobManagementService`, DI) | ✅ Done — needs A1 (remove `IScheduleConfig`) |
| First task: `CurrencyRateSyncTask` in Currencies BC | ✅ Done |
| Unit tests (35 tests passing) | ✅ Done — needs coverage for A3/A4 changes |
| DB migration (`time_management` schema, original) | ⬜ Pending approval |
| DB migration (A1/A2 schema changes: `Schedule` col, `DeferredJobQueue`, `LockExpiresAt`) | ⬜ Pending approval — must be coordinated with original migration |
| Integration tests | ⬜ Not started |
| `JobManagementController` + Views | ✅ Done |
| Atomic switch for `CurrencyRateSyncTask` (replace lazy NBP fetch) | ⬜ After integration tests |

---

## References

- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0004 — Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](./0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0008 — Supporting/Currencies BC Design](./0008-supporting-currencies-bc-design.md)
- [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- [`docs/patterns/implementation-patterns.md`](../patterns/implementation-patterns.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 16, § 17
- Issues / PRs: <!-- link -->
- Repository: https://github.com/kwojtasinski-repo/ECommerceApp

## Reviewers

- @team/architecture
