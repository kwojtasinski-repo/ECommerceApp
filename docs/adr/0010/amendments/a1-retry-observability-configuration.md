## Amendment — Retry, Observability and Configuration Revisions (2026-02-26)

The following amendments extend the original design with a retry mechanism, dead-letter
observability, and a configurable backoff strategy. Sections §5 and §6 have been updated
in-place to reflect these additions. No existing contract (`IMessage`, `IMessageBroker`,
`IMessageHandler<T>`) changes — only infrastructure internals are extended.

---

### A1 — MessageEnvelope and channel type change

The channel type changes from `Channel<IMessage>` to `Channel<MessageEnvelope>`. `IMessage`
remains a clean marker interface with no retry fields on the public contract.

`MessageEnvelope` wraps every message in transit:

```csharp
public sealed record MessageEnvelope(
    Guid MessageId,
    IMessage Message,
    int RetryCount,
    int MaxRetries,
    string? LastError,
    DateTime EnqueuedAt,
    DateTime? RetryAfter,
    DateTime? FailedAt);
```

`MessageId` is stable across all retry attempts — the same `Guid` identifies the original
dispatch and all subsequent retries.

Affected components: `IMessageChannel`, `MessageChannel`, `IAsyncMessageDispatcher`,
`AsyncMessageDispatcher`, `BackgroundMessageDispatcher`.

---

### A2 — Retry mechanism with non-blocking re-enqueue

On handler failure, `BackgroundMessageDispatcher` does not re-enqueue synchronously. It uses
a fire-and-forget `Task.Run` pattern so the channel reader loop is never blocked during the
backoff delay:

```csharp
// On handler exception:
if (envelope.RetryCount < envelope.MaxRetries)
{
    var delay = ComputeBackoff(envelope.RetryCount, options);
    var retryEnvelope = envelope with
    {
        RetryCount = envelope.RetryCount + 1,
        RetryAfter = DateTime.UtcNow + delay,
        LastError = ex.Message,
        FailedAt = DateTime.UtcNow
    };
    _monitor.SetRetrying(retryEnvelope);
    _ = Task.Run(async () =>
    {
        await Task.Delay(delay, ct);
        await _channel.Writer.WriteAsync(retryEnvelope, ct);
    }, ct);
}
else
{
    _monitor.SetDeadLettered(retryEnvelope);
    _logger.LogCritical("Message dead-lettered: {MessageType} {MessageId}", ...);
}
```

Messages that exhaust all retries are not silently dropped — they enter `DeadLettered` state
in `InMemoryMessageRetryMonitor` and are logged at `Critical`.

---

### A3 — InMemoryMessageRetryMonitor

A new `internal sealed` Singleton tracks every in-flight message. Same structural pattern as
`InMemoryJobStatusMonitor`.

State machine:

```
Enqueued → Processing → (success)                   → removed from monitor
                     → (failure, retries remain)    → Retrying → Processing (next attempt)
                     → (failure, retries exhausted) → DeadLettered (retained until cleared)
```

Monitor entry fields:
- `MessageId` — stable `Guid` (dictionary key)
- `MessageType` — for diagnostics
- `Status` — `MessageRetryStatus` enum: `Processing | Retrying | DeadLettered`
- `RetryCount`
- `LastError`
- `NextRetryAt` — from `RetryAfter` on the envelope
- `EnqueuedAt`
- `ProcessingStartedAt` — set when handler invocation begins; cleared on success

Exposed via `IMessageRetryMonitor` (public interface in `Application/Messaging/`).

---

### A4 — Extended MessagingOptions configuration

`MessagingOptions` is extended with retry and timeout settings (see updated §5).

Backoff formula: `min(2^retryCount × RetryBaseDelaySeconds, RetryMaxDelaySeconds)` with
±15% uniform jitter, computed relative to `FailedAt` (not `EnqueuedAt`) — each retry window
starts from the previous failure.

`HandlerOverrides` is keyed by message type name. Any field absent in an override entry falls
back to the global default. Per-type configuration in a database is the correct evolution path
when the number of distinct message types grows beyond 3–4.

`MaxHandlerExecutionSeconds` is the intended bound for `CancellationTokenSource.CancelAfter(...)`
per handler invocation (see Risks & mitigations — hang detection edge case).

---
