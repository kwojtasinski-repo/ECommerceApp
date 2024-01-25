using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        private readonly List<string> IMAGE_EXTENSION_PARAMETERS = new List<string> { ".jpg", ".png" }; // extensions
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

            if (objectVm.Images == null || objectVm.Images != null && objectVm.Images.Count == 0)
            {
                throw new BusinessException("Adding image without source is not allowed");
            }

            if (objectVm != null && objectVm.Images.Count > 1)
            {
                throw new BusinessException("Cannot add more than one images use another method");
            }

            if (objectVm.Images != null && objectVm.Images.Count > 0)
            {
                ValidImages(objectVm.Images);
            }

            if (objectVm.ItemId.HasValue)
            {
                var imageCount = _imageRepository.GetAllImages().Where(im => im.ItemId == objectVm.ItemId.Value).AsNoTracking().Select(i => i.Id).ToList().Count;
                var count = imageCount + 1;

                if (count >= ALLOWED_IMAGES_COUNT)
                {
                    throw new BusinessException($"Cannot add more than {ALLOWED_IMAGES_COUNT} images. There is already {imageCount} images for item id {objectVm.ItemId.Value}");
                }
            }

            var imageSrc = objectVm.Images.FirstOrDefault();

            var image = new Image()
            {
                Id = objectVm.Id,
                ItemId = objectVm.ItemId,
            };

            if (imageSrc != null)
            {
                var fileDir = _fileStore.WriteFile(imageSrc, FILE_DIR);
                image.Name = fileDir.Name;
                image.SourcePath = fileDir.SourcePath;
            }

            var id = _imageRepository.AddImage(image).GetAwaiter().GetResult();

            return id;
        }

        public bool Delete(int id)
        {
            var image = _imageRepository.GetImageById(id).GetAwaiter().GetResult();
            if (image == null)
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
            var images = _imageRepository.GetAllImages().ToList();

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

            if (imageVm.Files == null || imageVm.Files != null && imageVm.Files.Count == 0)
            {
                throw new BusinessException("Adding image without source is not allowed");
            }

            if (imageVm.Files != null && imageVm.Files.Count > 0)
            {
                ValidImages(imageVm.Files);
            }

            if (imageVm.ItemId.HasValue)
            {
                var imageCount = _imageRepository.GetAllImages().Where(im => im.ItemId == imageVm.ItemId.Value).AsNoTracking().Select(i => i.Id).ToList().Count;
                var imagesToAddCount = imageVm.Files.Count;
                var count = imagesToAddCount + imageCount;

                if (count > ALLOWED_IMAGES_COUNT)
                {
                    throw new BusinessException($"Cannot add more than {ALLOWED_IMAGES_COUNT} images. There is already {imageCount} images for item id {imageVm.ItemId.Value}");
                }
            }

            var images = new List<Image>();

            foreach (var image in imageVm.Files)
            {
                var fileDir = _fileStore.WriteFile(image, FILE_DIR);

                var img = new Image()
                {
                    Id = 0,
                    ItemId = imageVm.ItemId,
                    Name = fileDir.Name,
                    SourcePath = fileDir.SourcePath
                };

                images.Add(img);
            }

            var ids = _imageRepository.AddImages(images);

            return ids;
        }

        public List<GetImageVm> GetImagesByItemId(int itemId)
        {
            var images = _imageRepository.GetAllImages().Where(i => i.ItemId == itemId).ToList();

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
            return _imageRepository.GetAllImages()
                        .Where(i => imagesId.Contains(i.Id))
                        .AsNoTracking()
                        .Select(i => new Image
                        {
                            Id = i.Id,
                            Name = i.Name,
                            ItemId = i.ItemId,
                        })
                        .ToList()
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
                var imageCount = _imageRepository.GetAllImages().Where(im => im.ItemId == dto.ItemId.Value).AsNoTracking().Select(i => i.Id).ToList().Count;
                var imagesToAddCount = files.Count();
                var count = imagesToAddCount + imageCount;

                if (count > ALLOWED_IMAGES_COUNT)
                {
                    throw new BusinessException($"Cannot add more than {ALLOWED_IMAGES_COUNT} images. There is already {imageCount} images for item id {dto.ItemId.Value}");
                }
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

        private record FileWithBytes(string Name, byte[] FileSource);
        private record ValidateFile(string Name, long Size);
    }
}
