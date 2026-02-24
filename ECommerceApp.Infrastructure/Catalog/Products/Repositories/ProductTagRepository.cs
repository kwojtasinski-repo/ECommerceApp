using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products.Repositories
{
    internal sealed class ProductTagRepository : IProductTagRepository
    {
        private readonly ProductDbContext _context;

        public ProductTagRepository(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<TagId> AddAsync(Tag tag)
        {
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return tag.Id;
        }

        public async Task<Tag> GetByIdAsync(TagId id)
            => await _context.Tags.FirstOrDefaultAsync(t => t.Id == id);

        public async Task<Tag> GetBySlugAsync(TagSlug slug)
            => await _context.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug);

        public async Task UpdateAsync(Tag tag)
        {
            _context.Tags.Update(tag);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(TagId id)
        {
            var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Id == id);
            if (tag is null)
                return false;
            _context.Tags.Remove(tag);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<List<Tag>> GetAllAsync()
            => await _context.Tags
                .AsNoTracking()
                .OrderBy(t => t.Name.Value)
                .ToListAsync();

        public async Task<List<Tag>> SearchByNameAsync(string query, int maxResults)
            => await _context.Tags
                .AsNoTracking()
                .Where(t => t.Name.Value.StartsWith(query))
                .OrderBy(t => t.Name.Value)
                .Take(maxResults)
                .ToListAsync();

        public async Task<List<Tag>> GetByIdsAsync(IEnumerable<int> ids)
            => await _context.Tags
                .Where(t => ids.Contains(t.Id.Value))
                .ToListAsync();

        public async Task<Tag> GetOrCreateAsync(string name)
        {
            var slug = TagSlug.FromName(name);
            var existing = await _context.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
            if (existing is not null)
                return existing;

            var tag = Tag.Create(name);
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();
            return tag;
        }
    }
}
