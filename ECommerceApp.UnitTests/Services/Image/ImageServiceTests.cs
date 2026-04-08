using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Catalog.Images.ViewModels;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Tests.Services.Image
{
    public class ImageServiceTests
    {
        private readonly Mock<IImageRepository> _imageRepository;
        private readonly Mock<IFileStoreProvider> _fileStoreProvider;
        private readonly Mock<IProductRepository> _productRepository;

        public ImageServiceTests()
        {
            _imageRepository = new Mock<IImageRepository>();
            _fileStoreProvider = new Mock<IFileStoreProvider>();
            _productRepository = new Mock<IProductRepository>();
        }

        [Fact]
        public async Task given_valid_image_should_add()
        {
            var image = CreateImageVm();
            image.Id = 0;
            var product = CreateProductWithImages(0);
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            _fileStoreProvider.Setup(p => p.WriteFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync(new FileDirectoryPOCO { Name = "Name", SourcePath = "/upload/Name" });
            _productRepository.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);
            _productRepository.Setup(p => p.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
            var imageService = CreateService();

            await imageService.Add(image);

            _productRepository.Verify(p => p.UpdateAsync(It.IsAny<Product>()), Times.Once);
        }

        [Fact]
        public async Task given_invalid_image_extension_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".bin");
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(image);

            await action.Should().ThrowExactlyAsync<BusinessException>();
        }

        [Fact]
        public async Task given_too_big_image_extension_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Images = new List<IFormFile>() { AddFileToIFormFile("abcsa2", 41943041) };
            image.Id = 0;
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(image);

            await action.Should().ThrowExactlyAsync<BusinessException>();
        }

        [Fact]
        public async Task given_too_many_images_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            image.Images.Add(AddFileToIFormFile("acs"));
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(image);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .WithMessage("Cannot add more than one images use another method");
        }

        [Fact]
        public async Task given_invalid_image_should_throw_an_exception()
        {
            var image = CreateImageVm();
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(image);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public async Task given_valid_image_with_invalid_files_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            image.Images = null;
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(image);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .WithMessage("Adding image without source is not allowed");
        }

        [Fact]
        public async Task given_file_when_limit_exceeded_should_throw_an_exception()
        {
            var image = CreateImageVm();
            image.Id = 0;
            var product = CreateProductWithImages(5);
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            _productRepository.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(image);

            await action.Should().ThrowExactlyAsync<BusinessException>();
        }

        [Fact]
        public async Task given_valid_images_should_add()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2") }, ItemId = itemId };
            var product = CreateProductWithImages(0);
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            _fileStoreProvider.Setup(p => p.WriteFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync(new FileDirectoryPOCO { Name = "Name", SourcePath = "/upload/Name" });
            _productRepository.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);
            _productRepository.Setup(p => p.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
            var imageService = CreateService();

            await imageService.AddImages(images);

            _productRepository.Verify(p => p.UpdateAsync(It.IsAny<Product>()), Times.Once);
        }

        [Fact]
        public async Task given_images_when_limit_exceeded_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2") }, ItemId = itemId };
            var product = CreateProductWithImages(5);
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            _productRepository.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(product);
            var imageService = CreateService();

            Func<Task> action = () => imageService.AddImages(images);

            await action.Should().ThrowExactlyAsync<BusinessException>();
        }

        [Fact]
        public async Task given_valid_images_with_too_large_file_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2", 41943041) }, ItemId = itemId };
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(".jpg");
            var imageService = CreateService();

            Func<Task> action = () => imageService.AddImages(images);

            await action.Should().ThrowExactlyAsync<BusinessException>();
        }

        [Fact]
        public async Task given_images_with_invalid_extension_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile> { AddFileToIFormFile("test1"), AddFileToIFormFile("test2") }, ItemId = itemId };
            var imageService = CreateService();

            Func<Task> action = () => imageService.AddImages(images);

            await action.Should().ThrowExactlyAsync<BusinessException>();
        }

        [Fact]
        public async Task given_invalid_images_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = new List<IFormFile>(), ItemId = itemId };
            var imageService = CreateService();

            Func<Task> action = () => imageService.AddImages(images);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .WithMessage("Adding image without source is not allowed");
        }

        [Fact]
        public async Task given_null_images_should_throw_an_exception()
        {
            int itemId = 1;
            var images = new AddImagesPOCO() { Files = null, ItemId = itemId };
            var imageService = CreateService();

            Func<Task> action = () => imageService.AddImages(images);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .WithMessage("Adding image without source is not allowed");
        }

        [Fact]
        public async Task given_null_image_when_add_should_throw_an_exception()
        {
            var imageService = CreateService();

            Func<Task> action = () => imageService.Add(null);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .Which.Message.Contains("cannot be null");
        }

        [Fact]
        public async Task given_null_image_when_add_images_should_throw_an_exception()
        {
            var imageService = CreateService();

            Func<Task> action = () => imageService.AddImages((AddImagesPOCO)null);

            (await action.Should().ThrowExactlyAsync<BusinessException>())
                .Which.Message.Contains("cannot be null");
        }

        private ImageService CreateService()
            => new ImageService(_imageRepository.Object, _fileStoreProvider.Object, _productRepository.Object);

        private static Product CreateProductWithImages(int imageCount)
        {
            var product = Product.Create("Test Product", 10m, "Description", 1);
            for (int i = 0; i < imageCount; i++)
                product.AddImage($"image{i}.jpg", "/upload", "Local");
            return product;
        }

        private static ImageVm CreateImageVm()
        {
            Random random = new();
            var name = $"Name {random.Next(1, 10)}";

            return new ImageVm
            {
                Id = 1,
                ItemId = 1,
                Name = name,
                SourcePath = $"Path/{random.Next(1, 20)}",
                Images = new List<IFormFile>() { AddFileToIFormFile(name) }
            };
        }

        private static IFormFile AddFileToIFormFile(string fileName, int size = 0)
        {
            var bytes = size == 0 ? Array.Empty<byte>() : new byte[size];
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, stream.Length, ".jpg", fileName)
            {
                Headers = new HeaderDictionary()
            };
        }
    }
}
