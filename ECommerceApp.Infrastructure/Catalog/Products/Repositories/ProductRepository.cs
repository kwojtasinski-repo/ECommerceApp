using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products.Repositories
{
    internal sealed class ProductRepository : IProductRepository
    {
        private readonly CatalogDbContext _context;

        public ProductRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<ProductId> AddAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product.Id;
        }

        public async Task<Product> GetByIdAsync(ProductId id)
            => await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<Product> GetByIdWithDetailsAsync(ProductId id)
            => await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductTags)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(ProductId id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product is null)
                return false;
            _context.Products.Remove(product);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsByIdAsync(ProductId id)
            => await _context.Products.AnyAsync(p => p.Id == id);

        public async Task<List<Product>> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1) pageNo = 1;

            return await _context.Products
                .AsNoTracking()
                .Where(p => string.IsNullOrEmpty(searchString) || p.Description.Value.Contains(searchString))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAsync(string searchString)
            => await _context.Products
                .AsNoTracking()
                .CountAsync(p => string.IsNullOrEmpty(searchString) || p.Description.Value.Contains(searchString));

        public async Task<List<Product>> GetPublishedAsync(int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1) pageNo = 1;

            return await _context.Products
                .AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published
                         && p.Quantity.Value > 0
                         && (string.IsNullOrEmpty(searchString) || p.Description.Value.Contains(searchString)))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountPublishedAsync(string searchString)
            => await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.Status == ProductStatus.Published
                              && p.Quantity.Value > 0
                              && (string.IsNullOrEmpty(searchString) || p.Description.Value.Contains(searchString)));

        public async Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids)
            => await _context.Products
                .Where(p => ids.Contains(p.Id.Value))
                .ToListAsync();
    }
}
