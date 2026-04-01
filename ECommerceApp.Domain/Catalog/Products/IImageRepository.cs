using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Catalog.Products
{
    public interface IImageRepository
    {
        Task<Image> GetImageById(int imageId);
        Task<List<Image>> GetAllImages();
        Task<List<Image>> GetProductImages(int productId);
    }
}
