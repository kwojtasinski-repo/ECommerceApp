# ADR-0006: Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives

## Status
Accepted

## Date
2026-02-23

## Context
The existing codebase exhibits **primitive obsession** throughout the domain, application,
and infrastructure layers:

1. **ID confusion** — entity identifiers are raw `int` values. Nothing prevents passing a
   `customerId` where an `orderId` is expected; the compiler accepts it and the bug only
   surfaces at runtime.

2. **Scattered validation** — string-based domain concepts (`Street`, `ZipCode`, `Email`, etc.)
   are validated in multiple places: FluentValidation DTOs, service methods, and controllers.
   The same "field is required" guard is written three or four times for every concept.
   Adding a new rule requires finding all those locations.

3. **Anemic domain objects** — a method signature such as
   `AddAddress(string street, string buildingNumber, int? flatNumber, int zipCode, string city, string country)`
   carries no domain semantics. The primitive types communicate nothing about constraints,
   format, or meaning — a caller can pass any `string` for `country` without the compiler
   objecting.

4. **No aggregate-level encapsulation** — because domain concepts have no identity of their
   own, invariants can only be enforced externally (in services or validators), making the
   domain model a passive data container.

The `AccountProfile` BC (ADR-0005) introduced typed IDs and value objects as a proof of
concept. The results are clear: the aggregate methods became shorter, validation disappeared
from services, and the compiler catches entire classes of bugs that were previously invisible.
The pattern should be standardised as a first-class building block for all bounded contexts.

## Decision

### 1. Shared `TypedId<T>` base in `ECommerceApp.Domain.Shared`
An `abstract record TypedId<T>(T Value)` lives in `ECommerceApp.Domain.Shared` so every
BC can derive its own typed ID without coupling to another BC's namespace.

```csharp
// Domain/Shared/TypedId.cs
public abstract record TypedId<T>(T Value)
{
    public static implicit operator T(TypedId<T> typedId) => typedId.Value;
    public override string ToString() => Value?.ToString() ?? string.Empty;
}
```

Each aggregate root defines its own sealed ID record:

```csharp
// Domain/AccountProfile/UserProfileId.cs
using ECommerceApp.Domain.Shared;

public sealed record UserProfileId(int Value) : TypedId<int>(Value);
```

### 2. Self-validating Value Objects as `sealed record`
Every domain concept that is defined by its value — not by an identifier — is modelled
as a `sealed record` with validation in its constructor. No public setters, no factory
methods, no external guards.

```csharp
// Domain/AccountProfile/ValueObjects/Street.cs
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

The constructor enforces the invariant once, at the point of creation. Any code that
holds a `Street` instance is guaranteed to hold a valid street.

### 3. `DomainException` for domain invariant violations
Value objects and aggregates throw `DomainException` — not `ArgumentException` or
`BusinessException` — to signal that a domain rule was violated. Each BC defines its own
`DomainException` (or re-uses the one from `ECommerceApp.Domain.Shared`; see Migration plan).

### 4. EF Core `HasConversion` for every VO property
Every VO property on a mapped entity requires an explicit converter in the EF configuration:

```csharp
ab.Property(a => a.Street)
  .HasConversion(x => x.Value, v => new Street(v))
  .HasMaxLength(300)
  .IsRequired();
