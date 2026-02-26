# ADR-0010: In-Memory Message Broker for Cross-BC Communication

## Status
Accepted

## Date
2026-02-26

## Context

As the codebase adds more Bounded Contexts (TimeManagement, Currencies, Orders, Payments),
services in one BC occasionally need to trigger work in another BC. The naive approach is
direct interface injection:

```csharp
// OrderService directly depends on TimeManagement:
await _deferredJobScheduler.ScheduleAsync("PaymentTimeout", paymentId, runAt);
```

This creates **compile-time coupling between BCs** — `Orders.Application` must reference
`TimeManagement.Application`. As the number of BCs grows, these cross-references form a web
that makes BCs hard to reason about, test in isolation, or extract later.

ECommerceApp is a **modular monolith** today. A real message bus (RabbitMQ, Azure Service Bus)
is not justified at current scale, but the architectural seam for one should be established
early — before coupling solidifies across many BCs.

The following forces apply:

- Cross-BC coupling must be eliminated **from the first integration** (checkout → TimeManagement),
  not deferred to a future refactor.
- A real message bus is premature: no infrastructure, no operational overhead justified yet.
- The in-process `Channel<T>` pattern (already used by `JobTriggerChannel`) proves that
  async in-memory dispatch works well and is testable.
- When a real message bus does arrive, only the infrastructure implementation must change —
  no BC publisher or handler code should need to be rewritten.

---

## Decision

We introduce a **thin in-memory message broker** as shared infrastructure. It provides a
single, stable abstraction for all cross-BC publish/subscribe communication within the monolith.

```
Publisher BC                Shared Infrastructure          Subscriber BC
──────────────             ──────────────────────         ──────────────
IMessageBroker              Channel<IMessage>              IMessageHandler<T>
  .PublishAsync(msg)  →     BackgroundMessageDispatcher →  .HandleAsync(msg, ct)
```

All cross-BC calls go through `IMessageBroker`. No BC may directly inject a service from
another BC to trigger cross-BC side effects.

---

### 1. Shared contracts (`Application/Messaging/`)

```csharp
// Marker interface — all cross-BC messages implement this
public interface IMessage { }

// Publisher API — injected into any BC service that needs to trigger cross-BC work
public interface IMessageBroker
{
    Task PublishAsync(params IMessage[] messages);
}

// Handler contract — each BC registers handlers for messages it cares about
public interface IMessageHandler<TMessage> where TMessage : class, IMessage
{
    Task HandleAsync(TMessage message, CancellationToken ct = default);
}
```

---

### 2. Infrastructure implementation (`Infrastructure/Messaging/`)

#### 2a. `MessageChannel` (Singleton)

Wraps `Channel<IMessage>` — the in-process async backbone:

```csharp
internal sealed class MessageChannel : IMessageChannel
{
    private readonly Channel<IMessage> _messages = Channel.CreateUnbounded<IMessage>();
    public ChannelReader<IMessage> Reader => _messages.Reader;
    public ChannelWriter<IMessage> Writer => _messages.Writer;
}
```

#### 2b. `AsyncMessageDispatcher` (Singleton)

