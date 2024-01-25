using ECommerceApp.API;
using ECommerceApp.Application.FileManager;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Image;
using ECommerceApp.Domain.Interface;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class ImageControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>, IDisposable
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;
        private readonly DirectoryInfo _testData;

        public ImageControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
            _testData = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.GetDirectories().Where(d => d.Name == "TestData").FirstOrDefault();
        }

        [Fact]
        public async Task given_valid_id_should_return_imageAsync()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;
            var name = "image1";

            var response = await client.Request($"api/images/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var image = JsonConvert.DeserializeObject<GetImageVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            image.ShouldNotBeNull();
            image.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_valid_image_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var filePath = _testData.GetFiles().Where(f => f.Name == "apple-iphone-13.jpg").FirstOrDefault().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var image = new AddImagePOCO { File = file, ItemId = 1 };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(image);

            var id = await client.Request($"api/images")
                .PostAsync(multiContent)
                .ReceiveJson<int>();

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_valid_images_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var filePaths = _testData.GetFiles().ToList();
            var formFiles = new List<IFormFile>();
            foreach (var filePath in filePaths)
            {
                var file = await Utilities.CreateIFormFileFrom(filePath.FullName);
                formFiles.Add(file);
            }
            var images = new AddImagesPOCO { ItemId = 1, Files = formFiles };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagesPOCO>(images);

            var ids = await client.Request($"api/images/multi-upload")
                .PostAsync(multiContent)
                .ReceiveJson<List<int>>();

            ids.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_id_image_should_delete()
        {
            var client = await _factory.GetAuthenticatedClient();
            var filePath = _testData.GetFiles().Where(f => f.Name == "redmi-note-10.jpg").FirstOrDefault().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var image = new AddImagePOCO { File = file, ItemId = 1 };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(image);
            var id = await client.Request($"api/images")
                .AllowAnyHttpStatus()
                .PostAsync(multiContent)
                .ReceiveJson<int>();

            var response = await client.Request($"api/images/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var imageDeleted = await client.Request($"api/images/{id}")
                            .WithHeader("content-type", "application/json")
                            .AllowAnyHttpStatus()
                            .GetAsync()
                            .ReceiveJson<GetImageVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            imageDeleted.ShouldBeNull();
        }

        public void Dispose()
        {
            var imageRepository = _factory.Services.GetService(typeof(IImageRepository)) as IImageRepository;
            var fileStore = _factory.Services.GetService(typeof(IFileStore)) as IFileStore;
            var images = imageRepository.GetAllImages().Where(i => !i.SourcePath.Contains(".."));
            
            foreach (var image in images)
            {
                fileStore.DeleteFile(image.SourcePath);
            }

            GC.SuppressFinalize(this);
        }
    }
}
