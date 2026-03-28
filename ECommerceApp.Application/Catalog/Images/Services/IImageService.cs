using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using System.Collections.Generic;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    public interface IImageService
    {
        int Add(ImageVm objectVm);
        bool Delete(int id);
        GetImageVm Get(int id);
        List<GetImageVm> GetAll();
        List<GetImageVm> GetAll(string searchName);
        List<int> AddImages(AddImagesPOCO imageVm);
        List<GetImageVm> GetImagesByItemId(int imageId);
    }
}
