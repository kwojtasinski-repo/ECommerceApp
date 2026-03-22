using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Contracts
{
    public interface ICatalogClient
    {
        Task<decimal?> GetUnitPriceAsync(int productId, CancellationToken ct = default);

        Task<CatalogProductPage> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, CancellationToken ct = default);

        Task<IReadOnlyList<CatalogProductSummary>> GetProductsByIdsAsync(
            IReadOnlyList<int> productIds, CancellationToken ct = default);
    }

    public sealed record CatalogProductSummary(int Id, string Name);

    public sealed record CatalogProductItem(int Id, string Name, decimal Cost, int CategoryId);

    public sealed record CatalogProductPage(
        IReadOnlyList<CatalogProductItem> Products,
        int Count,
        int PageSize,
        int CurrentPage,
        string SearchString);
}
