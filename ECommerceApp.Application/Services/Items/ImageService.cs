using ECommerceApp.Application.DTO;
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

namespace ECommerceApp.Application.Services.Items
{
    public class ImageService : IImageService
    {
        private readonly IFileStore _fileStore;
        private readonly IImageRepository _imageRepository;
        private readonly int ALLOWED_SIZE = 10 * 1024 * 1024; // 10 mb
        private readonly List<string> IMAGE_EXTENSION_PARAMETERS = new () { ".jpg", ".png" }; // extensions
        private readonly string FILE_DIR = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Upload" + Path.DirectorySeparatorChar + "Files" + Path.DirectorySeparatorChar + Guid.NewGuid().ToString();
        private readonly int ALLOWED_IMAGES_COUNT = 5;

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

        public List<ImageInfoDto> GetImages(IEnumerable<int> imagesId)
        {
            return _imageRepository.GetImagesByItemsId(imagesId)
                                   .Select(i => new ImageInfoDto
                                   {
                                       Id = i.Id,
                                       Name = i.Name,
                                       ItemId = i.ItemId
                                   })
                                   .ToList();
        }

        public List<int> AddImages(AddImagesWithBase64POCO dto)
        {
            if (dto is null)
            {
                throw new BusinessException($"{typeof(AddImagesPOCO).Name} cannot be null");
            }

            if (dto.FilesWithBase64Format == null || !dto.FilesWithBase64Format.Any())
            {
                throw new BusinessException("Adding image without source is not allowed");
            }

            var files = dto.FilesWithBase64Format.Select(f => new FileWithBytes(f.Name, Convert.FromBase64String(f.FileSource)));
            ValidImages(files);
            if (dto.ItemId.HasValue)
            {
                ValidImageCount(dto.ItemId.Value, files.Count());
            }

            var images = new List<Image>();

            foreach (var image in files)
            {
                var fileDir = _fileStore.WriteFile(image.Name, image.FileSource, FILE_DIR);

                var img = new Image()
                {
                    Id = 0,
                    ItemId = dto.ItemId,
                    Name = fileDir.Name,
                    SourcePath = fileDir.SourcePath
                };

                images.Add(img);
            }

            var ids = _imageRepository.AddImages(images);

            return ids;
        }

        public string ValidBase64File(IEnumerable<ValidBase64File> base64Files)
        {
            if (base64Files is null || !base64Files.Any())
            {
                return string.Empty;
            }

            var errors = new StringBuilder();
            foreach (var file in base64Files)
            {
                if (!IsBase64String(file.FileSource))
                {
                    errors.AppendLine($"Image '{file.Name}' has invalid Base64 string");
                }
            }
            return errors.ToString();
        }

        private void ValidImages(ICollection<IFormFile> images)
        {
            ValidImages(images.Select(i => new ValidateFile(i.FileName, i.Length)));
        }

        private void ValidImages(IEnumerable<FileWithBytes> images)
        {
            ValidImages(images.Select(i => new ValidateFile(i.Name, i.FileSource.LongLength)));
        }

        private void ValidImages(IEnumerable<ValidateFile> images)
        {
            var errors = new StringBuilder();

            // FIRST VALIDATION
            foreach (var image in images)
            {
                var size = image.Size;
                var fileName = image.Name;

                if (size > ALLOWED_SIZE)
                {
                    errors.Append("Image ").Append(fileName).Append(" is too big (").Append(size).Append(" bytes). Allowed ").Append(ALLOWED_SIZE).Append("bytes\r\n");
                }

                var extension = _fileStore.GetFileExtenstion(fileName);
                var containsExtension = IMAGE_EXTENSION_PARAMETERS.Contains(extension);

                if (!containsExtension)
                {
                    var sb = new StringBuilder();
                    IMAGE_EXTENSION_PARAMETERS.ForEach(i => sb.AppendLine(i));
                    errors.AppendLine($"Image {fileName} extension {extension} is not allowed. Allowed extensions {sb}");
                }
            }

            // ERRORS OCCUERD
            if (errors.Length > 0)
            {
                throw new BusinessException(errors.ToString());
            }
        }

        private void ValidImageCount(int itemId, int imagesToAdd)
        {
            var imageCount = _imageRepository.GetCountByItemId(itemId);
            var count = imageCount + imagesToAdd;

            if (count >= ALLOWED_IMAGES_COUNT)
            {
                throw new BusinessException($"Cannot add more than {ALLOWED_IMAGES_COUNT} images. There are already '{imagesToAdd}' images for item with id '{itemId}'");
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

        private static bool IsBase64String(string base64)
        {
            Span<byte> buffer = new(new byte[base64.Length]);
            return Convert.TryFromBase64String(base64, buffer, out int bytesParsed);
        }

        private record FileWithBytes(string Name, byte[] FileSource);
        private record ValidateFile(string Name, long Size);
    }
}
