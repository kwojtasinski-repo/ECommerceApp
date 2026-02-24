using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products.Repositories
{
    internal sealed class CategoryRepository : ICategoryRepository
    {
        private readonly ProductDbContext _context;

        public CategoryRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<CategoryId> AddAsync(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category.Id;
        }

        public async Task<Category> GetByIdAsync(CategoryId id)
            => await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);

        public async Task<Category> GetBySlugAsync(Slug slug)
            => await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == slug);

        public async Task UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(CategoryId id)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category is null)
                return false;
            _context.Categories.Remove(category);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsByIdAsync(CategoryId id)
            => await _context.Categories.AnyAsync(c => c.Id == id);

        public async Task<List<Category>> GetAllAsync()
            => await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name.Value)
                .ToListAsync();
    }
}
