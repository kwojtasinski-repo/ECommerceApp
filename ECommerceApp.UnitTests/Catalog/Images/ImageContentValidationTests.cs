using AwesomeAssertions;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Images
{
    /// <summary>Content (magic bytes / end marker) validation inside <see cref="ImageService"/>, shared by every upload path.</summary>
    public class ImageContentValidationTests
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] PngEnd = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

        private readonly Mock<IFileStoreProvider> _fileStoreProvider = new();
        private readonly Mock<IProductRepository> _productRepository = new();

        [Fact]
        public async Task AddImages_ValidJpeg_ShouldBeAccepted()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", Jpeg()));

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task AddImages_ValidPng_ShouldBeAccepted()
        {
            var service = CreateService(".png");

            Func<Task> act = () => service.AddImages(Upload("a.png", Png()));

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task AddImages_TextBytesNamedJpg_ShouldBeRejected()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>")));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddImages_PngContentNamedJpg_ShouldBeRejected()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", Png()));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddImages_JpegContentNamedPng_ShouldBeRejected()
        {
            var service = CreateService(".png");

            Func<Task> act = () => service.AddImages(Upload("a.png", Jpeg()));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddImages_TruncatedJpeg_ShouldBeRejected()
        {
            var service = CreateService(".jpg");
            var truncated = Jpeg()[..^10];

            Func<Task> act = () => service.AddImages(Upload("a.jpg", truncated));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddImages_TruncatedPng_ShouldBeRejected()
        {
            var service = CreateService(".png");
            var truncated = Png()[..^4];

            Func<Task> act = () => service.AddImages(Upload("a.png", truncated));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddImages_JpegHeaderFollowedByScriptWithoutEndMarker_ShouldBeRejected()
        {
            var service = CreateService(".jpg");
            var polyglot = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0 };
            polyglot.AddRange(Encoding.ASCII.GetBytes("<?php system($_GET['c']); ?>"));

            Func<Task> act = () => service.AddImages(Upload("a.jpg", polyglot.ToArray()));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task AddImages_EmptyFile_ShouldBeRejected()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", Array.Empty<byte>()));

            await act.Should().ThrowAsync<BusinessException>();
        }

        private ImageService CreateService(string extension)
        {
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(extension);
            _fileStoreProvider.Setup(p => p.WriteFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync(new FileDirectoryPOCO { Name = "Name", SourcePath = "/upload/Name" });
            _productRepository.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Product.Create("Test Product", 10m, "Description", 1));
            _productRepository.Setup(p => p.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
            return new ImageService(new Mock<IImageRepository>().Object, _fileStoreProvider.Object, _productRepository.Object);
        }

        private static AddImagesPOCO Upload(string name, byte[] content)
        {
            var stream = new MemoryStream(content);
            return new AddImagesPOCO
            {
                ItemId = 1,
                Files = new List<IFormFile> { new FormFile(stream, 0, content.Length, "files", name) { Headers = new HeaderDictionary() } }
            };
        }

        private static byte[] Jpeg() => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9 };

        private static byte[] Png()
        {
            var bytes = new List<byte>(PngSignature);
            bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE });
            bytes.AddRange(PngEnd);
            return bytes.ToArray();
        }
    }
}
