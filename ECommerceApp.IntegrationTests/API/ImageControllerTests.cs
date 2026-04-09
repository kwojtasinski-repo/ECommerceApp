using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ECommerceApp.IntegrationTests.API
{
    public class ImageControllerTests : ApiTestBase<ImageApiTestFactory>, IClassFixture<ImageApiTestFactory>
    {
        private readonly DirectoryInfo _testData;

        public ImageControllerTests(ImageApiTestFactory factory, ITestOutputHelper output)
            : base(factory, output)
        {
            _testData = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.GetDirectories().Where(d => d.Name == "TestData").FirstOrDefault();
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _factory.EnsureSeedCatalogData();
        }

        public override async Task DisposeAsync()
        {
            var imageRepository = _factory.Services.GetService(typeof(IImageRepository)) as IImageRepository;
            var fileStore = _factory.Services.GetService(typeof(IFileStore)) as IFileStore;
            var images = await imageRepository.GetAllImages();

            foreach (var image in images)
            {
                fileStore.DeleteFile(image.FileName.Value);
            }

            GC.SuppressFinalize(this);
            await base.DisposeAsync();
        }

        private async Task<FlurlClient> GetLoggingClient()
        {
            var client = await _factory.GetAuthenticatedClient();
            client.Settings.OnErrorAsync = async call =>
            {
                var body = call.Response?.ResponseMessage?.Content != null
                    ? await call.Response.ResponseMessage.Content.ReadAsStringAsync()
                    : string.Empty;
                _output.WriteLine($"[HTTP {call.Response?.StatusCode}] {call.Request.Url}: {body}");
            };
            return client;
        }

        [Fact]
        public async Task given_valid_id_should_return_imageAsync()
        {
            var client = await GetLoggingClient();
            var filePath = _testData.GetFiles().First().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var addPoco = new AddImagePOCO { File = file, ItemId = _factory.SeededItemId };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(addPoco);
            var id = await client.Request("api/images")
                .PostAsync(multiContent)
                .ReceiveJson<int>();

            var response = await client.Request($"api/images/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var bytes = await response.ResponseMessage.Content.ReadAsByteArrayAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            bytes.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task given_valid_image_should_add()
        {
            var client = await GetLoggingClient();
            var filePath = _testData.GetFiles().Where(f => f.Name == "apple-iphone-13.jpg").FirstOrDefault().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var image = new AddImagePOCO { File = file, ItemId = _factory.SeededItemId };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(image);

            var id = await client.Request($"api/images")
                .PostAsync(multiContent)
                .ReceiveJson<int>();

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_valid_images_should_add()
        {
            var client = await GetLoggingClient();
            var filePaths = _testData.GetFiles().ToList();
            var formFiles = new List<IFormFile>();
            foreach (var filePath in filePaths)
            {
                var file = await Utilities.CreateIFormFileFrom(filePath.FullName);
                formFiles.Add(file);
            }
            var images = new AddImagesPOCO { ItemId = _factory.SeededItemId, Files = formFiles };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagesPOCO>(images);

            var ids = await client.Request($"api/images/multi-upload")
                .PostAsync(multiContent)
                .ReceiveJson<List<int>>();

            ids.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_id_image_should_delete()
        {
            var client = await GetLoggingClient();
            var filePath = _testData.GetFiles().Where(f => f.Name == "redmi-note-10.jpg").FirstOrDefault().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var image = new AddImagePOCO { File = file, ItemId = _factory.SeededItemId };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(image);
            var id = await client.Request($"api/images")
                .AllowHttpStatus(HttpStatusCode.OK, HttpStatusCode.Created)
                .PostAsync(multiContent)
                .ReceiveJson<int>();

            var response = await client.Request($"api/images/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var afterDeleteResponse = await client.Request($"api/images/{id}")
                            .AllowAnyHttpStatus()
                            .GetAsync();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            afterDeleteResponse.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        }
    }
}
