using ECommerceApp.Application.Abstracts;
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
    public class ImageService : AbstractService<ImageVm, IImageRepository, Image>, IImageService
    {
        private readonly IFileStore _fileStore;
        private readonly int ALLOWED_SIZE = 10 * 1024 * 1024; // 10 mb
        private readonly List<string> IMAGE_EXTENSION_PARAMETERS = new List<string> { ".jpg", ".png" }; // extensions
        private readonly string FILE_DIR = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Upload" + Path.DirectorySeparatorChar + "Files" + Path.DirectorySeparatorChar + Guid.NewGuid().ToString();
        private readonly int ALLOWED_IMAGES_COUNT = 5;

        public ImageService(IImageRepository repo, IFileStore fileStore) : base(repo)
        {
            _fileStore = fileStore;
        }

        public override int Add(ImageVm objectVm)
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
                var imageCount = _repo.GetAll().Where(im => im.ItemId == objectVm.ItemId.Value).AsNoTracking().Select(i => i.Id).ToList().Count;
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

            var id = _repo.Add(image);

            return id;
        }

        public override void Delete(int id)
        {
            var image = _repo.GetById(id);
            if (image != null)
            {
                _repo.Delete(image);
                _fileStore.DeleteFile(image.SourcePath);
            }
        }

        public override ImageVm Get(int id)
        {
            var image = _repo.GetById(id);

            ImageVm imageVm = null;

            if (image != null)
            {
                imageVm = new ImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    SourcePath = image.SourcePath,
                    ImageSource = _fileStore.ReadFile(image.SourcePath)
                };
            }

            return imageVm;
        }

        public List<ImageVm> GetAll()
        {
            var images = _repo.GetAll().ToList();

            var imagesVm = new List<ImageVm>();

            foreach (var image in images)
            {
                var img = new ImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    SourcePath = image.SourcePath,
                    ImageSource = _fileStore.ReadFile(image.SourcePath)
                };

                imagesVm.Add(img);
            }
            return imagesVm;
        }

        public List<ImageVm> GetAll(string searchName)
        {
            var images = _repo.GetAll().Where(i => i.Name.Contains(searchName)).ToList();

            var imagesVm = new List<ImageVm>();

            foreach (var image in images)
            {
                var img = new ImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    SourcePath = image.SourcePath,
                    ImageSource = _fileStore.ReadFile(image.SourcePath)
                };

                imagesVm.Add(img);
            }
            return imagesVm;
        }

        public override void Update(ImageVm objectVm)
        {
            if (objectVm is null)
            {
                throw new BusinessException($"{typeof(ImageVm).Name} cannot be null");
            }

            var image = Get(objectVm.Id);

            if (image.SourcePath != objectVm.SourcePath)
            {
                throw new BusinessException("Cannot update source path, contact with admin");
            }

            var img = new Image()
            {
                Id = objectVm.Id,
                ItemId = objectVm.ItemId,
                SourcePath = objectVm.SourcePath,
                Name = objectVm.Name
            };

            _repo.Update(img);
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
                var imageCount = _repo.GetAll().Where(im => im.ItemId == imageVm.ItemId.Value).AsNoTracking().Select(i => i.Id).ToList().Count;
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

            var ids = _repo.AddRange(images);

            return ids;
        }

        public void PartialUpdate(UpdateImagePOCO image)
        {
            if (image is null)
            {
                throw new BusinessException($"{typeof(UpdateImagePOCO).Name} cannot be null");
            }

            var img = _repo.GetById(image.Id);

            img.ItemId = image.ItemId;
            string name = _fileStore.ReplaceInvalidChars(image.Name);
            img.Name = name;

            _repo.Update(img);
        }
        public List<GetImageVm> GetImagesByItemId(int itemId)
        {
            var images = _repo.GetAll().Where(i => i.ItemId == itemId).ToList();

            var imagesVm = new List<GetImageVm>();
            foreach (var image in images)
            {
                var img = new GetImageVm()
                {
                    Id = image.Id,
                    ItemId = image.ItemId,
                    Name = image.Name,
                    ImageSource = _fileStore.ReadFile(image.SourcePath)
                };
                imagesVm.Add(img);
            }

            return imagesVm;
        }

        private void ValidImages(ICollection<IFormFile> images)
        {
            var errors = new StringBuilder();

            // FIRST VALIDATION
            foreach (var image in images)
            {
                var size = image.Length;
                var fileName = image.FileName;

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
    }
}
