using ECommerceApp.Application.Presale.Checkout.Contracts;
using System.Collections.Generic;

namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed record StorefrontProductVm(
        int ProductId,
        string Name,
        decimal Price,
        int CategoryId,
        int AvailableQuantity,
        bool InStock,
        string? MainImageUrl);

    public sealed record StorefrontProductListVm(
        IReadOnlyList<StorefrontProductVm> Products,
        int TotalCount,
        int PageSize,
        int CurrentPage,
        string SearchString);

    public sealed record StorefrontProductDetailsVm(
        int ProductId,
        string Name,
        decimal Price,
        string Description,
        string CategoryName,
        IReadOnlyList<CatalogProductImage> Images,
        IReadOnlyList<int> TagIds,
        IReadOnlyList<string> TagNames,
        int AvailableQuantity,
        bool InStock);
}
