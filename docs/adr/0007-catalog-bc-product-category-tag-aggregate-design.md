# ADR-0007: Catalog BC — Product, Category, and Tag Aggregate Design

## Status
Accepted

## Date
2026-02-25

## Context
The existing `Item` domain model in `ECommerceApp.Domain.Model` is fully anemic: flat properties,
no invariants, no state machine, and a `Brand` / `Type` navigation-property coupling that is
never enforced at the domain level. Images are managed through a separate `ImageService` that
operates on plain `Image` domain objects with no knowledge of which product owns them. Tags are
attached via a join table with no aggregate ownership.

As part of the post-event-storming refactoring strategy (ADR-0002), the Catalog domain was
identified as **ready for a parallel implementation** because:

1. It is self-contained — no cross-BC state transitions, unlike Orders or Payments.
2. Product lifecycle (`Draft → Published ↔ Unpublished`) is a simple, well-understood
   state machine that demonstrates the rich aggregate pattern without saga complexity.
3. Several value concepts (name, slug, price, description, quantity, image file name) were
   scattered as raw primitives, causing repeated validation across multiple layers.
4. The `Item` naming conflicted with business language — the domain calls them **Products**.

During implementation the following additional design questions arose and were resolved:

- **Slug length diverges by entity** — `CategorySlug` (max 100) and `TagSlug` (max 30) have
  different constraints; sharing a single `Slug` VO requires manual guard checks inside every
  aggregate method.
- **Main image invariant** — the `Image.SetAsMain()` and `Image.ClearMain()` operations must
  be callable from the owning aggregate only. A public setter would allow external code to
  create a state where multiple images are flagged as main.
- **SortOrder assignment** — requiring the caller to supply `sortOrder` on every `AddImage`
  call creates collision risk and places position-tracking responsibility on the client.
- **Tag simplification** — `Color` and `IsVisible` fields were removed as premature
  generalisation; they will be added back once there is a concrete business requirement.
- **Category simplification** — `ParentId` (hierarchy) and `IsVisible` were deferred; the
  category tree feature requires a dedicated ADR and UI design.

## Decision

### 1. Aggregate renamed: `Item` → `Product`
The aggregate root class, its typed ID, and the join entity are renamed:

| Old | New | Table |
|---|---|---|
| `Item` | `Product` | `catalog.Products` |
| `ItemId` | `ProductId` | FK column `ProductId` |
| `ItemTag` | `ProductTag` | `catalog.ProductTags` |

### 2. Domain model structure
```
Domain/Catalog/Products/
  Product.cs              ← aggregate root
  ProductId.cs            ← TypedId<int>
  Category.cs             ← entity (no parent, no visibility — v1)
  CategoryId.cs           ← TypedId<int>
  Tag.cs                  ← entity (no color, no visibility — v1)
  TagId.cs                ← TypedId<int>
  Image.cs                ← owned entity (ProductId FK)
  ImageId.cs              ← TypedId<int>
  ProductTag.cs           ← join entity
  ProductStatus.cs        ← enum (Draft | Published | Unpublished)
  IProductRepository.cs
  ICategoryRepository.cs
  IProductTagRepository.cs
  Events/
    ProductCreated.cs
    ProductPublished.cs
    ProductUnpublished.cs
  ValueObjects/
    ProductName.cs          max 150, min 3
    ProductDescription.cs   max 300, empty allowed
    ProductQuantity.cs      >= 0
    CategoryName.cs         max 100
    CategorySlug.cs         max 100
    TagName.cs              max 50
    TagSlug.cs              max 30
    ImageFileName.cs        max 500
```

### 3. BC-specific slug value objects: `CategorySlug` and `TagSlug`
Rather than a shared `Slug` VO with manual length guards inside each entity method, each BC
entity defines its own typed slug that embeds the length constraint in the constructor.
Both delegate format validation to the shared `Slug` VO:

```csharp
public sealed record CategorySlug
{
    public string Value { get; }
    public CategorySlug(string value)
    {
        var slug = new Slug(value);           // validates lowercase/format
        if (slug.Value.Length > 100)
            throw new DomainException("Category slug must not exceed 100 characters.");
        Value = slug.Value;
    }
    public static CategorySlug FromName(string name) { ... }
}
```

