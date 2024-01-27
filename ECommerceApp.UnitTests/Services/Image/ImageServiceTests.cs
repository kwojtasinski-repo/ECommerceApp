using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ECommerceApp.Tests.Services.Image
{
    public class ImageServiceTests
    {
        private readonly Mock<IImageRepository> _imageRepository;
        private readonly Mock<IFileStore> _fileStore;

        public ImageServiceTests()
        {
            _imageRepository = new Mock<IImageRepository>();
            _fileStore = new Mock<IFileStore>();
        }

        [Fact]
        public void given_valid_image_should_add()
        {
            var image = CreateImageVm();
            image.Id = 0;
            _imageRepository.Setup(i => i.GetAllImages()).Returns(new List<Domain.Model.Image>());
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".jpg");
            _fileStore.Setup(f => f.WriteFile(It.IsAny<IFormFile>(), It.IsAny<string>())).Returns(new Application.POCO.FileDirectoryPOCO { Name = "Name", SourcePath = "/abc"});
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);
            
            imageService.Add(image);

            _imageRepository.Verify(i => i.AddImage(It.IsAny<Domain.Model.Image>()), Times.Once);
        }

        [Fact]
        public void given_invalid_image_extension_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            _imageRepository.Setup(i => i.GetAllImages()).Returns(new List<Domain.Model.Image>());
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".bin");
            _fileStore.Setup(f => f.WriteFile(It.IsAny<IFormFile>(), It.IsAny<string>())).Returns(new Application.POCO.FileDirectoryPOCO { Name = "Name", SourcePath = "/abc" });
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(image);

            action.Should().ThrowExactly<BusinessException>();
        }

        [Fact]
        public void given_too_big_image_extension_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Images = new List<IFormFile>() { AddFileToIFormFile("abcsa2", 41943041) };
            image.Id = 0;
            _imageRepository.Setup(i => i.GetAllImages()).Returns(new List<Domain.Model.Image>());
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".jpg");
            _fileStore.Setup(f => f.WriteFile(It.IsAny<IFormFile>(), It.IsAny<string>())).Returns(new Application.POCO.FileDirectoryPOCO { Name = "Name", SourcePath = "/abc" });
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(image);

            action.Should().ThrowExactly<BusinessException>();
        }

        [Fact]
        public void given_too_many_images_should_throw_an_exception() 
        {
            var image = CreateImageVm();
            image.Id = 0;
            image.Images.Add(AddFileToIFormFile("acs"));
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(image);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Cannot add more than one images use another method");
        }

        [Fact]
        public void given_invalid_image_should_throw_an_exception()
        {
            var image = CreateImageVm();
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(image);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_image_with_invalid_files_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            image.Images = null;
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(image);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Adding image without source is not allowed");
        }

        [Fact]
        public void given_file_when_limit_exceeded_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            _imageRepository.Setup(i => i.GetCountByItemId(It.IsAny<int>())).Returns(5);
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".jpg");
            _fileStore.Setup(f => f.WriteFile(It.IsAny<IFormFile>(), It.IsAny<string>())).Returns(new Application.POCO.FileDirectoryPOCO { Name = "Name", SourcePath = "/abc" });
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(image);

            action.Should().ThrowExactly<BusinessException>();
        }

        [Fact]
        public void given_valid_images_should_add()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2") }, ItemId = itemId };
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".jpg");
            _fileStore.Setup(f => f.WriteFile(It.IsAny<IFormFile>(), It.IsAny<string>())).Returns(new Application.POCO.FileDirectoryPOCO { Name = "Name", SourcePath = "/abc" });
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            imageService.AddImages(images);

            _imageRepository.Verify(i => i.AddImages(It.IsAny<List<Domain.Model.Image>>()), Times.Once);
        }

        [Fact]
        public void given_images_when_limit_exceeded_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2") }, ItemId = itemId };
            _imageRepository.Setup(i => i.GetCountByItemId(It.IsAny<int>())).Returns(5);
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".jpg");
            _fileStore.Setup(f => f.WriteFile(It.IsAny<IFormFile>(), It.IsAny<string>())).Returns(new Application.POCO.FileDirectoryPOCO { Name = "Name", SourcePath = "/abc" });
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.AddImages(images);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Cannot add more than 5 images. There is already 5 images for item id 1");
        }

        [Fact]
        public void given_valid_images_with_too_large_file_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2", 41943041) }, ItemId = itemId };
            _fileStore.Setup(f => f.GetFileExtenstion(It.IsAny<string>())).Returns(".jpg");
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.AddImages(images);

            action.Should().ThrowExactly<BusinessException>();
        }

        [Fact]
        public void given_images_with_invalid_extension_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2") }, ItemId = itemId };
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.AddImages(images);

            action.Should().ThrowExactly<BusinessException>();
        }

        [Fact]
        public void given_invalid_images_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> {  }, ItemId = itemId };
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.AddImages(images);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Adding image without source is not allowed");
        }

        [Fact]
        public void given_null_images_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = null, ItemId = itemId };
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.AddImages(images);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Adding image without source is not allowed");
        }

        [Fact]
        public void given_null_image_when_add_should_throw_an_exception()
        {
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.Add(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_image_when_add_images_should_throw_an_exception()
        {
            var imageService = new ImageService(_imageRepository.Object, _fileStore.Object);

            Action action = () => imageService.AddImages((AddImagesPOCO)null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private ImageVm CreateImageVm()
        {
            Random random = new Random();
            var name = $"Name {random.Next(1, 10)}";

            var image = new ImageVm
            {
                Id = 1,
                ItemId = 1,
                Name = name,
                SourcePath = $"Path/{random.Next(1, 20)}",
                Images = new List<IFormFile>() { AddFileToIFormFile(name) }
            };

            return image;
        }

        private IFormFile AddFileToIFormFile(string fileName, int size = 0)
        {
            var bytes = size == 0 ? Array.Empty<byte>() : new byte[size]; 
            var stream = new MemoryStream(bytes);
            var formFile = new FormFile(stream, 0, stream.Length, ".jpg", fileName)
            {
                Headers = new HeaderDictionary()
            };
            return formFile;
        }

        private Domain.Model.Image CreateImage()
        {
            var image = new Domain.Model.Image
            {
                Id = 1,
                ItemId = 1,
                Name = "img",
                SourcePath = "../src"
            };
            return image;
        }
    }
}
