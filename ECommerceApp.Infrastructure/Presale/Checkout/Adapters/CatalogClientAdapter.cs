using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Presale.Checkout.Adapters
{
    internal sealed class CatalogClientAdapter : ICatalogClient
    {
        private readonly IProductService _productService;

        public CatalogClientAdapter(IProductService productService)
        {
            _productService = productService;
        }

        public Task<decimal?> GetUnitPriceAsync(int productId, CancellationToken ct = default)
            => _productService.GetUnitPriceAsync(productId, ct);

        public async Task<CatalogProductPage> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var result = await _productService.GetPublishedProducts(pageSize, pageNo, searchString);

            var items = result.Products
                .Select(p => new CatalogProductItem(p.Id, p.Name, p.Cost, p.CategoryId))
                .ToList();

            return new CatalogProductPage(items, result.Count, result.PageSize, result.CurrentPage, result.SearchString ?? "");
        }

        public async Task<IReadOnlyList<CatalogProductSummary>> GetProductsByIdsAsync(
            IReadOnlyList<int> productIds, CancellationToken ct = default)
        {
            var snapshots = await _productService.GetProductSnapshotsByIdsAsync(productIds, ct);
            return snapshots.Select(s => new CatalogProductSummary(s.Id, s.Name)).ToList();
        }
    }
}
