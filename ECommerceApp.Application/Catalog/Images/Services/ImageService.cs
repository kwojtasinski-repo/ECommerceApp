using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    internal sealed class ImageService : IImageService
    {
        private readonly IFileStore _fileStore;
        private readonly IImageRepository _imageRepository;
        private readonly string FILE_DIR = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Upload" + Path.DirectorySeparatorChar + "Files" + Path.DirectorySeparatorChar + Guid.NewGuid().ToString();

        public ImageService(IImageRepository repo, IFileStore fileStore)
        {
            _imageRepository = repo;
            _fileStore = fileStore;
        }

        public int Add(ImageVm objectVm)
        {
            if (objectVm is null)
            {
                throw new BusinessException($"{typeof(ImageVm).Name} cannot be null");
            }

            if (objectVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (objectVm.Images == null || objectVm.Images.Count == 0)
            {
                throw new BusinessException("Adding image without source is not allowed");
            }

            if (objectVm.Images.Count > 1)
            {
                throw new BusinessException("Cannot add more than one images use another method");
            }

            ValidImages(objectVm.Images);
            if (objectVm.ItemId.HasValue)
            {
                ValidImageCount(objectVm.ItemId.Value, 1);
            }

            var imageSrc = objectVm.Images.FirstOrDefault();
            var image = CreateImage(imageSrc, objectVm.ItemId);
            var id = _imageRepository.AddImage(image).GetAwaiter().GetResult();
            return id;
        }

        public bool Delete(int id)
        {
            var image = _imageRepository.GetImageById(id).GetAwaiter().GetResult();
            if (image is null)
            {
                return false;
            }

            var deleted = _imageRepository.DeleteImage(image);
            _fileStore.DeleteFile(image.SourcePath);
            return deleted;
        }

        public GetImageVm Get(int id)
        {
            var image = _imageRepository.GetImageById(id).GetAwaiter().GetResult();

            GetImageVm imageVm = null;

            if (image != null)
            {
                imageVm = new GetImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    ImageSource = Convert.ToBase64String(_fileStore.ReadFile(image.SourcePath))
                };
            }

            return imageVm;
        }

        public List<GetImageVm> GetAll()
        {
            var images = _imageRepository.GetAllImages();

            var imagesVm = new List<GetImageVm>();

            foreach (var image in images)
            {
                var img = new GetImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    ImageSource = Convert.ToBase64String(_fileStore.ReadFile(image.SourcePath))
                };

                imagesVm.Add(img);
            }
            return imagesVm;
        }

        public List<GetImageVm> GetAll(string searchName)
        {
            var images = _imageRepository.GetAllImages().Where(i => i.Name.Contains(searchName)).ToList();

            var imagesVm = new List<GetImageVm>();

            foreach (var image in images)
            {
                var img = new GetImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    ImageSource = Convert.ToBase64String(_fileStore.ReadFile(image.SourcePath))
                };

                imagesVm.Add(img);
            }
            return imagesVm;
        }

        public List<int> AddImages(AddImagesPOCO imageVm)
        {
            if (imageVm is null)
            {
                throw new BusinessException($"{typeof(AddImagesPOCO).Name} cannot be null");
            }

            if (imageVm.Files == null || imageVm.Files.Count == 0)
            {
                throw new BusinessException("Adding image without source is not allowed");
            }

            ValidImages(imageVm.Files);
            if (imageVm.ItemId.HasValue)
            {
                ValidImageCount(imageVm.ItemId.Value, imageVm.Files.Count);
            }

            var images = new List<Image>();

            foreach (var image in imageVm.Files)
            {
                images.Add(CreateImage(image, imageVm.ItemId));
            }

            var ids = _imageRepository.AddImages(images);

            return ids;
        }

        public List<GetImageVm> GetImagesByItemId(int itemId)
        {
            var images = _imageRepository.GetItemImages(itemId);

            var imagesVm = new List<GetImageVm>();
            foreach (var image in images)
            {
                var img = new GetImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    ImageSource = Convert.ToBase64String(_fileStore.ReadFile(image.SourcePath))
                };
                imagesVm.Add(img);
            }

            return imagesVm;
        }

        private void ValidImages(ICollection<IFormFile> images)
        {
            ValidImages(images.Select(i => new ValidateFile(i.FileName, i.Length)));
        }

        private void ValidImages(IEnumerable<ValidateFile> images)
        {
            var errorMessage = new ErrorMessage();

            foreach (var image in images)
            {
                var size = image.Size;
                var fileName = image.Name;

                if (size > ImageConstraints.MaxFileSizeBytes)
                {
                    errorMessage.Message.Append("Image ").Append(fileName).Append(" is too big (").Append(size).Append(" bytes). Allowed ").Append(ImageConstraints.MaxFileSizeBytes).Append("bytes\r\n");
                    errorMessage.ErrorCodes.Add(ErrorCode.Create("fileSizeTooBig", new List<ErrorParameter> { ErrorParameter.Create("name", fileName), ErrorParameter.Create("size", size), ErrorParameter.Create("allowedSize", ImageConstraints.MaxFileSizeBytes) }));
                }

                var extension = _fileStore.GetFileExtenstion(fileName) ?? string.Empty;
                var containsExtension = ImageConstraints.AllowedExtensions.Contains(extension);

                if (!containsExtension)
                {
                    var sb = new StringBuilder();
                    foreach (var ext in ImageConstraints.AllowedExtensions) sb.AppendLine(ext);
                    errorMessage.Message.AppendLine($"Image {fileName} extension {extension} is not allowed. Allowed extensions {sb}");
                    errorMessage.ErrorCodes.Add(ErrorCode.Create("fileExtensionNotAllowed", new List<ErrorParameter> { ErrorParameter.Create("name", fileName), ErrorParameter.Create("extension", extension), ErrorParameter.Create("extensions", sb.ToString()) }));
                }
            }

            if (errorMessage.HasErrors())
            {
                throw new BusinessException(errorMessage);
            }
        }

        private void ValidImageCount(int itemId, int imagesToAdd)
        {
            var imageCount = _imageRepository.GetCountByItemId(itemId);
            var count = imageCount + imagesToAdd;

            if (count > ImageConstraints.MaxImagesPerItem)
            {
                throw new BusinessException($"Cannot add more than {ImageConstraints.MaxImagesPerItem} images. There are already '{imagesToAdd}' images for item with id '{itemId}'", ErrorCode.Create("tooManyImages", new List<ErrorParameter> { ErrorParameter.Create("allowedImagesCount", ImageConstraints.MaxImagesPerItem), ErrorParameter.Create("imageCount", imagesToAdd), ErrorParameter.Create("id", itemId) }));
            }
        }

        private Image CreateImage(IFormFile image, int? itemId)
        {
            var fileDir = _fileStore.WriteFile(image, FILE_DIR);

            return new Image()
            {
                Id = 0,
                ItemId = itemId,
                Name = fileDir.Name,
                SourcePath = fileDir.SourcePath
            };
        }

        private record ValidateFile(string Name, long Size);
    }
}
