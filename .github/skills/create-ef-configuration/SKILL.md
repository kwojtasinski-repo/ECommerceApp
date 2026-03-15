---
name: create-ef-configuration
description: >
  Scaffold an EF Core IEntityTypeConfiguration<T> class for a domain entity.
  Includes TypedId conversion, value object conversions, owned types, indexes,
  and precision settings following project conventions.
argument-hint: "<EntityName> [BcInfraPath like Catalog/Products]"
---

# Create EF Configuration

Generate an entity configuration class for a domain entity.

## File placement

`ECommerceApp.Infrastructure/<Module>/<BC>/Configurations/{{EntityName}}Configuration.cs`

## Template

```csharp
using ECommerceApp.Domain.{{DomainNamespace}};
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceApp.Infrastructure.{{InfraNamespace}}.Configurations
{
    internal sealed class {{EntityName}}Configuration : IEntityTypeConfiguration<{{EntityName}}>
    {
        public void Configure(EntityTypeBuilder<{{EntityName}}> builder)
        {
            builder.ToTable("{{TableName}}");

            // Primary key with TypedId conversion
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .HasConversion(x => x.Value, v => new {{EntityName}}Id(v))
                   .ValueGeneratedOnAdd();

            // String property with value object conversion
            builder.Property(e => e.Name)
                   .HasConversion(x => x.Value, v => new {{ValueObjectType}}(v))
                   .HasMaxLength({{maxLength}})
                   .IsRequired();

            // Decimal property with precision
            builder.Property(e => e.Cost)
                   .HasPrecision(18, 4)
                   .IsRequired();

            // Enum as string
            builder.Property(e => e.Status)
                   .HasConversion<string>()
                   .HasMaxLength(30)
                   .IsRequired();

            // Owned type (value object embedded in separate table)
            builder.OwnsOne(e => e.{{OwnedProperty}}, owned =>
            {
                owned.ToTable("{{OwnedTableName}}");
                owned.WithOwner().HasForeignKey("{{EntityName}}Id");
                owned.Property(p => p.SomeField).HasMaxLength(100).IsRequired();
            });
            builder.Navigation(e => e.{{OwnedProperty}}).IsRequired();

            // Unique index
            builder.HasIndex(e => e.{{IndexedProperty}}).IsUnique();
        }
    }
}
```

## Property patterns — pick what applies

| Domain type                 | EF conversion                                                                       |
| --------------------------- | ----------------------------------------------------------------------------------- |
| `TypedId` (int-based)       | `.HasConversion(x => x.Value, v => new XxxId(v)).ValueGeneratedOnAdd()`             |
| `TypedId` (string-based)    | `.HasConversion(x => x.Value, v => new XxxId(v)).HasMaxLength(450)`                 |
| Value object (single value) | `.HasConversion(x => x.Value, v => new Xxx(v))`                                     |
| `Price` / `Money`           | `.HasConversion(x => x.Amount, v => new Price(v)).HasPrecision(18, 4)`              |
| Enum                        | `.HasConversion<string>().HasMaxLength(30)`                                         |
| Owned type (in same table)  | `OwnsOne(e => e.X, o => { o.Property(...).HasColumnName("X"); })`                   |
| Owned type (separate table) | `OwnsOne(e => e.X, o => { o.ToTable("..."); o.WithOwner().HasForeignKey("..."); })` |

## Rules

1. Class visibility: `internal sealed`
2. Table name = plural of entity name (e.g., `Products`, `Orders`)
3. All TypedId properties MUST have explicit conversion — never rely on convention
4. All decimal properties MUST specify `HasPrecision(18, 4)`
5. All string properties MUST specify `HasMaxLength()`
6. Owned types with `IsRequired()` navigation need `builder.Navigation(e => e.X).IsRequired()`
7. Read the entity's domain class before generating — match all properties exactly
