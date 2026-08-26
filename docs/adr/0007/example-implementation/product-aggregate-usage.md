# Catalog implementation sketch

This document contains implementation guidance extracted from ADR-0007. It is deliberately
not a second decision record; changes to aggregate ownership, cross-BC contracts, or deferred
scope belong in the ADR.

## Domain layout

Catalog keeps the aggregate and its supporting entities under `Domain/Catalog/Products/`:

- `Product` is the aggregate root.
- `Category` and `Tag` are Catalog entities with typed IDs and name/slug value objects.
- `Image` is owned by `Product`; `ProductTag` is the join entity.
- `ProductStatus` has `Draft`, `Published`, `Unpublished`, and `Discontinued` values.
- `UnpublishReason` is owned by Catalog and is carried by `ProductUnpublished`.

`Product`, `Category`, `Tag`, and `Image` use private setters and private parameterless
constructors for EF Core materialization. `Product.Create` validates the category ID and starts
the product in `Draft`.

## Value objects and state transitions

`CategorySlug` and `TagSlug` delegate format validation to the shared `Slug` value object while
enforcing their own maximum lengths: 100 and 30 characters respectively. Their `FromName`
factories keep slug rules inside the value object instead of the entity methods.

The `Product` state machine is:

```text
Draft -> Published -> Unpublished -> Published
									 -> Discontinued
```

`Publish`, `Unpublish(UnpublishReason)`, and `Discontinue` guard invalid transitions with
`DomainException` and return their corresponding domain events. `ProductCreated` and
`ProductDiscontinued` are present under `Domain/Catalog/Products/Events/`; the Application layer
publishes the corresponding integration messages through the message broker. The currently
implemented outbound message contracts include `ProductPublished`, `ProductUnpublished`, and
`ProductDiscontinued` under `Application/Catalog/Products/Messages/`.

## Image handling

`Product` owns active images and limits them to five. `AddImage` appends using the next sort
order and makes the first active image the main image. `SetMainImage` clears the active main
flags before setting the requested image. `RemoveImage` soft-deletes the image, promotes the
first remaining active image when necessary, and compacts sort order.

`ReorderImages(IList<int>)` requires the complete active image ID list and assigns positions
atomically. `Image.SetAsMain`, `Image.ClearMain`, and `Image.SoftDelete` are internal so callers
cannot bypass the aggregate invariant.

## Rules

- Keep `Product.Quantity` out of Catalog; stock quantity belongs to Inventory.
- Keep category hierarchy and visibility out of v1 until a separate UI and ADR exist.
- Keep tag color and visibility out of v1 until a concrete requirement exists.
- Configure explicit string lengths in the Catalog EF model; do not rely on `varchar(max)`.
- Use Catalog-specific slug value objects instead of caller-side length guards.
- Do not add cross-BC navigation properties to Catalog entities.
- Publish cross-BC changes through `IMessageBroker`; do not inject another BC's service.

## Outside this document

- ADR-0007 owns the architectural decision and the v1 scope.
- EF configuration and migration approval follow the repository's Infrastructure and migration
	policies; this sketch does not replace those records.
- Exact controller routes and the completed legacy switch are tracked by the Catalog code and
	the bounded-context map, not by this implementation sketch.
