using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Catalog.Products
{
    public interface IProductRepository
    {
        Task<ProductId> AddAsync(Product product);
        Task<Product> GetByIdAsync(ProductId id);
        Task<Product> GetByIdWithDetailsAsync(ProductId id);
        Task UpdateAsync(Product product);
        Task<bool> DeleteAsync(ProductId id);
        Task<bool> ExistsByIdAsync(ProductId id);
        Task<List<Product>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountAsync(string searchString);
        Task<List<Product>> GetPublishedAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountPublishedAsync(string searchString);
        Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids);
    }
}
