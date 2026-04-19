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
