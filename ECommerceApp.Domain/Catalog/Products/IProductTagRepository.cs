using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Catalog.Products
{
    public interface IProductTagRepository
    {
        Task<TagId> AddAsync(Tag tag);
        Task<Tag> GetByIdAsync(TagId id);
        Task<Tag> GetBySlugAsync(TagSlug slug);
        Task UpdateAsync(Tag tag);
        Task<bool> DeleteAsync(TagId id);
        Task<List<Tag>> GetAllAsync();
        Task<List<Tag>> SearchByNameAsync(string query, int maxResults);
        Task<List<Tag>> GetByIdsAsync(IEnumerable<int> ids);
        Task<Tag> GetOrCreateAsync(string name);
    }
}
