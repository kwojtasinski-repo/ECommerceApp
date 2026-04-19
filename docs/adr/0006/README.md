# ADR-0006: TypedId and Value Objects as Shared Domain Primitives

**Status**: Accepted
**BC**: Shared / Domain primitives

## What this decision covers
`TypedId<T>` sealed record base, per-BC typed IDs (`OrderId`, `PaymentId`, etc.),
and shared value objects: `Price`, `Money`, `Quantity`, `StockQuantity`.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0006-typedid-and-value-objects-as-shared-domain-primitives.md | Full design: TypedId pattern, VO invariants, EF conversions | Creating a new TypedId or VO |
| example-implementation/typedid-usage-examples.md | How to define and use a TypedId | Adding a new typed ID |
| example-implementation/value-object-patterns.md | Price, Money, Quantity creation and EF mapping | Working with shared VOs |

## Key rules
- New BC-specific IDs extend `TypedId<int>` (not raw int/Guid)
- `Price` and `Money` require `> 0`; `Quantity` requires `>= 0`
- EF conversions registered in each BC's DbContext configuration

## Related ADRs
- ADR-0003 (folder structure) — where TypedIds live per BC
