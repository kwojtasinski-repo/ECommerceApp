using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Interfaces
{
    public interface IImageService : IAbstractService<ImageVm, IImageRepository, Image>
    {
        List<ImageVm> GetAll();
        List<ImageVm> GetAll(string searchName);
        List<int> AddImages(AddImagesPOCO imageVm);
        void PartialUpdate(UpdateImagePOCO image);
        List<GetImageVm> GetImagesByItemId(int imageId);
    }
}
