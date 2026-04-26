---
name: create-cqrs-handler
description: >
  Scaffold a CQRS command handler for a new BC operation.
  Uses ICommandHandler<TCommand, TResult> — command returns a typed result.
  Creates: ICommandHandler interface (first use only), command record, result type, handler class, DI registration.
  Follows the existing sealed record + static factory result pattern.
argument-hint: "<CommandName> <BcPath like Sales/Orders> [result-type: enum|record]"
---

# Create CQRS Handler

Scaffold a command handler using `ICommandHandler<TCommand, TResult>` (Option B — command with result).

> **First-time setup**: If `ICommandHandler` does not yet exist in the codebase, create the interface first (Step 0 below). After the first use, skip Step 0.

---

## Step 0 — Create the interface (first time only)

Check: does `ECommerceApp.Application/Interfaces/ICommandHandler.cs` exist?

If NO, create it:

**File**: `ECommerceApp.Application/Interfaces/ICommandHandler.cs`

```csharp
namespace ECommerceApp.Application.Interfaces
{
    public interface ICommandHandler<TCommand, TResult>
        where TCommand : class
    {
        Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
    }
}
```

> Note: `Application/Interfaces/` already contains domain repository interfaces. This interface lives alongside them.

---

## File layout (per command)

| File            | Path                                                                              |
| --------------- | --------------------------------------------------------------------------------- |
| Command record  | `Application/{{Module}}/{{BC}}/Commands/{{CommandName}}.cs`                       |
| Result type     | `Application/{{Module}}/{{BC}}/Results/{{CommandName}}Result.cs`                  |
| Handler class   | `Application/{{Module}}/{{BC}}/Handlers/{{CommandName}}Handler.cs`                |
| DI registration | Existing `Application/{{Module}}/{{BC}}/Extensions.cs` — add one `AddScoped` line |

---

## Template — Command record

```csharp
namespace ECommerceApp.Application.{{Module}}.{{BC}}.Commands
{
    public sealed record {{CommandName}}(
        {{Property1Type}} {{Property1}},
        {{Property2Type}} {{Property2}});
}
```

Rules:

- Commands are `sealed record` — immutable, value-semantics, no setters.
- Properties use primitive types or existing value objects (never raw domain entities).
- No validation logic in the record — use a `FluentValidation` validator (`/create-validator`).

---

## Template — Result type (enum — for simple state outcomes)

Use when: the operation succeeds or fails with distinct named states and the caller routes on the state.

```csharp
namespace ECommerceApp.Application.{{Module}}.{{BC}}.Results
{
    public enum {{CommandName}}Result
    {
        Success,
        NotFound,
        // Add domain-specific failure cases
    }
}
```

## Template — Result type (sealed record — for rich outcomes)

Use when: the result carries data on success (e.g. created entity ID) or multiple failure reasons with details.

```csharp
namespace ECommerceApp.Application.{{Module}}.{{BC}}.Results
{
    public sealed record {{CommandName}}Result
    {
        public bool IsSuccess { get; }
        public int? EntityId { get; }
        public string? FailureReason { get; }

        private {{CommandName}}Result(bool isSuccess, int? entityId, string? failureReason)
        {
            IsSuccess = isSuccess;
            EntityId = entityId;
            FailureReason = failureReason;
        }

        public static {{CommandName}}Result Success(int entityId) => new(true, entityId, null);
        public static {{CommandName}}Result NotFound() => new(false, null, "Entity not found.");
        // Add domain-specific factory methods for each failure case
    }
}
```

> Existing examples: `PlaceOrderResult` (record), `PaymentOperationResult` (enum), `CouponApplyResult` (enum).
> Pick the shape that matches the calling code's branching needs.

---

## Template — Handler class

```csharp
using ECommerceApp.Application.{{Module}}.{{BC}}.Commands;
using ECommerceApp.Application.{{Module}}.{{BC}}.Results;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.{{Module}}.{{BC}}.Interfaces;

namespace ECommerceApp.Application.{{Module}}.{{BC}}.Handlers
{
    internal sealed class {{CommandName}}Handler : ICommandHandler<{{CommandName}}, {{CommandName}}Result>
    {
        private readonly I{{AggregateRepository}} _repository;
        // Add further dependencies (other repos, domain services, IModuleClient)

        public {{CommandName}}Handler(I{{AggregateRepository}} repository)
        {
            _repository = repository;
        }

        public async Task<{{CommandName}}Result> HandleAsync(
            {{CommandName}} command,
            CancellationToken cancellationToken = default)
        {
            // 1. Load aggregate
            var entity = await _repository.GetByIdAsync(command.{{IdProperty}}, cancellationToken);
            if (entity is null)
                return {{CommandName}}Result.NotFound();

            // 2. Apply domain operation
            // entity.DoSomething(...);

            // 3. Persist
            await _repository.UpdateAsync(entity, cancellationToken);

            // 4. Publish cross-BC event if needed
            // await _moduleClient.PublishAsync(new SomethingHappened(entity.Id));

            return {{CommandName}}Result.Success(entity.Id);
        }
    }
}
```

Rules:

- Handler is always `internal sealed`.
- Handler owns one unit of work: load → operate → persist → publish.
- Never return domain entities — only result types.
- Throw `BusinessException` only for programming errors / invariant violations. Expected failures → result type.

---

## DI registration

In the consuming BC's `Application/{{Module}}/{{BC}}/Extensions.cs`:

```csharp
services.AddScoped<ICommandHandler<{{CommandName}}, {{CommandName}}Result>, {{CommandName}}Handler>();
```

---

## Invoking the handler (from a service or controller)

```csharp
// In Application service:
private readonly ICommandHandler<{{CommandName}}, {{CommandName}}Result> _handler;

public async Task<{{CommandName}}Result> {{MethodName}}Async({{CommandName}} command)
    => await _handler.HandleAsync(command);
```

```csharp
// In a Web/API controller (via service, never directly):
var result = await _someService.{{MethodName}}Async(new {{CommandName}}(param1, param2));
if (!result.IsSuccess)
    throw new BusinessException(result.FailureReason);
```

---

## Rules

1. `ICommandHandler<TCommand, TResult>` interface lives in `Application/Interfaces/` — create once, reuse across all BCs.
2. Handler is always `internal sealed`.
3. Command record is `sealed record` — no setters.
4. Result type: use `enum` for simple state, `sealed record` with static factories for rich data.
5. Do NOT place business logic in the command record or result type.
6. Always add a matching unit test — use `/create-unit-test` with the handler class as target.
7. This is separate from `IMessageHandler<T>` (cross-BC events). Do not confuse them.
