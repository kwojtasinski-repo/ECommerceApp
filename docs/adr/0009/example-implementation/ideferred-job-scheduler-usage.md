# Scheduling a deferred job

Use `IDeferredJobScheduler` when a workflow must cause one task to run at a concrete future time.
The owning application service supplies a stable job name, entity ID, and UTC `runAt` value:

```csharp
await _deferredJobScheduler.ScheduleAsync(
	jobName: "PaymentTimeout",
	entityId: order.Id.ToString(),
	runAt: expiresAt,
	ct: cancellationToken);
```

Call `CancelAsync(jobName, entityId, cancellationToken)` when the workflow makes the deferred
action unnecessary. The scheduler owns queue persistence and dispatch; the calling BC owns only
the business decision to schedule or cancel.

## Rules

- Use UTC timestamps and a stable entity identifier.
- Do not query or write the TimeManagement database from the calling BC.
- Keep the job name aligned with the registered task implementation.
- Make the task idempotent because delivery and retry can repeat execution.

## Outside this document

Queue schema, retry calculation, deletion, and zombie recovery follow A1-A5 in
[job-engine.md](job-engine.md).
