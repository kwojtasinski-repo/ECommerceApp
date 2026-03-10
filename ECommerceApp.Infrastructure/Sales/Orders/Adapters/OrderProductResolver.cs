using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Sales.Orders.Contracts;
using ECommerceApp.Domain.Sales.Orders;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Sales.Orders.Adapters
{
    internal sealed class OrderProductResolver : IOrderProductResolver
    {
        private readonly IProductService _productService;

        public OrderProductResolver(IProductService productService)
        {
            _productService = productService;
        }

        public async Task<OrderProductSnapshot?> ResolveAsync(int productId, CancellationToken ct = default)
        {
            var product = await _productService.GetProductDetails(productId, ct);

            if (product is null)
            {
                return null;
            }

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)
                ?? product.Images.OrderBy(i => i.SortOrder).FirstOrDefault();

            return new OrderProductSnapshot(product.Name, mainImage?.Url);
        }

        public async Task<IReadOnlyDictionary<int, OrderProductSnapshot>> ResolveAllAsync(
            IReadOnlyList<int> productIds, CancellationToken ct = default)
        {
            var snapshots = await _productService.GetProductSnapshotsByIdsAsync(productIds, ct);
            var result = new Dictionary<int, OrderProductSnapshot>(snapshots.Count);
            foreach (var s in snapshots)
                result[s.Id] = new OrderProductSnapshot(s.Name, s.ImageUrl);
            return result;
        }
    }
}
