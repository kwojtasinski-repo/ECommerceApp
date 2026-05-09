using ECommerceApp.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Catalog.Products.Repositories
{
    internal class ImageRepository : IImageRepository
    {
        private readonly ICatalogDbContext _context;

        public ImageRepository(ICatalogDbContext context)
        {
            _context = context;
        }

        public async Task<Image> GetImageById(int imageId)
            => await _context.Images.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == new ImageId(imageId));

        public async Task<List<Image>> GetAllImages()
        {
            return await _context.Images.AsNoTracking().Where(i => !i.IsDeleted).ToListAsync();
        }

        public async Task<List<Image>> GetProductImages(int productId)
        {
            return await _context.Images.AsNoTracking()
                .Where(i => i.ProductId == new ProductId(productId) && !i.IsDeleted)
                .ToListAsync();
        }
    }
}
