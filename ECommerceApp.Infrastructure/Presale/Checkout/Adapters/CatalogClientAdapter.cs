using ECommerceApp.Application.Catalog.Products.Services;
using ECommerceApp.Application.Presale.Checkout.Contracts;
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
    }
}
