using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
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
    }
}