Writes to the channel asynchronously (fire-and-forget from publisher's perspective):

```csharp
internal sealed class AsyncMessageDispatcher : IAsyncMessageDispatcher
{
    private readonly IMessageChannel _channel;
    public Task PublishAsync<T>(T message) where T : class, IMessage
        => _channel.Writer.WriteAsync(message).AsTask();
}
```

#### 2c. `InMemoryMessageBroker` (Scoped)

Entry point for publishers. Supports two dispatch modes via `MessagingOptions`:

```csharp
internal sealed class InMemoryMessageBroker : IMessageBroker
{
    // UseBackgroundDispatcher = true  → AsyncMessageDispatcher → Channel (async)
    // UseBackgroundDispatcher = false → IModuleClient.PublishAsync (sync, same request)
    public async Task PublishAsync(params IMessage[] messages) { ... }
}
```

Default for ECommerceApp: `UseBackgroundDispatcher = true` — all cross-BC messages are
dispatched asynchronously via the channel.

#### 2d. `BackgroundMessageDispatcher` (BackgroundService)

Single reader of `Channel<IMessage>`. Resolves the correct `IMessageHandler<T>` by message
type and calls `HandleAsync`:

```csharp
while await _channel.Reader.WaitToReadAsync(ct):
    var message = await _channel.Reader.ReadAsync(ct)
    var handlerType = typeof(IMessageHandler<>).MakeGenericType(message.GetType())
    using var scope = _serviceScopeFactory.CreateScope()
    var handler = scope.ServiceProvider.GetRequiredService(handlerType)
    await ((dynamic)handler).HandleAsync((dynamic)message, ct)
```

Exceptions from handlers are caught, logged, and do not propagate to the host.

---

### 3. Message ownership rule

Messages are defined in the **publishing BC**:

```
Application/Orders/Messages/
  CheckoutCompleted.cs        ← owned by Orders BC
    public record CheckoutCompleted(string PaymentId, DateTime TimeoutAt) : IMessage;
```

Handlers are defined in the **subscribing BC**:

```
Application/Supporting/TimeManagement/Handlers/
  CheckoutCompletedHandler.cs  ← owned by TimeManagement BC
    : IMessageHandler<CheckoutCompleted>
    → calls IDeferredJobScheduler.ScheduleAsync(...)
```

Dependency direction: `TimeManagement` → `Orders.Messages`. The reverse (`Orders` → `TimeManagement`)
is **forbidden**.

---

### 4. Concrete checkout example — zero BC coupling

```csharp
// OrderService (Orders BC) — no reference to TimeManagement at all:
await _messageBroker.PublishAsync(
    new CheckoutCompleted(payment.Id.ToString(), DateTime.UtcNow.AddMinutes(15)));

// CheckoutCompletedHandler (TimeManagement BC):
public async Task HandleAsync(CheckoutCompleted msg, CancellationToken ct)
    => await _deferredJobScheduler.ScheduleAsync(
           "PaymentTimeout", msg.PaymentId, msg.TimeoutAt, ct);
```

---

### 5. `MessagingOptions` configuration

```json
// appsettings.json
"Messaging": {
  "UseBackgroundDispatcher": true,
  "RetryCount": 5,
  "RetryBaseDelaySeconds": 5,
  "RetryMaxDelaySeconds": 60,
  "MaxHandlerExecutionSeconds": 30,
  "HandlerOverrides": {
    "CheckoutCompleted": {
      "RetryBaseDelaySeconds": 10
    }
  }
}
```

`UseBackgroundDispatcher = false` is available for test scenarios where synchronous
in-request dispatch is easier to assert against.

`HandlerOverrides` is keyed by message type name; any field absent in an override entry
falls back to the global default. See Amendment A4 for full details.

---

### 6. Folder structure

```
ECommerceApp.Application/Messaging/
  IMessage.cs
  IMessageBroker.cs
  IMessageHandler.cs
  IAsyncMessageDispatcher.cs
  IMessageChannel.cs
  IMessageRetryMonitor.cs          ← new (public observability interface)
  MessagingOptions.cs              ← extended (retry + HandlerOverrides fields)
  MessageEnvelope.cs               ← new
  MessageRetryStatus.cs            ← new (Processing | Retrying | DeadLettered)
  HandlerRetryOptions.cs           ← new (per-type retry override)
  Extensions.cs                    ← AddMessagingServices()

ECommerceApp.Infrastructure/Messaging/
  MessageChannel.cs                ← updated (Channel<MessageEnvelope>)
  AsyncMessageDispatcher.cs        ← updated (envelope-aware)
  InMemoryMessageBroker.cs
  InMemoryMessageRetryMonitor.cs   ← new (internal sealed Singleton)
  BackgroundMessageDispatcher.cs   ← updated (retry logic, monitor, hang timeout)
  Extensions.cs                    ← updated (register InMemoryMessageRetryMonitor)
```

---

### 7. Migration path to real message bus

`InMemoryMessageBroker` is the only class replaced when a real bus arrives:

```
InMemoryMessageBroker → RabbitMqMessageBroker  (swap the IMessageBroker registration)
```

All publisher call sites (`_messageBroker.PublishAsync(...)`) and all handler implementations
(`IMessageHandler<T>`) remain unchanged. The seam is stable.

---

## Consequences

### Positive

- Zero compile-time coupling between BCs that communicate via messages.
- `IMessageBroker` / `IMessageHandler<T>` is the stable seam for the future message bus —
  no BC code changes when the bus arrives.
- Handlers are independently testable: inject a mock `IMessageBroker` in publisher tests;
  call `HandleAsync` directly in handler tests.
- `BackgroundMessageDispatcher` is identical in structure to `JobDispatcherService` — no new
  patterns introduced.
- `UseBackgroundDispatcher = false` allows synchronous test dispatch without a real channel.

### Negative

- One additional `BackgroundService` running concurrently.
- Up to `RetryCount` retry attempts (default 5) with exponential backoff
  (`min(2^n × RetryBaseDelaySeconds, RetryMaxDelaySeconds)` ± 15% jitter). Messages
  exhausting all retries enter `DeadLettered` state in `InMemoryMessageRetryMonitor` —
  visible and logged at `Critical`; not silently dropped.
- Cross-BC messages are not durable — app restart drops any unprocessed messages in the channel.
  For non-financial background notifications this is acceptable. For critical flows (e.g.,
  checkout → PaymentTimeout scheduling), the `DeferredJobQueue` row is the durable record;
  the message only triggers the insert.

### Risks & mitigations

- **Risk**: Handler not registered → `GetRequiredService` throws → message dropped.
  **Mitigation**: `BackgroundMessageDispatcher` catches and logs; alert on repeated failures.
- **Risk**: Slow handler blocks channel reader for other messages.
  **Mitigation**: Each handler runs in its own `IServiceScope` inside `Task.Run` (future
  enhancement); for current synchronous dispatch, handler must be fast.
- **Risk**: Handler hangs (edge case) — `BackgroundMessageDispatcher` loop stalls indefinitely.
  **Mitigation**: Apply `CancellationTokenSource.CancelAfter(MaxHandlerExecutionSeconds)` per
  handler invocation to bound execution time. `ProcessingStartedAt` stored in
  `InMemoryMessageRetryMonitor` provides observability (`Status=Processing AND
  now − ProcessingStartedAt > MaxHandlerExecutionSeconds`). If hanging handlers become common,
  move dispatch to parallel `Task.Run` per message. Considered an edge case in the current
  system — document and monitor rather than over-engineer.
- **Risk**: Message type defined in wrong BC creates reverse dependency.
  **Mitigation**: Enforce via code review — messages live in publisher's `Messages/` folder only.

---

## Alternatives considered

- **Direct service injection** — rejected; creates BC-to-BC compile-time coupling that grows
  with every new integration. Impossible to remove once established across many BCs.
- **MediatR** — rejected; adds a third-party dependency and blurs the BC boundary (MediatR
  handlers are in the same DI scope as the publisher). `IMessageHandler<T>` is a clearer,
  thinner contract.
- **Real message bus (RabbitMQ / Azure Service Bus)** — rejected for Phase 1; no infrastructure,
  operational overhead not justified at current scale. Migration path exists (§ 7).
- **Domain events (in-request)** — rejected for cross-BC triggers; domain events are scoped
  to a single aggregate's transaction. Cross-BC communication needs an explicit async seam.

---

## Migration plan

1. Create `Application/Messaging/` with 6 contract files + `Extensions.cs`.
2. Create `Infrastructure/Messaging/` with 4 implementation files + `BackgroundMessageDispatcher` + `Extensions.cs`.
3. Register via `AddMessagingServices()` in `Application/DependencyInjection.cs` and
   `AddMessagingInfrastructure()` in `Infrastructure/DependencyInjection.cs`.
4. Add `"Messaging": { "UseBackgroundDispatcher": true }` to `appsettings.json`.
5. Create first message: `CheckoutCompleted` in `Application/Orders/Messages/`.
6. Create first handler: `CheckoutCompletedHandler` in `Application/Supporting/TimeManagement/Handlers/`.
7. Replace `IDeferredJobScheduler` direct injection in `OrderService` with `IMessageBroker.PublishAsync`.
8. Write unit tests for `CheckoutCompletedHandler` and the broker dispatch path.

No existing code is removed until Step 7. Parallel change strategy applies.

---

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

## Conformance checklist

- [ ] `IMessage`, `IMessageBroker`, `IMessageHandler<T>` live in `Application/Messaging/`
- [ ] No BC service directly injects a service from a different BC to trigger cross-BC side effects
- [ ] All cross-BC messages implement `IMessage` and live in the **publishing** BC's `Messages/` folder
- [ ] All `IMessageHandler<T>` implementations live in the **subscribing** BC
- [ ] `InMemoryMessageBroker` is `internal sealed`
- [ ] `BackgroundMessageDispatcher` is registered via `TryAddHostedService`
- [ ] `MessagingOptions` is bound from configuration (not hardcoded)
- [ ] Handler exceptions are caught and logged — never propagate to `BackgroundMessageDispatcher` loop
- [ ] `MessageEnvelope` carries `MessageId`, `RetryCount`, `RetryAfter`, `FailedAt` — `IMessage` stays clean
- [ ] `BackgroundMessageDispatcher` uses non-blocking `Task.Run(Delay + WriteAsync)` re-enqueue on retry
- [ ] `InMemoryMessageRetryMonitor` is `internal sealed` Singleton, keyed by `MessageId`
- [ ] `RetryCount`, `RetryBaseDelaySeconds`, `RetryMaxDelaySeconds` bound from `MessagingOptions` (not hardcoded)
- [ ] `HandlerOverrides` per-type override falls back to global defaults when a field is absent
- [ ] `DeadLettered` entries logged at `Critical` and retained in monitor until explicitly cleared

---

## References

- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0004 — Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0009 — Supporting/TimeManagement BC Design](./0009-supporting-timemanagement-bc-design.md) (first consumer)
- [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)

## Reviewers

- @team/architecture
