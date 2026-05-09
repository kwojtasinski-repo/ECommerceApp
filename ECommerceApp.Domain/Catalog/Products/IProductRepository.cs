using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Catalog.Products
{
    public interface IProductRepository
    {
        Task<ProductId> AddAsync(Product product);
        Task<Product> GetByIdAsync(ProductId id);
        Task<Product> GetByIdWithDetailsAsync(ProductId id, CancellationToken cancellationToken = default);
        Task UpdateAsync(Product product);
        Task<bool> DeleteAsync(ProductId id);
        Task<bool> ExistsByIdAsync(ProductId id);
        Task<List<Product>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountAsync(string searchString);
        Task<List<Product>> GetPublishedAsync(int pageSize, int pageNo, string searchString, int? categoryId = null);
        Task<int> CountPublishedAsync(string searchString, int? categoryId = null);
        Task<List<Product>> GetPublishedByTagAsync(int tagId, int pageSize, int pageNo);
        Task<int> CountPublishedByTagAsync(int tagId);
        Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids);
        Task<List<Product>> GetByIdsWithImagesAsync(IEnumerable<int> ids, CancellationToken ct = default);
        Task<decimal?> GetUnitPriceAsync(ProductId id, CancellationToken ct = default);
    }
}
