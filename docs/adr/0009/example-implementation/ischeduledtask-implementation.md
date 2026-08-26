# Implementing IScheduledTask

Implement `IScheduledTask` in the owning BC. `TaskName` must match the job definition owned by
TimeManagement. `ExecuteAsync` receives `JobExecutionContext` and must report success or failure
before returning. Unexpected exceptions are handled by the dispatcher as failed executions.

## Rules

- Do not define cron, retry, timezone, or database scheduling metadata in the owning BC.
- Keep task registration in the owning BC's application DI extension.
- Keep the task small and delegate domain behavior to the owning service or aggregate.
- Test the task by asserting the reported `JobExecutionContext` outcome.

## Outside this document

Scheduler, channel, persistence, retry, and zombie-recovery rules are maintained in
[job-engine.md](job-engine.md) and governed by ADR-0009 amendments A1-A5.
