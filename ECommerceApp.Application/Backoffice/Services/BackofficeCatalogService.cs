using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Catalog.Products.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Backoffice.Services
{
    internal sealed class BackofficeCatalogService : IBackofficeCatalogService
    {
        private readonly IProductService _productService;

        public BackofficeCatalogService(IProductService productService)
        {
            _productService = productService;
        }

        public Task<BackofficeCatalogListVm> GetProductsAsync(int pageSize, int pageNo, string? searchString, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<BackofficeCatalogDetailVm?> GetProductDetailAsync(int productId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
