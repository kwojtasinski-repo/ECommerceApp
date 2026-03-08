using System.Collections.Generic;

namespace ECommerceApp.Application.Presale.Checkout.ViewModels
{
    public sealed record StorefrontProductVm(
        int ProductId,
        string Name,
        decimal Price,
        int CategoryId,
        int AvailableQuantity,
        bool InStock);

    public sealed record StorefrontProductListVm(
        IReadOnlyList<StorefrontProductVm> Items,
        int TotalCount,
        int PageSize,
        int CurrentPage,
        string SearchString);
}
