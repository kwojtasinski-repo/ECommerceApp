---
name: create-message-contract
description: >
  Scaffold a cross-BC message contract — an IMessage record that defines a published event.
  Option A: event record only (publisher BC defines it; handler added separately via /create-domain-event).
  Includes naming conventions, property conventions, and BC map / ADR reminder.
argument-hint: "<EventName> <PublisherBcPath like Sales/Orders>"
---

# Create Message Contract

Scaffold a cross-BC event contract as an `IMessage` record (event-only).

> **Split responsibilities**:
>
> - This skill → defines the **event shape** (publisher side).
> - `/create-domain-event handler-only` → adds the **handler** (consumer side).
>   Keep them separate — the publisher should not know about consumers.

---

## Naming convention

| Pattern                          | Example                                                           |
| -------------------------------- | ----------------------------------------------------------------- |
| Past tense, what happened        | `OrderPlaced`, `PaymentConfirmed`, `StockReserved`                |
| Verb + subject (domain language) | `CouponApplied`, `CartAbandoned`                                  |
| Never: `XxxEvent` suffix         | ❌ `OrderPlacedEvent` — the `IMessage` base already implies event |

---

## File placement

**File**: `ECommerceApp.Application/{{PublisherModule}}/{{PublisherBC}}/Messages/{{EventName}}.cs`

Example: `ECommerceApp.Application/Sales/Orders/Messages/OrderPlaced.cs`

---

## Template — simple event record

```csharp
using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.{{PublisherModule}}.{{PublisherBC}}.Messages
{
    public record {{EventName}}(
        int {{AggregateId}},
        {{Property2Type}} {{Property2}}) : IMessage;
}
```

## Template — event with nested payload (complex data)

Use when the event carries a group of related properties that logically belong together:

```csharp
using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.{{PublisherModule}}.{{PublisherBC}}.Messages
{
    public record {{EventName}}(
        int {{AggregateId}},
        {{NestedName}} {{NestedProperty}}) : IMessage
    {
        public record {{NestedName}}(
            {{Prop1Type}} {{Prop1}},
            {{Prop2Type}} {{Prop2}});
    }
}
```

Existing example: see `OrderPlacementFailed` in `Application/Sales/Orders/Messages/`.

---

## Property rules

| Rule                                      | Reason                                                                         |
| ----------------------------------------- | ------------------------------------------------------------------------------ |
| Use `int` for IDs (not TypedId)           | Keeps the contract simple; consumers don't depend on domain value objects      |
| Use primitives and built-in types only    | Avoid referencing domain entities or Application-layer objects in the contract |
| Include only what consumers need to react | Not a full entity dump — model the event, not the aggregate state              |
| Timestamps: use `DateTimeOffset`          | `DateTime` loses timezone info across process boundaries                       |

---

## Publishing side (where to call `PublishAsync`)

Publish from within an Application service or CQRS handler after persisting state:

```csharp
await _moduleClient.PublishAsync(new {{EventName}}(
    entity.Id,
    entity.{{Property2}}));
```

> Publish **after** `SaveChangesAsync()` — never before. If persistence fails, the event must not fire.

---

## Checklist — after creating the contract

- [ ] Event record created in publisher BC's `Messages/` folder
- [ ] `IMessage` base applied (`using ECommerceApp.Application.Messaging;`)
- [ ] Property types are primitives — no TypedId, no domain entities
- [ ] `PublishAsync` call added in the service/handler after successful persistence
- [ ] **`docs/architecture/bounded-context-map.md`** updated — add the new cross-BC dependency arrow
- [ ] If this event crosses a BC boundary not yet documented — suggest a new ADR or ADR amendment
- [ ] Add handler in consumer BC using `/create-domain-event handler-only {{EventName}} {{ConsumerBcPath}}`

---

## Rules

1. Event records are always `public` — they cross BC boundaries.
2. The publisher BC owns the contract file. Consumer BCs import it via a `using` directive.
3. Never put business logic or methods on the event record.
4. Do NOT register the event in DI — only handlers are registered.
5. One record per event — do not reuse the same record for semantically different events.
6. After adding a new cross-BC event, update `bounded-context-map.md` (mandatory).
