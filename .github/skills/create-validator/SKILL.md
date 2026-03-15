---
name: create-validator
description: >
  Scaffold a FluentValidation AbstractValidator<T> for a DTO or ViewModel.
  Supports simple property rules, conditional rules, collection validation,
  and child validator composition.
argument-hint: "<DtoOrVmName> [BcName]"
---

# Create Validator

Generate a FluentValidation validator class for a DTO or ViewModel.

## File placement

Co-locate the validator in the **same file** as the DTO/ViewModel it validates:

- DTOs: `ECommerceApp.Application/{{Module}}/{{BC}}/Dto/{{DtoName}}.cs`
- ViewModels: `ECommerceApp.Application/ViewModels/{{VmName}}.cs`

If the file already has only the DTO/VM class, append the validator class at the bottom of the same file.

## Template — simple validator

```csharp
using FluentValidation;

public class {{TypeName}}Validation : AbstractValidator<{{TypeName}}>
{
    public {{TypeName}}Validation()
    {
        // Required string
        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(255);

        // Required numeric (greater than zero)
        RuleFor(x => x.Quantity)
            .NotNull()
            .GreaterThan(0);

        // Required decimal with range
        RuleFor(x => x.Price)
            .NotNull()
            .GreaterThan(0)
            .LessThanOrEqualTo(999999.99m);

        // Required enum / id reference
        RuleFor(x => x.CategoryId)
            .NotNull()
            .GreaterThan(0);
    }
}
```

## Template — conditional and collection rules

```csharp
public class {{TypeName}}Validation : AbstractValidator<{{TypeName}}>
{
    public {{TypeName}}Validation()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .NotEmpty();

        // Conditional rule — only validate when property has value
        When(x => x.Items is not null && x.Items.Count > 0, () =>
        {
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId)
                    .NotNull()
                    .GreaterThan(0);
                item.RuleFor(i => i.Quantity)
                    .NotNull()
                    .GreaterThan(0);
            });
        });
    }
}
```

## Template — wrapper validator (VM that wraps a DTO)

```csharp
public class {{VmName}}Validation : AbstractValidator<{{VmName}}>
{
    public {{VmName}}Validation()
    {
        // Defer to the inner DTO's validator
        RuleFor(x => x.{{DtoProperty}}).SetValidator(new {{DtoName}}Validation());
    }
}
```

## Common rule patterns

| Scenario | Rule |
|---|---|
| Required string | `.NotNull().NotEmpty().MaximumLength(N)` |
| Optional string with max length | `.MaximumLength(N).When(x => x.Prop is not null)` |
| Required int > 0 | `.NotNull().GreaterThan(0)` |
| Required decimal > 0 | `.NotNull().GreaterThan(0).LessThanOrEqualTo(max)` |
| Required email | `.NotNull().NotEmpty().EmailAddress()` |
| Collection not empty | `.NotNull().NotEmpty()` on the list property |
| Each item in collection | `RuleForEach(x => x.Items).ChildRules(...)` |
| Conditional block | `When(x => condition, () => { RuleFor(...); })` |
| Nested DTO | `RuleFor(x => x.Inner).SetValidator(new InnerValidation())` |

## Rules

1. Validator class name: `{{TypeName}}Validation` (suffix is `Validation`, not `Validator`)
2. Co-locate in the same `.cs` file as the DTO/VM — do NOT create a separate file
3. Validators are auto-discovered by `AddFluentValidation(cfg => cfg.RegisterValidatorsFromAssembly(...))` — no manual DI registration needed
4. Read the DTO/VM properties before generating — match every required property
5. Use `NotNull()` before `NotEmpty()` for reference types and nullable value types
6. String lengths must match EF `HasMaxLength()` from the entity configuration
