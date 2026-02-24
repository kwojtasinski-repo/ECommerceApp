using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products.Repositories
{
    internal sealed class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _context;

        public ProductRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<ItemId> AddAsync(Item item)
        {
            _context.Items.Add(item);
            await _context.SaveChangesAsync();
            return item.Id;
        }

        public async Task<Item> GetByIdAsync(ItemId id)
            => await _context.Items.FirstOrDefaultAsync(i => i.Id == id);

        public async Task<Item> GetByIdWithDetailsAsync(ItemId id)
            => await _context.Items
                .Include(i => i.Images)
                .Include(i => i.ItemTags)
                .FirstOrDefaultAsync(i => i.Id == id);

        public async Task UpdateAsync(Item item)
        {
            _context.Items.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(ItemId id)
        {
            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id);
            if (item is null)
                return false;
            _context.Items.Remove(item);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsByIdAsync(ItemId id)
            => await _context.Items.AnyAsync(i => i.Id == id);

        public async Task<List<Item>> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1) pageNo = 1;

            return await _context.Items
                .AsNoTracking()
                .Where(i => string.IsNullOrEmpty(searchString) || i.Description.Value.Contains(searchString))
                .OrderBy(i => i.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAsync(string searchString)
            => await _context.Items
                .AsNoTracking()
                .CountAsync(i => string.IsNullOrEmpty(searchString) || i.Description.Value.Contains(searchString));

        public async Task<List<Item>> GetPublishedAsync(int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1) pageNo = 1;

            return await _context.Items
                .AsNoTracking()
                .Where(i => i.Status == ProductStatus.Published
                         && i.Quantity.Value > 0
                         && (string.IsNullOrEmpty(searchString) || i.Description.Value.Contains(searchString)))
                .OrderBy(i => i.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountPublishedAsync(string searchString)
            => await _context.Items
                .AsNoTracking()
                .CountAsync(i => i.Status == ProductStatus.Published
                              && i.Quantity.Value > 0
                              && (string.IsNullOrEmpty(searchString) || i.Description.Value.Contains(searchString)));

        public async Task<List<Item>> GetByIdsAsync(IEnumerable<int> ids)
            => await _context.Items
                .Where(i => ids.Contains(i.Id.Value))
                .ToListAsync();
    }
}
