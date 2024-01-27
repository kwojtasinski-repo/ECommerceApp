using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Interface
{
    public interface IImageRepository
    {
        Task DeleteImage(int imageId);
        bool DeleteImage(Image image);
        Task<int> AddImage(Image image);
        Task<Image> GetImageById(int imageId);
        List<Image> GetAllImages();
        List<Image> GetItemImages(int itemId);
        List<Image> GetImagesByItemsId(IEnumerable<int> imagesId);
        List<int> AddImages(List<Image> images);
        int GetCountByItemId(int? itemId);
    }
}
