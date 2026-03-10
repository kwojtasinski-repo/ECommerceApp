using ECommerceApp.Application.Catalog.Products.DTOs;
using ECommerceApp.Application.Catalog.Products.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Products.Services
{
    public interface IProductService
    {
        Task<int> AddProduct(CreateProductDto dto);
        Task<bool> UpdateProduct(UpdateProductDto dto);
        Task<bool> DeleteProduct(int id);
        Task<ProductDetailsVm> GetProductDetails(int id, CancellationToken cancellationToken = default);
        Task<ProductListVm> GetAllProducts(int pageSize, int pageNo, string searchString);
        Task<ProductListVm> GetPublishedProducts(int pageSize, int pageNo, string searchString);
        Task PublishProduct(int id);
        Task UnpublishProduct(int id);
        Task<bool> ProductExists(int id);
        Task<decimal?> GetUnitPriceAsync(int id, CancellationToken ct = default);
    }
}
