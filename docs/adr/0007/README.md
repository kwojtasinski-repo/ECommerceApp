# ADR-0007: Catalog BC — Product, Category, Tag Aggregate Design

**Status**: Accepted
**BC**: Catalog

## What this decision covers
Design of `Product`, `Category`, `Tag` aggregates, image soft-delete, `IImageService`,
and Catalog→other BC name-sync messages.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0007-catalog-bc-product-category-tag-aggregate-design.md | Full design: aggregates, image handling, DB schema | Understanding Catalog BC |
| example-implementation/product-aggregate-usage.md | Product.Create(), Publish/Unpublish, image assignment | Implementing catalog operations |

## Key rules
- Images are soft-deleted — never hard-delete (snapshot URLs remain valid)
- `Product.Quantity` does NOT exist in Catalog — that belongs to Inventory
- Switch complete — legacy Item/Type/Image controllers removed

## Related ADRs
- ADR-0011 (Inventory) — subscribes to ProductPublished/Unpublished
- ADR-0016 (Coupons) — subscribes to ProductNameChanged/CategoryNameChanged/TagNameChanged
