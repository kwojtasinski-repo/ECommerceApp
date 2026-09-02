using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products.Repositories
{
    internal sealed class ProductRepository : IProductRepository
    {
        private readonly ICatalogDbContext _context;

        public ProductRepository(ICatalogDbContext context)
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

        public async Task<Product> GetByIdWithDetailsAsync(ProductId id, CancellationToken cancellationToken = default)
            => await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductTags)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public async Task UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(ProductId id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product is null)
            {
                return false;
            }

            _context.Products.Remove(product);
            return (await _context.SaveChangesAsync()) > 0;
        }

        public async Task<bool> ExistsByIdAsync(ProductId id)
            => await _context.Products.AnyAsync(p => p.Id == id);

        public async Task<List<Product>> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            if (pageNo < 1) pageNo = 1;

            var query = ApplySearch(_context.Products.AsNoTracking(), searchString);
            return await query
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountAsync(string searchString)
            => await ApplySearch(_context.Products.AsNoTracking(), searchString).CountAsync();

        public async Task<List<Product>> GetPublishedAsync(int pageSize, int pageNo, string searchString, int? categoryId = null)
        {
            if (pageNo < 1) pageNo = 1;

            var query = _context.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Published);
            if (categoryId.HasValue)
            {
                var typedCategoryId = new CategoryId(categoryId.Value);
                query = query.Where(p => p.CategoryId == typedCategoryId);
            }

            query = ApplySearch(query, searchString);
            return await query
                .Include(p => p.Images)
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountPublishedAsync(string searchString, int? categoryId = null)
        {
            var query = _context.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Published);
            if (categoryId.HasValue)
            {
                var typedCategoryId = new CategoryId(categoryId.Value);
                query = query.Where(p => p.CategoryId == typedCategoryId);
            }

            return await ApplySearch(query, searchString).CountAsync();
        }

        /// <summary>
        /// <see cref="Product.Name"/> is mapped with <c>HasConversion</c> (a plain scalar column);
        /// <see cref="Product.Description"/> is mapped with <c>OwnsOne</c> (an owned-type navigation with
        /// its own shadow property). EF Core cannot translate a single boolean expression that OR-combines
        /// a predicate over each — confirmed empirically on SQL Server (<c>InvalidOperationException</c>:
        /// "could not be translated"), regardless of whether it's written as <c>.Contains()</c> or
        /// <c>EF.Functions.Like()</c>. Two separately-mapped queries unioned server-side avoids the mixed
        /// expression tree entirely: each side only ever touches one of the two mapping styles.
        /// </summary>
        private static IQueryable<Product> ApplySearch(IQueryable<Product> query, string searchString)
        {
            if (string.IsNullOrEmpty(searchString))
            {
                return query;
            }

            var pattern = "%" + searchString + "%";
            var byName = query.Where(p => EF.Functions.Like(p.Name.Value, pattern));
            var byDescription = query.Where(p => EF.Functions.Like(p.Description.Value, pattern));
            return byName.Union(byDescription);
        }

        public async Task<List<Product>> GetPublishedByTagAsync(int tagId, int pageSize, int pageNo)
        {
            if (pageNo < 1) pageNo = 1;
            var tagIdTyped = new TagId(tagId);
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Where(p => p.Status == ProductStatus.Published
                         && p.ProductTags.Any(pt => pt.TagId == tagIdTyped))
                .OrderBy(p => p.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountPublishedByTagAsync(int tagId)
        {
            var tagIdTyped = new TagId(tagId);
            return await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.Status == ProductStatus.Published
                              && p.ProductTags.Any(pt => pt.TagId == tagIdTyped));
        }

        public async Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var productIds = ids.Select(id => new ProductId(id)).ToList();
            return await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();
        }

        public async Task<List<Product>> GetByIdsWithImagesAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var productIds = ids.Select(id => new ProductId(id)).ToList();
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Images)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(ct);
        }

        public async Task<decimal?> GetUnitPriceAsync(ProductId id, CancellationToken ct = default)
            => await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id && p.Status == ProductStatus.Published)
                .Select(p => (decimal?)p.Cost.Amount)
                .FirstOrDefaultAsync(ct);
    }
}
