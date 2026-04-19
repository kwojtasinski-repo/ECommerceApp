---
description: "Rules for Domain/Shared primitives in ECommerceApp: TypedId, Money, Price, UnitCost, Quantity. Applies whenever editing shared domain types."
applyTo: "ECommerceApp.Domain/Shared/**/*.cs"
---

# Shared Domain Primitives — ECommerceApp

> Source of truth: [ADR-0006](../../docs/adr/0006/README.md)
> Read before creating or modifying any type under `ECommerceApp.Domain/Shared/`.

## TypedId<T>

- All entity and aggregate identifiers MUST extend `TypedId<T>` — never use raw `int`, `Guid`, or `string` as an ID in a domain model.
- Define each ID as a `sealed record` in the BC's own folder (e.g. `Domain/Sales/Orders/OrderId.cs`), NOT inside `Domain/Shared/`.
- `Domain/Shared/TypedId.cs` is the base only — do not add BC-specific IDs here.
- Example: `public sealed record OrderId(int Value) : TypedId<int>(Value);`
- Implicit conversion to `T` is provided by the base — do not re-define it.

## Money

- `Money` is immutable — `Amount`, `CurrencyCode`, and `Rate` are set at construction only.
- Use `Money.Pln(amount)` factory for PLN amounts — do not call the constructor with `"PLN"` and `1m` directly.
- `Amount` must be positive; `Rate` must be positive; `CurrencyCode` must be non-empty — validation is in the constructor.
- Do NOT add currency-conversion logic outside `Money.ToBaseCurrency()`.

## Price

- `Price` wraps a single positive `decimal Amount` — use for catalogue/list prices.
- Use `Price.ToMoney(rate)` to convert to a `Money` value; do not construct `Money` directly from a `Price`.
- `Price` is immutable — no setters, no mutation after construction.

## UnitCost

- `UnitCost` wraps a non-negative `decimal Amount` — use for unit-level costs where zero is valid (e.g. free items).
- Prefer `UnitCost.Zero` for zero values — do not use `new UnitCost(0)`.
- Do NOT use `UnitCost` for catalogue prices — use `Price` instead.

## Quantity

- `Quantity` wraps a positive `int Value` — use for item counts; zero and negative are invalid.
- Do NOT use raw `int` for quantity fields on aggregates or DTOs that flow through domain logic.

## General rules for all shared value objects

- All shared value objects are `sealed record` — never `class`.
- No setters — all properties use `{ get; }` (constructor-assigned only).
- Self-validating constructors throw `DomainException` (from `ECommerceApp.Domain.Shared`) — never return `null` or a sentinel value.
- Do NOT add EF Core concerns (e.g. `[Column]`, `[Key]`) inside `Domain/Shared/` — configure via `IEntityTypeConfiguration<T>` in Infrastructure.
- Do NOT add new shared primitives without a corresponding ADR or update to ADR-0006.
