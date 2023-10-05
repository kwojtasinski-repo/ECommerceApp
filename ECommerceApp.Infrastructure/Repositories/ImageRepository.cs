using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ImageRepository : GenericRepository<Image>, IImageRepository
    {
        public ImageRepository(Context context) : base(context)
        {
        }

        public async Task<int> AddImage(Image image)
        {
            var id = await AddAsync(image);
            return id;
        }

        public async Task DeleteImage(int imageId)
        {
            await DeleteAsync(imageId);
        }

        public IQueryable<Image> GetAllImages()
        {
            var query = GetAll();
            return query;
        }

        public async Task<Image> GetImageById(int imageId)
        {
            var image = await GetByIdAsync(imageId);
            return image;
        }
    }
}
