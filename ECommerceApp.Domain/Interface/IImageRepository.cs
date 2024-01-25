using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Interface
{
    public interface IImageRepository
    {
        Task DeleteImage(int imageId);
        bool DeleteImage(Image image);
        Task<int> AddImage(Image image);
        Task<Image> GetImageById(int imageId);
        IQueryable<Image> GetAllImages();
        List<int> AddImages(List<Image> images);
    }
}