This means entity factory methods (`Category.Create`, `Tag.Create`) contain no length guard
logic — the invariant lives in the VO, which is the single source of truth.

### 4. Product `Status` state machine
```
Draft ──Publish()──► Published ──Unpublish()──► Unpublished ──Publish()──► Published
```
- `Publish()` throws `DomainException` if already `Published`.
- `Unpublish()` throws `DomainException` if not `Published`.
- State transitions return domain events (`ProductPublished`, `ProductUnpublished`).
- `Create(...)` always starts in `Draft`; returns `(Product, ProductCreated)` tuple.

### 5. Image ownership and main-image invariant
`Image` is an owned entity of `Product` (max 5 per product). The `IsMain` flag is
controlled exclusively by `Product` through:

- `AddImage(fileName)` — auto-assigns `sortOrder = _images.Count`; first image becomes main
- `SetMainImage(int imageId)` — clears all main flags, then sets exactly one; throws if imageId not found
- `RemoveImage(int imageId)` — removes image, re-compacts sort order (0, 1, 2, …), promotes
  first remaining image as main if the removed one was main

`Image.SetAsMain()` and `Image.ClearMain()` are **`internal`** — callable only within
`ECommerceApp.Domain`. External code (Application, Infrastructure) cannot bypass the invariant.

### 6. Sort order: auto-assign + explicit reorder
`Product.AddImage(fileName)` assigns `sortOrder = _images.Count` (append-to-end).
Reordering is done atomically via `Product.ReorderImages(IList<int> orderedImageIds)`, which
validates the complete list and reassigns positions 0, 1, 2, … in one operation.
This eliminates collision risk and removes position-tracking responsibility from callers.

### 7. Per-BC `ProductDbContext` with `catalog.*` schema
Own `ProductDbContext : DbContext` with `HasDefaultSchema("catalog")`.
Tables: `catalog.Products`, `catalog.Categories`, `catalog.Tags`, `catalog.Images`,
`catalog.ProductTags`.

All string columns have explicit `HasMaxLength` constraints — no `varchar(max)`.

| Column | Max |
|---|---|
| `Products.Name` | 150 |
| `Products.Description` | 300 |
| `Categories.Name` | 100 |
| `Categories.Slug` | 100 |
| `Tags.Name` | 50 |
| `Tags.Slug` | 30 |
| `Images.FileName` | 500 |

### 8. Category — v1 scope
`Category` has only `CategoryName Name` and `CategorySlug Slug`. `ParentId` (hierarchy)
and `IsVisible` are explicitly deferred — they require a separate UI and ADR before
implementation. A note is tracked in the refactoring progress table below.

### 9. Tag — v1 scope
`Tag` has only `TagName Name` and `TagSlug Slug`. `Color` and `IsVisible` are deferred.

### 10. AutoMapper global converters
The following global converters are registered in `MappingProfile` so per-VM mappings
do not need `ForMember` for type conversion:

```csharp
CreateMap<ProductId, int>().ConvertUsing(x => x.Value);
CreateMap<ProductName, string>().ConvertUsing(x => x.Value);
CreateMap<Price, decimal>().ConvertUsing(x => x.Amount);
CreateMap<TagName, string>().ConvertUsing(x => x.Value);
CreateMap<CategoryName, string>().ConvertUsing(x => x.Value);
CreateMap<CategorySlug, string>().ConvertUsing(x => x.Value);
CreateMap<TagSlug, string>().ConvertUsing(x => x.Value);
CreateMap<ProductDescription, string>().ConvertUsing(x => x.Value);
CreateMap<ProductQuantity, int>().ConvertUsing(x => x.Value);
CreateMap<ImageFileName, string>().ConvertUsing(x => x.Value);
```

## Consequences

### Positive
- **Compile-time safety** — `ProductId`, `CategoryId`, `TagId`, `ImageId` prevent ID mix-ups.
- **Single validation location** — every constraint lives in its VO; services and controllers
  contain zero guard clauses for domain concepts.
- **Invariant encapsulation** — `IsMain` can only be changed through `Product` aggregate
  methods; no external code can create an inconsistent multi-main state.
- **Clean sort order** — `ReorderImages` is atomic and collision-free; callers provide intent,
  the aggregate enforces structure.