```

The EF Core private constructor for the entity takes the VO types directly (EF applies the
converter and injects the VO instance). The public domain constructor takes primitives and
delegates validation to the VO constructors.

### 5. AutoMapper global type converters for VO → primitive mapping
A global type converter per VO eliminates per-mapping `ForMember` noise:

```csharp
CreateMap<Street, string>().ConvertUsing(x => x.Value);
CreateMap<UserProfileId, int>().ConvertUsing(x => x.Value);
```

## Consequences

### Positive
- **Compile-time ID safety** — passing the wrong ID type is a build error, not a runtime bug.
- **Self-documenting APIs** — `UpdateAddress(AddressId id, Street street, ZipCode zip, …)`
  reads like the domain, not like a data transfer form.
- **Zero duplicated validation** — invariants live in one place: the VO constructor.
  Removing a rule, changing a limit, or adding a new constraint requires editing one file.
- **Thinner services** — service methods no longer carry guard clauses for primitive values;
  they receive already-valid objects from the domain.
- **Record equality for free** — record value semantics give correct `Equals` / `GetHashCode`
  without boilerplate.

### Negative
- **More files** — every domain concept becomes its own file. A BC with six address fields
  produces six VO files.
- **EF Core ceremony** — each VO property needs a `HasConversion` call in the configuration.
  Forgetting one produces a runtime mapping error, not a build error.
- **AutoMapper converters** — global converters must be registered for every VO → primitive
  direction. Missing one causes a silent default-value mapping.
- **Migration cost** — existing BCs (Customer, Order, Payment, etc.) still use raw primitives.
  They must be migrated incrementally; doing it all at once is a high-risk mass refactor.

### Risks & mitigations
- **Risk**: EF Core `ValueGeneratedOnAdd` does not work with `{ get; }` init-only properties
  on records. **Mitigation**: Use `{ get; private set; }` for the `Id` property so EF Core can
  assign the generated value via reflection after `INSERT`.
- **Risk**: Nullable VOs (e.g. `FlatNumber?`) require explicit null handling in the EF converter
  and in AutoMapper. **Mitigation**: use `x => x!.Value` in the EF converter (EF handles null
  automatically for nullable reference types) and `ForMember` with null propagation in AutoMapper.
- **Risk**: New team members unfamiliar with the pattern apply raw primitives in a new BC.
  **Mitigation**: this ADR + `implementation-patterns.md` (sections 3–4) + code review checklist.

## Alternatives considered

- **FluentValidation only** — validation is centralised in DTO validators, domain objects stay
  anemic. Rejected: validation is duplicated across DTO, service, and domain layers; the domain
  model has no invariant enforcement of its own.
- **Vogen / StronglyTypedId NuGet packages** — auto-generate boilerplate for typed IDs. Rejected:
  adds an external dependency, generates non-transparent code, and does not solve the VO
  validation problem.
- **Inline `record` per BC without a shared base** — each BC defines its own ID record without
  inheriting from `TypedId<T>`. Rejected: loses the implicit conversion operator and `ToString`
  override; every BC reimplements the same two lines.
- **`struct`-based IDs** — value-type IDs avoid heap allocation. Rejected: EF Core `HasConversion`
  and AutoMapper work more reliably with reference-type records; struct nullability adds
  complexity for optional relationships.

## Migration plan

1. **New BCs** — all new bounded contexts MUST define typed IDs and value objects from day one.
   Use `sealed record <Name>Id(int Value) : TypedId<int>(Value)` for every aggregate ID,
   and `sealed record <VO>` with validation in the constructor for every domain concept.

2. **Existing BCs** — migrate on demand, one BC at a time, as part of a dedicated BC
   modernisation sprint. Priority order (most benefit / least risk first):
   `Customer` → `Order` → `Payment` → `Refund` → `Coupon`.

3. **Shared `DomainException`** — `DomainException` was moved to `ECommerceApp.Domain.Shared`
   when the Catalog/Products BC adopted the VO pattern alongside AccountProfile.
   `ECommerceApp.Domain.AccountProfile.DomainException` still exists for backward compatibility
   and will be consolidated when AccountProfile is updated to use the shared one.

4. **Shared monetary VOs** — `Price` and `Money` live in `ECommerceApp.Domain.Shared`:
   - `Price` (PLN-only, no currency field) — used by Catalog (`Item.Cost`) and Orders (`Order.TotalCost`).
   - `Money` (Amount + CurrencyCode + Rate) — used by Payments (`Payment.Amount`).
     Rate captures the NBP exchange rate at transaction time for audit and PLN conversion.
   - `Price.ToMoney()` bridges Catalog/Orders to the Payment BC at checkout time.

5. **`implementation-patterns.md`** — sections 3 (Value Object) and 4 (Strongly-Typed ID)
   updated to reflect this decision (done alongside this ADR).
