using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Application.Exceptions;
using System.Text;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using System.Linq;
using ECommerceApp.Application.POCO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Application.Services
{
    public abstract class ImageServiceAbstract : IBaseService<ImageVm>
    {
        private readonly IAbstractRepository<Image> _repo;
        private readonly IFileStore _fileStore;
        protected readonly int ALLOWED_SIZE = 10 * 1024 * 1024; // 10 mb
        protected readonly List<string> IMAGE_EXTENSION_PARAMETERS = new List<string> { ".jpg", ".png" }; // extensions
        protected readonly string FILE_DIR = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "Upload" + Path.DirectorySeparatorChar + "Files" + Path.DirectorySeparatorChar + Guid.NewGuid().ToString();
        protected readonly int ALLOWED_IMAGES_COUNT = 5;

        public ImageServiceAbstract(IAbstractRepository<Image> repo, IFileStore fileStore)
        {
            _repo = repo;
            _fileStore = fileStore;
        }

        public int Add(ImageVm objectVm)
        {
            if (objectVm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            if (objectVm.Images == null || (objectVm.Images != null && objectVm.Images.Count == 0))
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
                var imageCount = _repo.GetAll().Where(im => im.ItemId == objectVm.ItemId.Value).AsNoTracking().Select(i=>i.Id).ToList().Count;
                var count = imageCount + 1;

                if (count >= ALLOWED_IMAGES_COUNT)
                {
                    throw new BusinessException($"Cannot add more than {ALLOWED_IMAGES_COUNT} images. There is already {imageCount} images for item id {objectVm.ItemId.Value}");
                }
            }

            var imageSrc = objectVm.Images.FirstOrDefault();

            var image = new Domain.Model.Image()
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

        public void Delete(int id)
        {
            var image = _repo.GetById(id);
            if (image != null)
            {
                _repo.Delete(image);
                _fileStore.DeleteFile(image.SourcePath);
            }
        }

        public ImageVm Get(int id)
        {
            var image = _repo.GetById(id);

            ImageVm imageVm = null;

            if(image != null)
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

        public System.Collections.Generic.List<ImageVm> GetAll()
        {
            var images = _repo.GetAll().ToList();

            var imagesVm = new List<ImageVm>();

            foreach(var image in images)
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

        public System.Collections.Generic.List<ImageVm> GetAll(string searchName)
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

        public void Update(ImageVm objectVm)
        {
            var image = Get(objectVm.Id);

            if(image.SourcePath != objectVm.SourcePath)
            {
                throw new BusinessException("Cannot update source path, contact with admin");
            }

            var img = new Domain.Model.Image()
            {
                Id = objectVm.Id,
                ItemId = objectVm.ItemId,
                SourcePath = objectVm.SourcePath,
                Name = objectVm.Name
            };

            _repo.Update(img);
        }

        protected void ValidImages(ICollection<IFormFile> images)
        {
            var errors = new StringBuilder();

            // FIRST VALIDATION
            foreach (var carImage in images)
            {
                var size = carImage.Length;
                var fileName = carImage.FileName;

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

        public abstract List<int> AddImages(AddImagesPOCO image);
        public abstract void PartialUpdate(UpdateImagePOCO image);
        public abstract List<GetImageVm> GetImagesByItemId(int imageId);
    }
}