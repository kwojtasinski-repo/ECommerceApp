using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Application.Catalog.Products.Services;
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
        private readonly IFileStoreProvider _fileStoreProvider;
        private readonly IImageRepository _imageRepository;
        private readonly IProductRepository _productRepository;
        private readonly IImageSanitizer _imageSanitizer;
        private readonly IProductCacheInvalidator _productCacheInvalidator;

        public ImageService(IImageRepository repo, IFileStoreProvider fileStoreProvider, IProductRepository productRepository, IImageSanitizer imageSanitizer, IProductCacheInvalidator productCacheInvalidator)
        {
            _productCacheInvalidator = productCacheInvalidator;
            _imageSanitizer = imageSanitizer;
            _imageRepository = repo;
            _fileStoreProvider = fileStoreProvider;
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

            var sanitizedImages = ValidImages(objectVm.Images);

            var product = await _productRepository.GetByIdWithDetailsAsync(new ProductId(objectVm.ItemId.Value)) 
                ?? throw new BusinessException(
                    $"Product with id '{objectVm.ItemId.Value}' was not found",
                    ErrorCode.Create("productNotFound", new List<ErrorParameter> { ErrorParameter.Create("id", objectVm.ItemId.Value) }));

            ValidImageCount(product, 1);

            var fileDir = await _fileStoreProvider.WriteFileAsync(sanitizedImages.First(), ImagesConstants.FileProvider.Local);
            product.AddImage(fileDir.Name, fileDir.SourcePath, ImagesConstants.FileProvider.Local);
            await _productRepository.UpdateAsync(product);
            _productCacheInvalidator.InvalidateDetails(objectVm.ItemId.Value);

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
            _productCacheInvalidator.InvalidateDetails(image.ProductId.Value);
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
                .Where(i => i.FileName.Value.Contains(searchName))
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

            var sanitizedFiles = ValidImages(imageVm.Files);

            var product = await _productRepository.GetByIdWithDetailsAsync(new ProductId(imageVm.ItemId.Value)) 
                ?? throw new BusinessException($"Product with id '{imageVm.ItemId.Value}' was not found",
                    ErrorCode.Create("productNotFound", new List<ErrorParameter> { ErrorParameter.Create("id", imageVm.ItemId.Value) }));

            ValidImageCount(product, imageVm.Files.Count);

            var writtenFileNames = new List<string>();
            foreach (var file in sanitizedFiles)
            {
                var fileDir = await _fileStoreProvider.WriteFileAsync(file, ImagesConstants.FileProvider.Local);
                product.AddImage(fileDir.Name, fileDir.SourcePath, ImagesConstants.FileProvider.Local);
                writtenFileNames.Add(fileDir.Name);
            }

            await _productRepository.UpdateAsync(product);
            _productCacheInvalidator.InvalidateDetails(imageVm.ItemId.Value);

            return product.Images
                .Where(i => writtenFileNames.Contains(i.FileName.Value))
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
                Name = image.FileName.Value,
                ImageSource = Convert.ToBase64String(_fileStoreProvider.ReadFile(image.FileSource, image.Provider))
            };

        private List<IFormFile> ValidImages(ICollection<IFormFile> images)
            => ValidImages(images.Select(i => new ValidateFile(i?.FileName, i?.Length ?? 0, i)));

        /// <summary>
        /// Validates every upload and returns, in the same order, files holding ONLY the re-encoded image.
        /// The original bytes are never stored.
        /// </summary>
        private List<IFormFile> ValidImages(IEnumerable<ValidateFile> images)
        {
            var errorMessage = new ErrorMessage();
            var sanitized = new List<IFormFile>();

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

                var extension = _fileStoreProvider.GetFileExtenstion(fileName, ImagesConstants.FileProvider.Local) ?? string.Empty;
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
                else
                {
                    var result = Sanitize(image.File, extension);
                    if (result.IsAccepted)
                    {
                        sanitized.Add(ToSanitizedFile(image.File, result.Bytes));
                    }
                    else if (result.Rejection == ImageRejection.DimensionsTooLarge)
                    {
                        errorMessage.Message.AppendLine($"Image {fileName} dimensions {result.Width}x{result.Height} exceed the allowed limits.");
                        errorMessage.ErrorCodes.Add(ErrorCode.Create("imageDimensionsTooLarge", new List<ErrorParameter>
                        {
                            ErrorParameter.Create("name", fileName),
                            ErrorParameter.Create("width", result.Width),
                            ErrorParameter.Create("height", result.Height)
                        }));
                    }
                    else
                    {
                        errorMessage.Message.AppendLine($"Image {fileName} content does not match its {extension} extension or is corrupted.");
                        errorMessage.ErrorCodes.Add(ErrorCode.Create("fileContentInvalid", new List<ErrorParameter>
                        {
                            ErrorParameter.Create("name", fileName),
                            ErrorParameter.Create("extension", extension)
                        }));
                    }
                }
            }

            if (errorMessage.HasErrors())
            {
                throw new BusinessException(errorMessage);
            }

            return sanitized;
        }

        private ImageSanitizeResult Sanitize(IFormFile file, string extension)
        {
            if (file is null)
            {
                return ImageSanitizeResult.Rejected(ImageRejection.InvalidContent);
            }

            using var stream = file.OpenReadStream();
            return _imageSanitizer.Sanitize(stream, extension);
        }

        private static IFormFile ToSanitizedFile(IFormFile original, byte[] bytes)
            => new FormFile(new MemoryStream(bytes, writable: false), 0, bytes.Length, original.Name, original.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = original.ContentType
            };

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

        private record ValidateFile(string Name, long Size, IFormFile File);
    }
}
