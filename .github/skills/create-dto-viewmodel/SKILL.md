---
name: create-dto-viewmodel
description: >
  Scaffold a DTO or ViewModel with a static extension method mapping from a domain entity.
  Uses ToDto()/ToViewModel() extension methods — the gradual AutoMapper removal path.
  Creates: DTO/VM record or class, Mappings static class with extension method.
  Use when AutoMapper is NOT appropriate for the new artifact (new BCs, migration path).
argument-hint: "<EntityName> [dto|viewmodel] [BcPath like Sales/Orders]"
---

# Create DTO / ViewModel (Extension Method Mapping)

Scaffold a DTO or ViewModel using **static extension method mapping** — Option B of the mapping strategy.

> **Context**: AutoMapper is still active for legacy artifacts (all existing `IMapFrom<T>` classes).
> Use this skill for **new** BCs and any artifact that is not already mapped via AutoMapper.
> Do NOT mix both patterns in the same BC — pick one and stay consistent within that BC.

---

## Choose: DTO or ViewModel?

| Type      | Suffix | Location                                                                   | Used by                        |
| --------- | ------ | -------------------------------------------------------------------------- | ------------------------------ |
| DTO       | `Dto`  | `Application/{{Module}}/{{BC}}/Dto/`                                       | API layer (`ECommerceApp.API`) |
| ViewModel | `Vm`   | `Application/{{Module}}/{{BC}}/ViewModels/` (or `Application/ViewModels/`) | Web layer (`ECommerceApp.Web`) |

---

## File layout

| File                     | Path                                                               |
| ------------------------ | ------------------------------------------------------------------ |
| DTO / ViewModel class    | `Application/{{Module}}/{{BC}}/Dto/{{EntityName}}Dto.cs`           |
| Mappings extension class | `Application/{{Module}}/{{BC}}/Mappings/{{EntityName}}Mappings.cs` |

---

## Template — DTO (record, recommended for new code)

```csharp
namespace ECommerceApp.Application.{{Module}}.{{BC}}.Dto
{
    public sealed record {{EntityName}}Dto(
        int Id,
        {{Property1Type}} {{Property1}},
        {{Property2Type}} {{Property2}});
        // Mirror the public properties the consumer needs — not the full entity
}
```

## Template — ViewModel (class, for MVC views that need mutable properties or nested collections)

```csharp
namespace ECommerceApp.Application.{{Module}}.{{BC}}.ViewModels
{
    public class {{EntityName}}Vm
    {
        public int Id { get; set; }
        public {{Property1Type}} {{Property1}} { get; set; }
        public {{Property2Type}} {{Property2}} { get; set; }
        public IReadOnlyList<{{ChildVm}}> Items { get; set; } = [];
    }
}
```

---

## Template — Mappings extension class

**File**: `Application/{{Module}}/{{BC}}/Mappings/{{EntityName}}Mappings.cs`

```csharp
using ECommerceApp.Application.{{Module}}.{{BC}}.Dto;
using ECommerceApp.Domain.{{Module}}.{{BC}};

namespace ECommerceApp.Application.{{Module}}.{{BC}}.Mappings
{
    internal static class {{EntityName}}Mappings
    {
        public static {{EntityName}}Dto ToDto(this {{EntityName}} entity)
            => new(
                entity.Id,
                entity.{{Property1}},
                entity.{{Property2}});

        // For collections — avoids ToList() allocation when caller already has IEnumerable
        public static IReadOnlyList<{{EntityName}}Dto> ToDtoList(
            this IEnumerable<{{EntityName}}> entities)
            => entities.Select(e => e.ToDto()).ToList();
    }
}
```

### With nested child mapping:

```csharp
public static {{EntityName}}Dto ToDto(this {{EntityName}} entity)
    => new(
        entity.Id,
        entity.{{Property1}},
        entity.Items.Select(i => i.ToDto()).ToList()); // child entity needs its own extension
```

---

## Template — reverse mapping (Dto → Command or domain input)

When you also need to map from a DTO back to a command or domain input (e.g. create/update):

```csharp
public static {{CommandName}} ToCommand(this Create{{EntityName}}Dto dto, int userId)
    => new(
        dto.{{Property1}},
        dto.{{Property2}},
        userId);
```

---

## Rules

1. Extension class is always `internal static` — it is not part of the public API.
2. Namespace matches the folder exactly (`Application.{{Module}}.{{BC}}.Mappings`).
3. Do NOT use `_mapper` or AutoMapper in new BCs — that is the legacy path.
4. Do NOT put mapping logic inside the DTO/VM class itself — keep it in the Mappings class.
5. DTO properties: expose **only** what the consumer layer needs — do not mirror every entity field.
6. For nullable entity properties, keep them nullable in the DTO — do not silently default to empty string.
7. TypedId: unwrap to `int` (`.Value`) in the DTO — DTOs should not depend on domain value objects.
8. After creating the DTO + Mappings, update any service method that returned `_mapper.Map<T>()` to use `.ToDto()` instead — then verify the mapper registration for that type can be removed.

---

## Migration checklist (AutoMapper removal for one BC)

When using this skill to migrate an existing BC away from AutoMapper:

- [ ] Create the new DTO/VM with extension method mapping
- [ ] Swap `_mapper.Map<XxxDto>(entity)` → `entity.ToDto()` in the service layer
- [ ] Remove the `IMapFrom<Xxx>` implementation from the DTO/VM class
- [ ] Remove the `CreateMap<>` call from the AutoMapper profile for this type
- [ ] Run `dotnet build` — confirm no missing mapping registrations
- [ ] Run `dotnet test` — confirm no mapping-related test failures
- [ ] Add or update unit tests for the `ToDto()` extension method
