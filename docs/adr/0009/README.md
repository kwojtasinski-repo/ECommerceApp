# ADR-0009: Supporting/TimeManagement BC

**Status**: Accepted — Amended 2026-02-26
**BC**: Supporting/TimeManagement
**Last amended**: 2026-02-26

## What this decision covers
Design of the job scheduling infrastructure: `IScheduledTask` (recurring), `IDeferredJobScheduler`
(domain-triggered), deferred queue with exponential backoff, and zombie detection.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0009-supporting-timemanagement-bc-design.md | Core design: job taxonomy, IScheduledTask, IDeferredJobScheduler, DB schema | Understanding the job engine |
| amendments/a1-design-revisions.md | A1–A5: DB-first config, table split, status simplification, retry backoff, tick alignment | Working with job scheduling internals |
| checklist.md | Implementation conformance rules | Code review of job implementations |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/ischeduledtask-implementation.md | How to implement a new scheduled task | Adding a new recurring job |
| example-implementation/ideferred-job-scheduler-usage.md | How to schedule a deferred job from a domain event | Wiring domain events to jobs |
| example-implementation/job-registration-di.md | DI registration pattern for jobs | Setting up AddXxxServices() |

## Key rules
- All job definitions live in TimeManagement BC — never define job metadata in the calling BC
- Amendments A1–A5 override the original design in §4 and §6 — read amendments first
- `IDeferredJobScheduler` is for one-time domain-triggered jobs; `IScheduledTask` is for recurring

## Related ADRs
- ADR-0002 (Parallel Change strategy) — job migration follows parallel change
- ADR-0011 (Inventory) — uses `StockAdjustmentJob` via `IDeferredJobScheduler`
- ADR-0015 (Payments) — uses `PaymentWindowExpiredJob`
