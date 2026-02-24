using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Catalog.Products
{
    public interface IProductRepository
    {
        Task<ItemId> AddAsync(Item item);
        Task<Item> GetByIdAsync(ItemId id);
        Task<Item> GetByIdWithDetailsAsync(ItemId id);
        Task UpdateAsync(Item item);
        Task<bool> DeleteAsync(ItemId id);
        Task<bool> ExistsByIdAsync(ItemId id);
        Task<List<Item>> GetAllAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountAsync(string searchString);
        Task<List<Item>> GetPublishedAsync(int pageSize, int pageNo, string searchString);
        Task<int> CountPublishedAsync(string searchString);
        Task<List<Item>> GetByIdsAsync(IEnumerable<int> ids);
    }
}
