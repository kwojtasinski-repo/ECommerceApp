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
Examples: `Street`, `ZipCode`, `Email`, `Money`, `Discount`. See **ADR-0006**.

```csharp
// Domain/<Group>/<BcName>/ValueObjects/Street.cs
public sealed record Street
{
    public string Value { get; }

    public Street(string value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Street is required.");
        if (trimmed.Length > 200)
            throw new DomainException("Street must not exceed 200 characters.");
        Value = trimmed;
    }

    public override string ToString() => Value;
}
```

EF Core `HasConversion` in the owned-type or entity configuration:
```csharp
builder.Property(x => x.Street)
       .HasConversion(x => x.Value, v => new Street(v))
       .HasMaxLength(200)
       .IsRequired();
```

AutoMapper global converter (register once in `MappingProfile`):
```csharp
CreateMap<Street, string>().ConvertUsing(x => x.Value);
```

**Rules:**
- Always `sealed record` — never `class` or non-sealed record.
- No public setters — immutable after construction.
- Throw `DomainException` (not `ArgumentException` or `BusinessException`) for invariant violations.
  `DomainException` lives in `ECommerceApp.Domain.Shared` — use it from all BCs.
- Trim and normalise string input in the constructor (e.g. `Trim()`, `ToUpperInvariant()`).
- Override `ToString()` to return the human-readable value.
- For VOs with collections, override `Equals` / `GetHashCode` manually (record equality does not handle `ISet<T>` / `IList<T>` correctly).

### BC-specific slug VOs — `CategorySlug` and `TagSlug`

When a shared `Slug` VO is reused by entities with **different length constraints**, create
a BC-specific slug record that delegates format validation to `Slug` and adds its own length rule.
This removes the need for manual guards inside entity factory methods.

```csharp
// Domain/Catalog/Products/ValueObjects/CategorySlug.cs
public sealed record CategorySlug
{
    public string Value { get; }
    public CategorySlug(string value)
    {
        var slug = new Slug(value);          // validates lowercase/hyphens format
        if (slug.Value.Length > 100)
            throw new DomainException("Category slug must not exceed 100 characters.");
        Value = slug.Value;
    }
    public static CategorySlug FromName(string name)
    {
        var slug = Slug.FromName(name);
        if (slug.Value.Length > 100)
            throw new DomainException("Category slug exceeds 100 characters. Use a shorter name.");
        return new CategorySlug(slug.Value);
    }
    public override string ToString() => Value;
}
```

Length limits by entity (Catalog BC):

| VO | Max length |
|---|---|
| `CategorySlug` | 100 |
| `TagSlug` | 30 |

### Shared monetary Value Objects — `Price` and `Money`

Two monetary VOs live in `ECommerceApp.Domain.Shared` (see ADR-0006 § Migration plan):

**`Price`** — PLN-only catalog/order value. No currency field.
```csharp
// Domain/Shared/Price.cs
public sealed record Price
{
    public decimal Amount { get; }
    public Price(decimal amount)
    {
        if (amount <= 0) throw new DomainException("Price must be positive.");
        Amount = amount;
    }
    public Money ToMoney(decimal rate = 1m) => new Money(Amount, "PLN", rate);
}
```
Used by: `Item.Cost` (Catalog), `Order.TotalCost` (Orders).

**`Money`** — transactional amount with currency and exchange rate.
```csharp
// Domain/Shared/Money.cs
public sealed record Money
{
    public decimal Amount { get; }
    public string CurrencyCode { get; }
    public decimal Rate { get; }          // NBP rate at transaction time; PLN = 1
    public Money(decimal amount, string currencyCode, decimal rate) { ... }
    public static Money Pln(decimal amount) => new(amount, "PLN", 1m);
    public decimal ToBaseCurrency() => Amount * Rate;  // → PLN equivalent
}
```
Used by: `Payment.Amount` (Payments). Rate captures the NBP conversion fact at payment time.

EF Core mapping for `Price` (single column):
```csharp
builder.Property(i => i.Cost)
       .HasConversion(x => x.Amount, v => new Price(v))
       .HasPrecision(18, 4).IsRequired();
```

EF Core mapping for `Money` (owned type, three columns):
```csharp
builder.OwnsOne(p => p.Amount, m =>
{
    m.Property(x => x.Amount).HasColumnName("Amount").HasPrecision(18, 4);
    m.Property(x => x.CurrencyCode).HasColumnName("CurrencyCode").HasMaxLength(3);
    m.Property(x => x.Rate).HasColumnName("Rate").HasPrecision(18, 6);
});
```

---

## 4. Strongly-Typed ID

**When**: to prevent mixing IDs from different aggregates at compile time. See **ADR-0006**.

All typed IDs inherit from the shared base in `ECommerceApp.Domain.Shared`:

```csharp
// Domain/Shared/TypedId.cs  (shared — do not copy per BC)
public abstract record TypedId<T>(T Value)
{
    public static implicit operator T(TypedId<T> typedId) => typedId.Value;
    public override string ToString() => Value?.ToString() ?? string.Empty;
}
```

Each aggregate root defines its own sealed record:

```csharp
// Domain/<Group>/<BcName>/OrderId.cs
using ECommerceApp.Domain.Shared;

public sealed record OrderId(int Value) : TypedId<int>(Value);
```

EF Core value converter in `IEntityTypeConfiguration<T>`:
```csharp
builder.Property(x => x.Id)
       .HasConversion(x => x.Value, v => new OrderId(v))
       .ValueGeneratedOnAdd();
builder.HasKey(x => x.Id);
```

AutoMapper global converter (register once in `MappingProfile`):
```csharp
CreateMap<OrderId, int>().ConvertUsing(x => x.Value);
```

> **Id property on aggregate**: use `{ get; private set; }` — NOT `{ get; }` (init-only).
> EF Core needs to assign the database-generated value via reflection after `INSERT`.
> `{ get; }` breaks `ValueGeneratedOnAdd`.

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
