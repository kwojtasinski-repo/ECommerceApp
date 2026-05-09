using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    public interface IStorefrontQueryService
    {
        Task<StorefrontProductListVm> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, int? categoryId = null, CancellationToken ct = default);

        Task<StorefrontProductListVm> GetPublishedProductsByTagAsync(
            int tagId, int pageSize, int pageNo, CancellationToken ct = default);

        Task<StorefrontProductDetailsVm> GetProductDetailsAsync(int productId, CancellationToken ct = default);

        Task<IReadOnlyList<CatalogTagSummary>> GetAllTagsAsync(CancellationToken ct = default);

        Task<CatalogTagSummary> GetTagByIdAsync(int tagId, CancellationToken ct = default);
    }
}
