using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class StorefrontQueryService : IStorefrontQueryService
    {
        private readonly IProductService _products;
        private readonly IStockSnapshotRepository _stockSnapshots;

        public StorefrontQueryService(IProductService products, IStockSnapshotRepository stockSnapshots)
        {
            _products = products;
            _stockSnapshots = stockSnapshots;
        }

        public async Task<StorefrontProductListVm> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var productList = await _products.GetPublishedProducts(pageSize, pageNo, searchString);

            var productIds = productList.Products.Select(p => p.Id).ToList();
            var snapshotByProductId = new Dictionary<int, StockSnapshot>(productIds.Count);
            await foreach (var s in _stockSnapshots.GetByProductIdsAsync(productIds, ct))
            {
                snapshotByProductId[s.ProductId.Value] = s;
            }

            var items = productList.Products.Select(p =>
            {
                snapshotByProductId.TryGetValue(p.Id, out var snapshot);
                var available = snapshot?.AvailableQuantity ?? 0;
                return new StorefrontProductVm(p.Id, p.Name, p.Cost, p.CategoryId, available, available > 0);
            }).ToList();

            return new StorefrontProductListVm(
                items,
                productList.Count,
                productList.PageSize,
                productList.CurrentPage,
                productList.SearchString ?? "");
        }
    }
}
