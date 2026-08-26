# TimeManagement implementation guide

This companion contains implementation detail for ADR-0009. It is not a second decision
record; changes to ownership, job categories, or startup/database coupling belong in the ADR.

## Contracts and ownership

TimeManagement owns job metadata and exposes `IScheduledTask`, `IDeferredJobScheduler`,
`IJobTrigger`, and `JobExecutionContext` from `Application/Supporting/TimeManagement/`.
Calling bounded contexts register task implementations only; they do not define cron, retry,
timezone, or database settings.

Tasks report success, failure, or progress through `JobExecutionContext`. Recurring execution
has no entity ID; deferred execution carries the scheduled entity reference.

## Dispatch pipeline

`CronSchedulerService` and `DeferredJobPollerService` create trigger requests. A singleton
`JobTriggerChannel` backed by an unbounded single-reader `Channel<JobTriggerRequest>` decouples
those producers from `JobDispatcherService`.

The dispatcher resolves the task in a fresh service scope, executes it, persists the outcome,
updates the scheduled/deferred record, and catches unreported task exceptions as failures.
Recurring scheduling and deferred polling remain independent of task execution latency.

Amendments A1-A5 supersede the original scheduling internals. In particular, the current
implementation must follow the amendment rules for DB-first recurring configuration, the
split deferred queue, status values, retry calculation, zombie recovery, and tick alignment.

## Persistence and admin surface

The `time_management` schema stores scheduled jobs, deferred queue entries, and execution
history. Recurring configuration is persisted according to A1; deferred scheduling writes a
queue entry without coupling it to a scheduled-job foreign key according to A2. The dispatcher
removes successfully processed deferred entries, while retry and zombie behavior follows A3-A5.

The administrator-only `JobManagementController` exposes the job list, paginated history,
manual trigger, and enable/disable actions. The list combines configured jobs, persisted state,
and recent execution status.

## Rules

- Keep `IScheduledTask` implementations in their owning BC, not in TimeManagement itself.
- Register hosted services with `TryAddHostedService` so duplicate registrations are prevented.
- Validate cron expressions through `Cronos.CronExpression.Parse` in `CronSchedule`.
- Keep `JobName` non-empty and limited to 100 characters.
- Preserve the A1-A5 amendment semantics; do not copy superseded `IScheduleConfig` or cancelled
  status designs from the original ADR text.
- Keep job execution asynchronous through the channel; schedulers do not call tasks directly.
- Keep migration approval under the repository migration policy.

## Outside this document

- ADR-0009 owns the architectural decision and amendment precedence.
- Concrete task behavior belongs to its owning BC and its tests.
- EF migration history and production migration approval belong to Infrastructure governance.