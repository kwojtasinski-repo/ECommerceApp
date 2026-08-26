# Job registration in DI

TimeManagement owns the job definition. The bounded context that implements a task registers
only its `IScheduledTask` implementation in its application DI extension.

```csharp
services.AddScoped<IScheduledTask, CurrencyRateSyncTask>();
```

The task's `TaskName` is matched by the scheduler to the persisted job definition. Keep cron
expressions, timezones, retry limits, and hosted-service registrations inside TimeManagement.

## Rules

- Register each task once in its owning BC.
- Do not introduce `IScheduleConfig` or `TimeManagementBuilder` from superseded A1 design text.
- Use `TryAddHostedService` for TimeManagement hosted services.
- Keep manual trigger and admin operations behind TimeManagement application contracts.

## Outside this document

Dispatch and persistence details are maintained in [job-engine.md](job-engine.md); amendment
precedence is defined by ADR-0009.
