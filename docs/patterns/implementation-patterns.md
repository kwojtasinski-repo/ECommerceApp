# Reusable Implementation Patterns — ECommerceApp

> Reference document for implementing new bounded contexts.
> Used by: [`.github/prompts/bc-implementation.md`](../../.github/prompts/bc-implementation.md)
> Architecture decisions: ADR-0002, ADR-0003, ADR-0004
> Coding rules: [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)

A practical guide to the OOP and DDD patterns for new bounded context code.
Each section explains **when** to use the pattern, **how** to implement it,
and includes a minimal code template adapted to .NET 7 + MSSQL.

---

## Table of Contents

- [Module Structure](#1-module-structure)
- [Facade (Application Service)](#2-facade-application-service)
- [Value Object](#3-value-object)
- [Strongly-Typed ID](#4-strongly-typed-id)
- [Aggregate](#5-aggregate)
- [Domain Event](#6-domain-event)
- [Repository Interface](#7-repository-interface)
- [Repository Implementation](#8-repository-implementation)
- [Per-Module DbContext Interface](#9-per-module-dbcontext-interface)
- [DI Registration](#10-di-registration)
- [Strategy + Policy](#11-strategy--policy)
- [Anti-Corruption Layer (ACL)](#12-anti-corruption-layer-acl)
- [Optimistic Locking](#13-optimistic-locking)
- [Read Model](#14-read-model)

---

## 1. Module Structure

Every new BC uses **feature-folder organization** per ADR-0003 + ADR-0004.
The group prefix (`Sales`, `Inventory`, etc.) comes from ADR-0004 module taxonomy.

```
ECommerceApp.Domain/<Group>/<BcName>/
  <Aggregate>.cs
  <ValueObject>.cs
  I<Aggregate>Repository.cs

ECommerceApp.Application/<Group>/<BcName>/
  Services/
    <BcName>Service.cs       ← Facade
    I<BcName>Service.cs
  ViewModels/
    <Name>Vm.cs
  DTOs/
    <Name>Dto.cs

ECommerceApp.Infrastructure/<Group>/<BcName>/
  Repositories/
    <Aggregate>Repository.cs
  Configurations/
    <Aggregate>Configuration.cs
```

Namespace convention:
```csharp
namespace ECommerceApp.Domain.Sales.Orders;
namespace ECommerceApp.Application.Sales.Orders.Services;
namespace ECommerceApp.Infrastructure.Sales.Orders.Repositories;
```

---

## 2. Facade (Application Service)

**When**: one public entry point per BC. Orchestrates repositories, domain logic, and unit of work.
Other code calls only the Facade — never internal repositories or aggregates directly.

```csharp
// Application/<Group>/<BcName>/Services/IOrderService.cs
public interface IOrderService
{
    Task<int> PlaceOrder(PlaceOrderDto dto);
    Task<OrderDetailsVm?> GetOrder(int id);
}

// Application/<Group>/<BcName>/Services/OrderService.cs
internal sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly IMapper _mapper;

    public OrderService(IOrderRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<int> PlaceOrder(PlaceOrderDto dto)
    {
        var order = Order.Create(dto.CustomerId, dto.CurrencyId);
        return await _repo.AddAsync(order);
    }

    public async Task<OrderDetailsVm?> GetOrder(int id)
    {
        var order = await _repo.GetByIdAsync(id);
        return order is null ? null : _mapper.Map<OrderDetailsVm>(order);
    }
}
```

**Rules:**
- `internal sealed` — never public; exposed only via its interface.
- No business logic in the facade — delegate to aggregate methods.
- Throw `BusinessException` for domain violations.
- Never return raw domain entities — always map to VM or DTO.

---

## 3. Value Object

**When**: a concept defined entirely by its data, with no identity and immutable state.
Examples: `Money`, `TimeSlot`, `Address`, `Discount`.

```csharp
// Domain/<Group>/<BcName>/Money.cs
public record Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new BusinessException("Currency mismatch");
        return new Money(Amount + other.Amount, Currency);
    }
}
```

**Rules:**
- Always `record` — never `class`.
- No setters — produce new instances on change.
- Self-validating: throw `BusinessException` in factory/constructor for invalid state.
- For value objects with collections, override `Equals` and `GetHashCode` manually
  (record equality does not handle `ISet<T>` / `IList<T>` correctly).

---

## 4. Strongly-Typed ID

**When**: to prevent mixing IDs from different aggregates at compile time.

```csharp
// Domain/<Group>/<BcName>/OrderId.cs
public record OrderId(int Value)
{
    public static OrderId Of(int value) => new(value);
}
```

EF Core value converter in `IEntityTypeConfiguration<T>`:
```csharp
builder.Property(x => x.Id)
    .HasConversion(id => id.Value, raw => new OrderId(raw));
builder.HasKey(x => x.Id);
```

> **Note for .NET 7 / MSSQL**: `int` IDs remain preferred over `Guid`
> to align with the existing `BaseEntity` pattern and MSSQL identity columns.
> Introduce `Guid`-based strongly-typed IDs only for new greenfield aggregates
> that have no FK relationship with existing tables.

---

## 5. Aggregate

**When**: an entity that owns a consistency boundary. All state changes go through its own methods.

```csharp
// Domain/<Group>/<BcName>/Order.cs
public class Order : BaseEntity
{
    public string Number { get; private set; } = default!;
    public decimal Cost { get; private set; }
    public bool IsPaid { get; private set; }
    public string UserId { get; private set; } = default!;  // string only — no ApplicationUser nav prop
    public int CustomerId { get; private set; }
    public int CurrencyId { get; private set; }

    private readonly List<OrderItem> _orderItems = new();
    public IReadOnlyList<OrderItem> OrderItems => _orderItems.AsReadOnly();

    // EF Core requires a private parameterless constructor
    private Order() { }

    // Factory method — validates invariants before construction
    public static Order Create(int customerId, int currencyId, string userId)
    {
        if (customerId <= 0)
            throw new BusinessException("CustomerId must be positive");
        return new Order
        {
            Number = Guid.NewGuid().ToString(),
            CustomerId = customerId,
            CurrencyId = currencyId,
            UserId = userId,
            Cost = 0
        };
    }

    // State transition — aggregate owns its own transitions
    public OrderPaid MarkAsPaid(int paymentId)
    {
        if (IsPaid)
            throw new BusinessException(
                $"Order '{Id}' is already paid",
                ErrorCode.Create("orderAlreadyPaid", ErrorParameter.Create("id", Id)));

        IsPaid = true;
        return new OrderPaid(Id, paymentId, DateTime.UtcNow);
    }

    public void CalculateCost(decimal discountRate = 1.0m)
    {
        Cost = _orderItems.Sum(i => i.UnitCost * i.Quantity * discountRate);
    }
}
```

**Rules:**
- `private set` on all properties.
- Static factory method (`Create`) for construction with invariant checks.
- `private Order()` for EF Core materialization.
- State transitions return a domain event (or `void` if no event needed).
- No `ApplicationUser` navigation property — `string UserId` only.
- No Law of Demeter chains — pass required values as parameters.

---

## 6. Domain Event

**When**: to capture that something significant happened in the domain.
Returned from aggregate methods — not dispatched inside the aggregate.

```csharp
// Domain/<Group>/<BcName>/OrderPaid.cs
public record OrderPaid(
    int OrderId,
    int PaymentId,
    DateTime OccurredAt);
```

Call site in the Facade:
```csharp
var @event = order.MarkAsPaid(paymentId);
// handle or publish @event — the aggregate doesn't know about publishing
```

**Rules:**
- Past-tense name: `OrderPaid`, `PaymentConfirmed`, `SlotReserved`.
- Immutable `record` — all data set at creation.
- Aggregate returns the event; the Facade decides what to do with it.
- Do not inject `IMediator` or any publisher into aggregates.

---

## 7. Repository Interface

**When**: to abstract persistence from the domain. Lives in `Domain/<Group>/<BcName>/`.

```csharp
// Domain/<Group>/<BcName>/IOrderRepository.cs
public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetByNumberAsync(string number);
    Task<bool> ExistsByIdAndUserIdAsync(int id, string userId);
}
```

**Rules:**
- Extend `IGenericRepository<T>` for standard CRUD.
- Add only BC-specific query methods here.
- Interface lives in `Domain` — implementation in `Infrastructure`.
- No `IQueryable<T>` returned — compose queries inside the repository.

---

## 8. Repository Implementation

**When**: concrete EF Core implementation of the domain repository interface.

```csharp
// Infrastructure/<Group>/<BcName>/Repositories/OrderRepository.cs
internal sealed class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(Context context) : base(context) { }

    public async Task<Order?> GetByNumberAsync(string number)
        => await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Number == number);

    public async Task<bool> ExistsByIdAndUserIdAsync(int id, string userId)
        => await _context.Orders
            .AnyAsync(o => o.Id == id && o.UserId == userId);
}
```

**Rules:**
- `internal sealed` — not exposed outside Infrastructure.
- `AsNoTracking()` for all read-only queries.
- Extends `GenericRepository<T>` — gets `Add`, `Update`, `Delete`, `GetById` etc. for free.
- Uses the shared `Context` directly (single shared DB per ADR-0002 current state).

---

## 9. Per-Module DbContext Interface

**When**: to make the BC's persistence dependency explicit and testable.
Planned for implementation per ADR-0009 — define now, enforce enforcement later.

```csharp
// Infrastructure/<Group>/<BcName>/IOrderDbContext.cs
public interface IOrderDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
}

// Context.cs (add to existing shared Context — do NOT create a new DbContext)
public partial class Context : IOrderDbContext { }
```

> **Note**: Until ADR-0009 is implemented, use the shared `Context` directly in repositories.
> Define the interface now so that when the switch happens, the repository only needs
> its constructor parameter changed from `Context` to `IOrderDbContext`.

---

## 10. DI Registration

**When**: every new BC registers its own services in the layer's `DependencyInjection.cs`.
Do NOT register in `Startup.cs` / `Program.cs` directly.

```csharp
// Application/DependencyInjection.cs — add inside the existing method
services.AddScoped<IOrderService, OrderService>();

// Infrastructure/DependencyInjection.cs — add inside the existing method
services.AddScoped<IOrderRepository, OrderRepository>();
```

**Rules:**
- `IOrderService` registered as `internal sealed OrderService` — consumers only see the interface.
- New BC registrations are additive — existing registrations are never removed during parallel build.
- Remove old registrations only after the atomic switch (Step 9 of parallel change).

---

## 11. Strategy + Policy

**When**: a behaviour varies per context and strategies need to be composable.

```csharp
public interface IDiscountPolicy
{
    decimal Apply(decimal basePrice);

    static IDiscountPolicy None() => new NoDiscountPolicy();
    static IDiscountPolicy Fixed(decimal rate) => new FixedDiscountPolicy(rate);
    static IDiscountPolicy Composite(params IDiscountPolicy[] policies)
        => new CompositeDiscountPolicy(policies);
}

file class NoDiscountPolicy : IDiscountPolicy
{
    public decimal Apply(decimal basePrice) => basePrice;
}

file class FixedDiscountPolicy : IDiscountPolicy
{
    private readonly decimal _rate;
    public FixedDiscountPolicy(decimal rate) => _rate = rate;
    public decimal Apply(decimal basePrice) => basePrice * (1 - _rate);
}

public class CompositeDiscountPolicy : IDiscountPolicy
{
    private readonly IDiscountPolicy[] _policies;
    public CompositeDiscountPolicy(IDiscountPolicy[] policies) => _policies = policies;
    public decimal Apply(decimal basePrice)
        => _policies.Aggregate(basePrice, (price, p) => p.Apply(price));
}
```

**Rules:**
- `file` modifier hides concrete strategies from outside the file.
- Static factory methods on the interface — callers never reference concrete types.
- `CompositePolicy` is itself a strategy — it can be nested.

---

## 12. Anti-Corruption Layer (ACL)

**When**: an external system or legacy integration uses a different model.
Translate at the boundary — never let the foreign model leak into the domain.

```csharp
// Application/<Group>/<BcName>/Acl/LegacyOrderTranslator.cs
public class LegacyOrderTranslator
{
    public PlaceOrderDto Translate(LegacyOrderMessage message)
        => new PlaceOrderDto(
            CustomerId: message.ClientId,
            CurrencyId: ResolveCurrency(message.CurrencyCode),
            Lines: message.Items.Select(TranslateLine).ToList());

    private static int ResolveCurrency(string code) =>
        code switch { "PLN" => 1, "EUR" => 2, _ => throw new BusinessException($"Unknown currency: {code}") };

    private static OrderLineDto TranslateLine(LegacyItem i)
        => new(ItemId: i.Sku, Quantity: i.Qty, UnitCost: i.UnitPrice);
}
```

**Rules:**
- ACL lives inside the *receiving* BC — not in a shared layer.
- The external message type is defined in the ACL namespace — never used by the domain.
- Translator classes are pure: no side effects, no I/O.

---

## 13. Optimistic Locking

**When**: concurrent writes to the same record must be detected (e.g. inventory reservation).

Add `RowVersion` to the aggregate:
```csharp
public class Slot : BaseEntity
{
    public byte[] RowVersion { get; private set; } = default!;
    // ... other fields
}
```

Configure in EF Core:
```csharp
// Infrastructure/<Group>/Configurations/SlotConfiguration.cs
builder.Property(x => x.RowVersion)
    .IsRowVersion()
    .IsConcurrencyToken();
```

EF Core throws `DbUpdateConcurrencyException` automatically on conflict — catch and rethrow as `BusinessException`:
```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    throw new BusinessException("Concurrent modification detected — please retry.");
}
```

> **Note**: For bulk/high-volume operations, use Dapper with `UPDATE ... WHERE version = @v`
> and check affected rows manually (see ADR-0006 when implemented).

---

## 14. Read Model

**When**: a query needs data from multiple aggregates, or you want to avoid loading full aggregates for display.

```csharp
// Application/<Group>/<BcName>/DTOs/OrderSummaryDto.cs
public record OrderSummaryDto(
    int OrderId,
    string Number,
    decimal Cost,
    bool IsPaid,
    DateTime Ordered);

// Infrastructure/<Group>/<BcName>/Repositories/OrderReadModel.cs
internal sealed class OrderReadModel
{
    private readonly Context _context;
    public OrderReadModel(Context context) => _context = context;

    public async Task<IList<OrderSummaryDto>> GetOrdersForUser(string userId)
        => await _context.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .Select(o => new OrderSummaryDto(o.Id, o.Number, o.Cost, o.IsPaid, o.Ordered))
            .ToListAsync();
}
```

**Rules:**
- Read models are query-only — never mutated.
- `AsNoTracking()` always.
- May cross aggregate/table boundaries — that is their purpose.
- Keep in a separate class from the command-side repository.
