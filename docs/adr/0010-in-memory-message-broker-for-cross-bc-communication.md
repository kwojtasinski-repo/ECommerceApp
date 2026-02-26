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
  "UseBackgroundDispatcher": true
}
```

`UseBackgroundDispatcher = false` is available for test scenarios where synchronous
in-request dispatch is easier to assert against.

---

### 6. Folder structure

```
ECommerceApp.Application/Messaging/
  IMessage.cs
  IMessageBroker.cs
  IMessageHandler.cs
  IAsyncMessageDispatcher.cs
  IMessageChannel.cs
  MessagingOptions.cs
  Extensions.cs                    ← AddMessagingServices()

ECommerceApp.Infrastructure/Messaging/
  MessageChannel.cs
  AsyncMessageDispatcher.cs
  InMemoryMessageBroker.cs
  BackgroundMessageDispatcher.cs   ← BackgroundService
  Extensions.cs                    ← AddMessagingInfrastructure()
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
- If a handler throws and retries are not implemented, the message is silently dropped
  (logged but not retried). Acceptable for current scale; outbox pattern addresses this later.
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

## Conformance checklist

- [ ] `IMessage`, `IMessageBroker`, `IMessageHandler<T>` live in `Application/Messaging/`
- [ ] No BC service directly injects a service from a different BC to trigger cross-BC side effects
- [ ] All cross-BC messages implement `IMessage` and live in the **publishing** BC's `Messages/` folder
- [ ] All `IMessageHandler<T>` implementations live in the **subscribing** BC
- [ ] `InMemoryMessageBroker` is `internal sealed`
- [ ] `BackgroundMessageDispatcher` is registered via `TryAddHostedService`
- [ ] `MessagingOptions` is bound from configuration (not hardcoded)
- [ ] Handler exceptions are caught and logged — never propagate to `BackgroundMessageDispatcher` loop

---

## References

- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0004 — Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md)
- [ADR-0009 — Supporting/TimeManagement BC Design](./0009-supporting-timemanagement-bc-design.md) (first consumer)
- [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)

## Reviewers

- @team/architecture
