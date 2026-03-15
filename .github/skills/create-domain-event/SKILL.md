---
name: create-domain-event
description: >
  Scaffold a cross-BC domain event (IMessage record), its handler (IMessageHandler<T>),
  or both. Supports 3 modes: event-only, handler-only, event+handler.
  Domain-level events (no cross-BC) are simple records without IMessage.
argument-hint: "<EventName> [event-only|handler-only|both] [BcName]"
---

# Create Domain Event

Generate cross-BC message records and/or their handlers.

## Modes

| Mode | Creates |
|---|---|
| `event-only` | IMessage record in publishing BC's Messages folder |
| `handler-only` | IMessageHandler in consuming BC's Handlers folder + DI registration |
| `both` | Both files + DI registration |

## Important: two event categories

| Category | Base type | Location | Use case |
|---|---|---|---|
| Cross-BC message | `IMessage` | `Application/{{Module}}/{{BC}}/Messages/` | Consumed by other BCs via ModuleClient |
| Domain-level event | plain `record` | `Domain/{{Module}}/{{BC}}/Events/` | Internal to the BC, no cross-BC routing |

This skill focuses on **cross-BC messages** (the IMessage path). For domain-level events, create a simple record manually.

---

## Event template (IMessage)

**File**: `ECommerceApp.Application/{{PublisherModule}}/{{PublisherBC}}/Messages/{{EventName}}.cs`

```csharp
using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.{{PublisherModule}}.{{PublisherBC}}.Messages
{
    public record {{EventName}}({{Properties}}) : IMessage;
}
```

### Nested record pattern (when payload is complex)

```csharp
public record {{EventName}}(
    int {{AggregateId}},
    {{NestedRecordName}} {{NestedProperty}}) : IMessage
{
    public record {{NestedRecordName}}({{NestedProperties}});
}
```

---

## Handler template (IMessageHandler)

**File**: `ECommerceApp.Application/{{ConsumerModule}}/{{ConsumerBC}}/Handlers/{{EventName}}Handler.cs`

```csharp
using ECommerceApp.Application.{{PublisherModule}}.{{PublisherBC}}.Messages;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.{{ConsumerModule}}.{{ConsumerBC}}.Interfaces;

namespace ECommerceApp.Application.{{ConsumerModule}}.{{ConsumerBC}}.Handlers
{
    internal sealed class {{EventName}}Handler : IMessageHandler<{{EventName}}>
    {
        private readonly I{{Repository}} _repository;

        public {{EventName}}Handler(I{{Repository}} repository)
        {
            _repository = repository;
        }

        public async Task HandleAsync({{EventName}} message)
        {
            // TODO: implement handling logic
        }
    }
}
```

---

## DI registration (required for handlers)

Add to the consuming BC's Application extension:

```csharp
services.AddScoped<IMessageHandler<{{EventName}}>, {{EventName}}Handler>();
```

## Publishing side

Publish from Application service or handler:

```csharp
await _moduleClient.PublishAsync(new {{EventName}}(id, data));
```

## Rules

1. Event records are always `public` (consumed across assembly boundaries)
2. Handlers are always `internal sealed`
3. One handler per message type — `ModuleClient` uses `GetService()` (singular), not `GetServices()`
4. Handler DI: registered as `Scoped` via `IMessageHandler<T>` interface
5. If an event has no handler yet, that's fine — `ModuleClient` logs a warning but does not crash
6. Property naming: use the aggregate's TypedId type (e.g., `OrderId`) in the event, but pass `.Value` (int) to keep the message contract simple
7. Namespace must match folder structure exactly
