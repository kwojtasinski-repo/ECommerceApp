using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly IGenericRepository<Image> _genericRepository;

        public ImageRepository(IGenericRepository<Image> genericRepository)
        {
            _genericRepository = genericRepository;
        }

        public async Task<int> AddImage(Image image)
        {
            var id = await _genericRepository.AddAsync(image);
            return id;
        }

        public List<int> AddImages(List<Image> images)
        {
            return _genericRepository.AddRange(images);
        }

        public bool DeleteImage(Image image)
        {
            return _genericRepository.Delete(image);
        }

        public async Task DeleteImage(int imageId)
        {
            await _genericRepository.DeleteAsync(imageId);
        }

        public IQueryable<Image> GetAllImages()
        {
            var query = _genericRepository.GetAll();
            return query;
        }

        public async Task<Image> GetImageById(int imageId)
        {
            var image = await _genericRepository.GetByIdAsync(imageId);
            return image;
        }
    }
}