- **No varchar(max)** — every string column has an explicit length; schema is deterministic.
- **BC-specific slug VOs** — `CategorySlug` and `TagSlug` encode their own length constraints;
  `Tag.Create` and `Category.Create` contain no manual length checks.

### Negative
- More files per entity compared to the anemic model (VO, config, repository, DTO, VM each in their own file).
- `ProductDescription` allows empty string — callers must not assume a non-empty description.

### Risks & mitigations
- **EF Core LINQ translation of `.Value` on VO-converted properties** — EF Core 7 handles
  member access on value-converted types. Queries use `p.Quantity.Value > 0` and
  `p.Description.Value.Contains(q)`. If a future EF upgrade changes translation behaviour,
  the affected queries are isolated to `ProductRepository`.
- **`internal` on `Image.SetAsMain`/`ClearMain`** — EF Core materializes `IsMain` via the
  property setter (reflection), not through `SetAsMain`. The `internal` modifier has no effect
  on EF Core's ability to read and write the column.

## Alternatives considered
- **Shared `Slug` VO with caller-side length checks** — rejected because the guard then lives
  in the entity method, not in the type. Two entities with different slug lengths cannot both
  reuse the same VO without the VO being aware of both limits.
- **`isMain` parameter on `AddImage`** — rejected because it requires the caller to track
  whether an image should be main and does not prevent multiple main images.
- **Domain service for main image management** — considered but rejected for v1; the `Product`
  aggregate already knows all its images and can enforce the invariant directly. A domain
  service would be appropriate only if the invariant needed to span multiple aggregates.
- **`SortOrder` as value object** — rejected; a single non-negative integer rule does not
  justify a VO. Complexity does not add clarity here.

## Migration plan

1. New Catalog BC implementation is complete (parallel to legacy `Domain.Model.Item`).
2. Migration (`InitCatalogSchema`) targets `ProductDbContext` and creates `catalog.*` schema.
3. Migration requires explicit approval per `migration-policy.md`.
4. Switch to new Catalog BC in Web/API controllers is a separate step tracked in the
   bounded-context-map refactoring progress tracker.
5. Legacy `Domain.Model.Item`, `Image`, `Tag`, `Brand`, `Type` and related services/repositories
   are removed only after the atomic switch.

## Conformance checklist

- [ ] All `Product`, `Category`
- [ ] `Product.Create(...)` is `static`, returns `(Product, ProductCreated)`
- [ ] `Product.cs` has a `private Product()` parameterless constructor for EF Core
- [ ] `Product.cs`, `Category.cs`, `Tag.cs` live under `Domain/Catalog/Products/`
- [ ] No `ICollection<OrderItem>` or other cross-BC navigation in `Product.cs`
- [ ] `Image.SetAsMain()` and `Image.ClearMain()` are `internal` — not `public`
- [ ] `ProductDbContext` uses schema `"catalog"`
- [ ] `ProductService`, `CategoryService`, `ProductTagService` are each `internal sealed`
- [ ] `CategorySlug` enforces max length 100; `TagSlug` enforces max length 30

## Implementation Status

| Layer | Status |
|---|---|
| Domain (`Product`, `Category`, `Tag`, owned `Image`, `ProductStatus`, domain events, typed IDs, value objects, repository interfaces) | ✅ Done |
| Infrastructure (`ProductDbContext`, `catalog.*` schema, EF configs with `HasMaxLength`, `ProductRepository`, `CategoryRepository`, `ProductTagRepository`, DI) | ✅ Done |
| Application (DTOs with FluentValidation, ViewModels with AutoMapper, `IProductService`/`ProductService`, `ICategoryService`, `IProductTagService`, global VO converters) | ✅ Done |
| Unit tests (`ProductAggregateTests`, `ValueObjectTests`) | ✅ Done |
| DB migration (`InitCatalogSchema`, `catalog.*` tables) | ⬜ Pending approval |
| Integration tests | ⬜ Not started |
| Controller migration (`ItemController`, `ImageController`, `TagController`) | ⬜ Not started |
| Atomic switch — remove legacy `Domain.Model.Item`, `Image`, `Tag`, `Brand`, `Type` | ⬜ After integration tests |
| `Category.ParentId` + `IsVisible` (hierarchy / filtering) | ⬜ Separate ADR required — ADR-0007 §8 |
| `Tag.Color` + `IsVisible` | ⬜ Deferred — ADR-0007 §9 |
