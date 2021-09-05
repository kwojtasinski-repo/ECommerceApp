using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class ImageService : ImageServiceAbstract
    {
        private readonly IAbstractRepository<Image> _repo;
        private readonly IFileStore _fileStore;

        public ImageService(IAbstractRepository<Image> repo, IFileStore fileStore) : base(repo, fileStore)
        {
            _repo = repo;
            _fileStore = fileStore;
        }

        public override List<int> AddImages(AddImagesPOCO imageVm)
        {
            if (imageVm.Files == null || (imageVm.Files != null && imageVm.Files.Count == 0))
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

            var images = new List<Domain.Model.Image>();

            foreach (var image in imageVm.Files)
            {
                var fileDir = _fileStore.WriteFile(image, FILE_DIR);

                var img = new Domain.Model.Image()
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

        public override void PartialUpdate(UpdateImagePOCO image)
        {
            var img = _repo.GetById(image.Id);

            img.ItemId = image.ItemId;
            string name = _fileStore.ReplaceInvalidChars(image.Name);
            img.Name = name;

            _repo.Update(img);
        }
        public override List<GetImageVm> GetImagesByItemId(int itemId)
        {
            var images = _repo.GetAll().Where(i => i.ItemId == itemId).ToList();

            var imagesVm = new List<GetImageVm>();
            foreach(var image in images)
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
    }
}
