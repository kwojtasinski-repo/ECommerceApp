using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface IImageService : IAbstractService<ImageVm, IImageRepository, Image>
    {
        System.Collections.Generic.List<ImageVm> GetAll();
        System.Collections.Generic.List<ImageVm> GetAll(string searchName);
        List<int> AddImages(AddImagesPOCO imageVm);
        void PartialUpdate(UpdateImagePOCO image);
        List<GetImageVm> GetImagesByItemId(int imageId);
    }
}
