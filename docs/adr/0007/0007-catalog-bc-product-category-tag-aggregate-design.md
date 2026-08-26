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

### 2–10. Implementation detail

The aggregate layout, value-object constraints, product state transitions, image invariant,
catalog schema, deferred v1 fields, and mapping examples are implementation guidance rather
than additional architectural decisions. The maintained examples are collected here:

- [Catalog implementation sketch](example-implementation/product-aggregate-usage.md)

The architectural decisions remain in this ADR: Catalog owns Product, Category, Tag, and Image;
image ownership and soft-delete preserve product invariants; category and tag v1 deliberately
exclude hierarchy, visibility, and color; and slug constraints are encoded in Catalog-specific
value objects.

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

## References

- Related ADRs:
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../0002/0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](../0003/0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0006 — TypedId and Value Objects as Shared Domain Primitives](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
  - [ADR-0011 — Inventory/Availability BC Design](../0011/0011-inventory-availability-bc-design.md) (ProductSnapshot consumes Catalog events)
  - [ADR-0012 — Presale/Checkout BC Design](../0012/0012-presale-checkout-bc-design.md) (Presale/Checkout BC; Slice 1 has zero message subscriptions)
