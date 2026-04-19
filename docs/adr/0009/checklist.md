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
