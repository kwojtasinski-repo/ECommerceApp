using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
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

        public List<Image> GetAllImages()
        {
            return _genericRepository.GetAll().ToList();
        }

        public int GetCountByItemId(int? itemId)
        {
            return _genericRepository.GetAll()
                                     .AsNoTracking()
                                     .Where(im => itemId.HasValue && im.ItemId == itemId.Value)
                                     .Select(i => i.Id)
                                     .Count();
        }

        public async Task<Image> GetImageById(int imageId)
        {
            var image = await _genericRepository.GetByIdAsync(imageId);
            return image;
        }

        public List<Image> GetImagesByItemsId(IEnumerable<int> imagesId)
        {
            return _genericRepository.GetAll()
                        .Where(i => imagesId.Contains(i.Id))
                        .AsNoTracking()
                        .Select(i => new Image
                        {
                            Id = i.Id,
                            Name = i.Name,
                            ItemId = i.ItemId,
                        })
                        .ToList();
        }

        public List<Image> GetItemImages(int itemId)
        {
            return _genericRepository.GetAll()
                              .Where(i => i.ItemId == itemId)
                              .ToList();
        }
    }
}
