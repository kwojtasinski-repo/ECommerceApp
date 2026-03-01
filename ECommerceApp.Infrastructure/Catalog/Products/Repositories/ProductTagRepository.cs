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
        private readonly CatalogDbContext _context;

        public ProductTagRepository(CatalogDbContext context)
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
                .OrderBy(t => EF.Property<string>(t, "Name"))
                .ToListAsync();

        public async Task<List<Tag>> SearchByNameAsync(string query, int maxResults)
            => await _context.Tags
                .AsNoTracking()
                .Where(t => EF.Property<string>(t, "Name").StartsWith(query))
                .OrderBy(t => EF.Property<string>(t, "Name"))
                .Take(maxResults)
                .ToListAsync();

        public async Task<List<Tag>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var tagIds = ids.Select(id => new TagId(id)).ToArray();
            return await _context.Tags
                .Where(t => tagIds.Contains(t.Id))
                .ToListAsync();
        }

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

        public async Task<List<TagUsageSummary>> GetUsageSummariesAsync(int maxProductsPerTag)
        {
            var tags = await _context.Tags
                .AsNoTracking()
                .OrderBy(t => EF.Property<string>(t, "Name"))
                .ToListAsync();

            if (tags.Count == 0)
                return new List<TagUsageSummary>();

            var joins = await _context.ProductTags
                .AsNoTracking()
                .Join(_context.Products, pt => pt.ProductId, p => p.Id,
                      (pt, p) => new { pt.TagId, p.Name })
                .ToListAsync();

            var grouped = joins
                .GroupBy(j => j.TagId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return tags.Select(t =>
            {
                var usage = grouped.GetValueOrDefault(t.Id);
                var total = usage?.Count ?? 0;
                var topNames = usage?
                    .Take(maxProductsPerTag)
                    .Select(u => u.Name.Value)
                    .ToList() ?? new List<string>();
                return new TagUsageSummary(t.Id, t.Name.Value, t.Slug.Value, total, topNames);
            }).ToList();
        }
    }
}
