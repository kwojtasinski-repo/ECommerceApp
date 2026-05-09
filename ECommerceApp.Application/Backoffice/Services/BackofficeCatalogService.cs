using ECommerceApp.Application.Backoffice.ViewModels;
using ECommerceApp.Application.Catalog.Products.Services;
using System.Linq;
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

        public async Task<BackofficeCatalogListVm> GetProductsAsync(int pageSize, int pageNo, string searchString, CancellationToken ct = default)
        {
            var result = await _productService.GetAllProducts(pageSize, pageNo, searchString ?? string.Empty);
            return new BackofficeCatalogListVm
            {
                Products = result.Products.Select(p => new BackofficeCatalogItemVm
                {
                    Id = p.Id,
                    Name = p.Name,
                    Cost = p.Cost,
                    Status = p.Status,
                    CategoryName = string.Empty // not in ProductForListVm — tracked as follow-up
                }).ToList(),
                CurrentPage = result.CurrentPage,
                PageSize = result.PageSize,
                TotalCount = result.Count,
                SearchString = result.SearchString
            };
        }

        public async Task<BackofficeCatalogDetailVm> GetProductDetailAsync(int productId, CancellationToken ct = default)
        {
            if (!await _productService.ProductExists(productId))
            {
                return null;
            }

            var product = await _productService.GetProductDetails(productId, ct);
            return new BackofficeCatalogDetailVm
            {
                Id = product.Id,
                Name = product.Name,
                Cost = product.Cost,
                Status = product.Status,
                CategoryId = product.CategoryId,
                CategoryName = product.CategoryName
            };
        }
    }
}
