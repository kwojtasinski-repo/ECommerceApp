using ECommerceApp.Application.Catalog.Products.Services;
using AwesomeAssertions;
using ECommerceApp.Application.Catalog.Images;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Images
{
    /// <summary>
    /// Deep content validation and re-encoding in <see cref="ImageService"/>. Every upload path
    /// (classic POST, API, chunked, TUS) funnels through it, so the stored bytes are asserted here.
    /// </summary>
    public class ImageSanitizationTests
    {
        private const long BombMemoryBudgetBytes = 100L * 1024 * 1024;

        private readonly Mock<IFileStoreProvider> _fileStoreProvider = new();
        private readonly Mock<IProductRepository> _productRepository = new();
        private readonly List<byte[]> _storedFiles = new();

        [Fact]
        public async Task AddImages_ValidJpeg_ShouldStoreDecodableJpeg()
        {
            var service = CreateService(".jpg");

            await service.AddImages(Upload("a.jpg", ImagePayloads.ValidJpeg(40, 30)));

            using var codec = SKCodec.Create(new MemoryStream(_storedFiles.Should().ContainSingle().Subject));
            codec.Info.Width.Should().Be(40);
            codec.Info.Height.Should().Be(30);
            codec.EncodedFormat.Should().Be(SKEncodedImageFormat.Jpeg);
        }

        [Fact]
        public async Task AddImages_ValidPng_ShouldStoreDecodablePng()
        {
            var service = CreateService(".png");

            await service.AddImages(Upload("a.png", ImagePayloads.ValidPng(40, 30)));

            using var codec = SKCodec.Create(new MemoryStream(_storedFiles.Should().ContainSingle().Subject));
            codec.Info.Width.Should().Be(40);
            codec.Info.Height.Should().Be(30);
            codec.EncodedFormat.Should().Be(SKEncodedImageFormat.Png);
        }

        [Theory]
        [InlineData(1, 40, 20)]
        [InlineData(3, 40, 20)]
        [InlineData(6, 20, 40)]
        [InlineData(8, 20, 40)]
        public async Task AddImages_JpegWithExifOrientation_ShouldStoreUprightPixels(byte orientation, int expectedWidth, int expectedHeight)
        {
            var service = CreateService(".jpg");

            await service.AddImages(Upload("a.jpg", ImagePayloads.JpegWithOrientation(40, 20, orientation)));

            using var codec = SKCodec.Create(new MemoryStream(_storedFiles.Should().ContainSingle().Subject));
            codec.Info.Width.Should().Be(expectedWidth);
            codec.Info.Height.Should().Be(expectedHeight);
            codec.EncodedOrigin.Should().Be(SKEncodedOrigin.TopLeft);
        }

        [Fact]
        public async Task AddImages_JpegWithCommentSegmentCarryingMarker_ShouldNotStoreMarker()
        {
            var service = CreateService(".jpg");

            await service.AddImages(Upload("a.jpg", ImagePayloads.JpegWithCommentSegment("<script>" + ImagePayloads.Marker + "</script>")));

            ImagePayloads.Contains(_storedFiles.Should().ContainSingle().Subject, ImagePayloads.Marker).Should().BeFalse();
        }

        [Fact]
        public async Task AddImages_JpegWithExifSegmentCarryingMarker_ShouldNotStoreMarker()
        {
            var service = CreateService(".jpg");

            await service.AddImages(Upload("a.jpg", ImagePayloads.JpegWithExifSegment(ImagePayloads.Marker)));

            ImagePayloads.Contains(_storedFiles.Should().ContainSingle().Subject, ImagePayloads.Marker).Should().BeFalse();
        }

        [Fact]
        public async Task AddImages_JpegWithPayloadBeforeSecondEoi_ShouldNotStoreMarker()
        {
            var service = CreateService(".jpg");
            var upload = Upload("a.jpg", ImagePayloads.JpegWithPayloadBeforeSecondEoi("<html><script>" + ImagePayloads.Marker + "</script></html>"));

            await service.AddImages(upload);

            ImagePayloads.Contains(_storedFiles.Should().ContainSingle().Subject, ImagePayloads.Marker).Should().BeFalse();
        }

        [Fact]
        public async Task AddImages_PngWithTextChunkCarryingMarker_ShouldNotStoreMarker()
        {
            var service = CreateService(".png");

            await service.AddImages(Upload("a.png", ImagePayloads.PngWithTextChunk(ImagePayloads.Marker)));

            ImagePayloads.Contains(_storedFiles.Should().ContainSingle().Subject, ImagePayloads.Marker).Should().BeFalse();
        }

        [Fact]
        public async Task AddImages_PngWithDataAfterIend_ShouldNotStoreMarker()
        {
            var service = CreateService(".png");
            var upload = Upload("a.png", ImagePayloads.PngWithPayloadBeforeSecondIend("<html><script>" + ImagePayloads.Marker + "</script></html>"));

            await service.AddImages(upload);

            ImagePayloads.Contains(_storedFiles.Should().ContainSingle().Subject, ImagePayloads.Marker).Should().BeFalse();
        }

        [Fact]
        public async Task AddImages_PngDeclaringHugeDimensions_ShouldBeRejectedWithoutAllocatingForThePixels()
        {
            var service = CreateService(".png");
            var upload = Upload("a.png", ImagePayloads.PngDeclaringDimensions(60000, 60000));
            var before = GC.GetTotalAllocatedBytes(precise: false);

            Func<Task> act = () => service.AddImages(upload);

            await act.Should().ThrowAsync<BusinessException>();
            (GC.GetTotalAllocatedBytes(precise: false) - before).Should().BeLessThan(BombMemoryBudgetBytes);
            _storedFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_JpegDeclaringHugeDimensions_ShouldBeRejectedWithoutAllocatingForThePixels()
        {
            var service = CreateService(".jpg");
            var upload = Upload("a.jpg", ImagePayloads.JpegDeclaringDimensions(60000, 60000));
            var before = GC.GetTotalAllocatedBytes(precise: false);

            Func<Task> act = () => service.AddImages(upload);

            await act.Should().ThrowAsync<BusinessException>();
            (GC.GetTotalAllocatedBytes(precise: false) - before).Should().BeLessThan(BombMemoryBudgetBytes);
            _storedFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_PngWithZeroDimensions_ShouldBeRejected()
        {
            var service = CreateService(".png");

            Func<Task> act = () => service.AddImages(Upload("a.png", ImagePayloads.PngDeclaringDimensions(0, 0)));

            await act.Should().ThrowAsync<BusinessException>();
            _storedFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_JpegWithZeroDimensions_ShouldBeRejected()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", ImagePayloads.JpegDeclaringDimensions(0, 0)));

            await act.Should().ThrowAsync<BusinessException>();
            _storedFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_JpegWithGarbledBodyAndValidFraming_ShouldBeRejected()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", ImagePayloads.JpegWithGarbledBody()));

            await act.Should().ThrowAsync<BusinessException>();
            _storedFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_PngWithGarbledBodyAndValidFraming_ShouldBeRejected()
        {
            var service = CreateService(".png");

            Func<Task> act = () => service.AddImages(Upload("a.png", ImagePayloads.PngWithGarbledBody()));

            await act.Should().ThrowAsync<BusinessException>();
            _storedFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task AddImages_RealPngNamedJpg_ShouldBeRejected()
        {
            var service = CreateService(".jpg");

            Func<Task> act = () => service.AddImages(Upload("a.jpg", ImagePayloads.ValidPng()));

            await act.Should().ThrowAsync<BusinessException>();
            _storedFiles.Should().BeEmpty();
        }

        private ImageService CreateService(string extension)
        {
            _fileStoreProvider.Setup(p => p.GetFileExtenstion(It.IsAny<string>(), It.IsAny<string>())).Returns(extension);
            SetupFileStoreCapturingWrittenBytes();
            _productRepository.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<ProductId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Product.Create("Test Product", 10m, "Description", 1));
            _productRepository.Setup(p => p.UpdateAsync(It.IsAny<Product>())).Returns(Task.CompletedTask);
            return new ImageService(new Mock<IImageRepository>().Object, _fileStoreProvider.Object, _productRepository.Object, new ImageSanitizer(Options.Create(new ImageLimitsOptions())), new Mock<IProductCacheInvalidator>().Object);
        }

        private void SetupFileStoreCapturingWrittenBytes()
            => _fileStoreProvider.Setup(p => p.WriteFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .Returns<IFormFile, string>(async (file, _) =>
                {
                    await using var stream = file.OpenReadStream();
                    using var copy = new MemoryStream();
                    await stream.CopyToAsync(copy);
                    _storedFiles.Add(copy.ToArray());
                    return new FileDirectoryPOCO { Name = "Name", SourcePath = "/upload/Name" };
                });

        private static AddImagesPOCO Upload(string name, byte[] content)
        {
            var stream = new MemoryStream(content);
            return new AddImagesPOCO
            {
                ItemId = 1,
                Files = new List<IFormFile> { new FormFile(stream, 0, content.Length, "files", name) { Headers = new HeaderDictionary() } }
            };
        }
    }
}
