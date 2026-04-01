using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Catalog.Images.Services
{
    internal sealed class ImageService : IImageService
    {
        private readonly IFileStore _fileStore;
        private readonly IImageRepository _imageRepository;
        private readonly IProductRepository _productRepository;
        private static readonly string UPLOAD_DIR = Path.Combine(Directory.GetCurrentDirectory(), "Upload", "Images");

        public ImageService(IImageRepository repo, IFileStore fileStore, IProductRepository productRepository)
        {
            _imageRepository = repo;
            _fileStore = fileStore;
            _productRepository = productRepository;
        }

        public async Task<int> Add(ImageVm objectVm)
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

            if (!objectVm.ItemId.HasValue)
            {
                throw new BusinessException("Product id is required for adding images");
            }

            ValidImages(objectVm.Images);

            var product = await _productRepository.GetByIdWithDetailsAsync(new ProductId(objectVm.ItemId.Value)) 
                ?? throw new BusinessException(
                    $"Product with id '{objectVm.ItemId.Value}' was not found",
                    ErrorCode.Create("productNotFound", new List<ErrorParameter> { ErrorParameter.Create("id", objectVm.ItemId.Value) }));

            ValidImageCount(product, 1);

            var fileDir = _fileStore.WriteFile(objectVm.Images.First(), UPLOAD_DIR);
            product.AddImage(fileDir.SourcePath);
            await _productRepository.UpdateAsync(product);

            return product.Images.Last().Id?.Value ?? 0;
        }

        public async Task<bool> Delete(int id)
        {
            var image = await _imageRepository.GetImageById(id);
            if (image is null)
            {
                return false;
            }

            var product = await _productRepository.GetByIdWithDetailsAsync(new ProductId(image.ProductId.Value));
            if (product is null)
            {
                return false;
            }

            if (!product.RemoveImage(id))
            {
                return false;
            }

            await _productRepository.UpdateAsync(product);
            _fileStore.DeleteFile(image.FileName.Value);
            return true;
        }

        public async Task<GetImageVm> Get(int id)
        {
            var image = await _imageRepository.GetImageById(id);
            if (image is null)
            {
                return null;
            }

            return MapToGetImageVm(image);
        }

        public async Task<List<GetImageVm>> GetAll()
            => (await _imageRepository.GetAllImages()).Select(MapToGetImageVm).ToList();

        public async Task<List<GetImageVm>> GetAll(string searchName)
            => (await _imageRepository.GetAllImages())
                .Where(i => Path.GetFileName(i.FileName.Value).Contains(searchName))
                .Select(MapToGetImageVm)
                .ToList();

        public async Task<List<int>> AddImages(AddImagesPOCO imageVm)
        {
            if (imageVm is null)
            {
                throw new BusinessException($"{typeof(AddImagesPOCO).Name} cannot be null");
            }

            if (imageVm.Files == null || imageVm.Files.Count == 0)
            {
                throw new BusinessException("Adding image without source is not allowed");
            }

            if (!imageVm.ItemId.HasValue)
            {
                throw new BusinessException("Product id is required for adding images");
            }

            ValidImages(imageVm.Files);

            var product = await _productRepository.GetByIdWithDetailsAsync(new ProductId(imageVm.ItemId.Value)) 
                ?? throw new BusinessException($"Product with id '{imageVm.ItemId.Value}' was not found",
                    ErrorCode.Create("productNotFound", new List<ErrorParameter> { ErrorParameter.Create("id", imageVm.ItemId.Value) }));

            ValidImageCount(product, imageVm.Files.Count);

            var writtenPaths = new List<string>();
            foreach (var file in imageVm.Files)
            {
                var fileDir = _fileStore.WriteFile(file, UPLOAD_DIR);
                product.AddImage(fileDir.SourcePath);
                writtenPaths.Add(fileDir.SourcePath);
            }

            await _productRepository.UpdateAsync(product);

            return product.Images
                .Where(i => writtenPaths.Contains(i.FileName.Value))
                .Select(i => i.Id?.Value ?? 0)
                .ToList();
        }

        public async Task<List<GetImageVm>> GetImagesByItemId(int itemId)
            => (await _imageRepository.GetProductImages(itemId)).Select(MapToGetImageVm).ToList();

        private GetImageVm MapToGetImageVm(Image image)
            => new GetImageVm
            {
                Id = image.Id.Value,
                ItemId = image.ProductId.Value,
                Name = Path.GetFileName(image.FileName.Value),
                ImageSource = Convert.ToBase64String(_fileStore.ReadFile(image.FileName.Value))
            };

        private void ValidImages(ICollection<IFormFile> images)
            => ValidImages(images.Select(i => new ValidateFile(i.FileName, i.Length)));

        private void ValidImages(IEnumerable<ValidateFile> images)
        {
            var errorMessage = new ErrorMessage();

            foreach (var image in images)
            {
                var size = image.Size;
                var fileName = image.Name;

                if (size > ImageConstraints.MaxFileSizeBytes)
                {
                    errorMessage.Message.Append("Image ").Append(fileName).Append(" is too big (").Append(size)
                        .Append(" bytes). Allowed ").Append(ImageConstraints.MaxFileSizeBytes).Append("bytes\r\n");
                    errorMessage.ErrorCodes.Add(ErrorCode.Create("fileSizeTooBig", new List<ErrorParameter>
                    {
                        ErrorParameter.Create("name", fileName),
                        ErrorParameter.Create("size", size),
                        ErrorParameter.Create("allowedSize", ImageConstraints.MaxFileSizeBytes)
                    }));
                }

                var extension = _fileStore.GetFileExtenstion(fileName) ?? string.Empty;
                if (!ImageConstraints.AllowedExtensions.Contains(extension))
                {
                    var sb = new StringBuilder();
                    foreach (var ext in ImageConstraints.AllowedExtensions)
                    {
                        sb.AppendLine(ext);
                    }

                    errorMessage.Message.AppendLine($"Image {fileName} extension {extension} is not allowed. Allowed extensions {sb}");
                    errorMessage.ErrorCodes.Add(ErrorCode.Create("fileExtensionNotAllowed", new List<ErrorParameter>
                    {
                        ErrorParameter.Create("name", fileName),
                        ErrorParameter.Create("extension", extension),
                        ErrorParameter.Create("extensions", sb.ToString())
                    }));
                }
            }

            if (errorMessage.HasErrors())
            {
                throw new BusinessException(errorMessage);
            }
        }

        private static void ValidImageCount(Product product, int imagesToAdd)
        {
            var count = product.Images.Count + imagesToAdd;
            if (count > ImageConstraints.MaxImagesPerItem)
            {
                throw new BusinessException(
                    $"Cannot add more than {ImageConstraints.MaxImagesPerItem} images. There are already '{product.Images.Count}' images for product with id '{product.Id?.Value}'",
                    ErrorCode.Create("tooManyImages", new List<ErrorParameter>
                    {
                        ErrorParameter.Create("allowedImagesCount", ImageConstraints.MaxImagesPerItem),
                        ErrorParameter.Create("imageCount", product.Images.Count),
                        ErrorParameter.Create("id", product.Id?.Value ?? 0)
                    }));
            }
        }

        private record ValidateFile(string Name, long Size);
    }
}
