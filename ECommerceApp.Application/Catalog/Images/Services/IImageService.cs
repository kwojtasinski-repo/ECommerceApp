using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    public interface IImageService
    {
        Task<int> Add(ImageVm objectVm);
        Task<bool> Delete(int id);
        Task<GetImageVm> Get(int id);
        Task<List<GetImageVm>> GetAll();
        Task<List<GetImageVm>> GetAll(string searchName);
        Task<List<int>> AddImages(AddImagesPOCO imageVm);
        Task<List<GetImageVm>> GetImagesByItemId(int imageId);
    }
}
