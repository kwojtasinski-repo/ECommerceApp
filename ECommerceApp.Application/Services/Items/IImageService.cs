﻿using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Items
{
    public interface IImageService
    {
        int Add(ImageVm objectVm);
        bool Delete(int id);
        GetImageVm Get(int id);
        List<GetImageVm> GetAll();
        List<GetImageVm> GetAll(string searchName);
        List<int> AddImages(AddImagesPOCO imageVm);
        List<int> AddImages(AddImagesWithBase64POCO imageVm);
        List<GetImageVm> GetImagesByItemId(int imageId);
        List<ImageInfoDto> GetImages(IEnumerable<int> enumerable);
        ErrorMessage ValidBase64File(IEnumerable<ValidBase64File> base64Files);
    }
}
