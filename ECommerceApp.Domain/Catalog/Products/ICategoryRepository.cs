using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Catalog.Products
{
    public interface ICategoryRepository
    {
        Task<CategoryId> AddAsync(Category category);
        Task<Category> GetByIdAsync(CategoryId id);
        Task<Category> GetBySlugAsync(CategorySlug slug);
        Task UpdateAsync(Category category);
        Task<bool> DeleteAsync(CategoryId id);
        Task<bool> ExistsByIdAsync(CategoryId id);
        Task<List<Category>> GetAllAsync();
    }
}
