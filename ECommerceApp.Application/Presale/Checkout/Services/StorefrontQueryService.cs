using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Inventory.Availability.DTOs;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Services
{
    internal sealed class StorefrontQueryService : IStorefrontQueryService
    {
        private readonly IProductService _products;
        private readonly IStockService _stock;

        public StorefrontQueryService(IProductService products, IStockService stock)
        {
            _products = products;
            _stock = stock;
        }

        public async Task<StorefrontProductListVm> GetPublishedProductsAsync(
            int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var productList = await _products.GetPublishedProducts(pageSize, pageNo, searchString);

            var productIds = productList.Items.Select(p => p.Id).ToList();
            var stockByProductId = new Dictionary<int, StockItemDto>(productIds.Count);
            await foreach (var s in _stock.GetByProductIdsAsync(productIds, ct))
            {
                stockByProductId[s.ProductId] = s;
            }

            var items = productList.Items.Select(p =>
            {
                stockByProductId.TryGetValue(p.Id, out var stock);
                var available = stock?.AvailableQuantity ?? 0;
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
