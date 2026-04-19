## Conformance checklist

- [ ] All `Product`, `Category`, `Tag`, `Image` properties use `private set`
- [ ] `Product.Create(...)` is `static`, returns `(Product, ProductCreated)`
- [ ] `Product.cs` has a `private Product()` parameterless constructor for EF Core
- [ ] `Product.cs`, `Category.cs`, `Tag.cs` live under `Domain/Catalog/Products/`
- [ ] No `ICollection<OrderItem>` or other cross-BC navigation in `Product.cs`
- [ ] `Image.SetAsMain()` and `Image.ClearMain()` are `internal` — not `public`
- [ ] `ProductDbContext` uses schema `"catalog"`
- [ ] `ProductService`, `CategoryService`, `ProductTagService` are each `internal sealed`
- [ ] `CategorySlug` enforces max length 100; `TagSlug` enforces max length 30
- [ ] `ProductStatus` enum has four values: `Draft`, `Published`, `Unpublished`, `Discontinued` _(⏸ `Discontinued` deferred)_
- [ ] `UnpublishReason` enum lives under `Domain/Catalog/Products/` _(⏸ deferred)_
- [ ] `Product.Unpublish(UnpublishReason reason)` takes a reason parameter _(⏸ deferred)_
- [ ] `Product.Discontinue()` throws `DomainException` if already `Discontinued` _(⏸ deferred)_
- [ ] `Product.UpdateDetails(...)` returns `ProductDetailsUpdated` domain event — not `void` _(⏸ deferred)_
- [ ] `AddImage`/`SetMainImage` raise `ProductMainImageUpdated` domain event when main image changes _(⏸ deferred)_
- [ ] Integration messages `ProductAdded`, `ProductUpdated`, `ProductMainImageUpdated` live in `Application/Catalog/Products/Messages/` _(⏸ deferred)_
- [ ] `ProductUnpublished` integration message carries `Reason: UnpublishReason` field _(⏸ deferred)_
