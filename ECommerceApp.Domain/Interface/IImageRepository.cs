using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Domain.Interface
{
    public interface IImageRepository : IGenericRepository<Image>
    {
        Task DeleteImage(int imageId);
        Task<int> AddImage(Image image);
        Task<Image> GetImageById(int imageId);
        IQueryable<Image> GetAllImages();
    }
}
