using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class ItemControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>, IDisposable
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public ItemControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_item()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var item = JsonConvert.DeserializeObject<ItemDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            item.ShouldNotBeNull();
            item.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 153;

            var response = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_item_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItemDto();

            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_valid_item_with_images_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItemDto();
            item.Images = new List<AddItemImageDto> { new AddItemImageDto("test.png", "SW1hZ2VTb3VyY2U="), new AddItemImageDto("test2.png", "SW1hZ2VTb3VyY2U=") };

            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
            var imageService = _factory.Services.GetRequiredService<IImageService>();
            var images = imageService.GetImagesByItemId(id);
            images.ShouldNotBeNull();
            images.ShouldNotBeEmpty();
            images.Count.ShouldBe(item.Images.Count());
        }

        [Fact]
        public async Task given_valid_item_should_update()
        {
            var id = await AddDefaultItem();
            var client = await _factory.GetAuthenticatedClient();
            var itemDetails = await client.Request($"api/items/{id}").GetJsonAsync<ItemDetailsDto>();
            var name = "NameChanged";
            var cost = 199.99M;
            var tagsId = itemDetails.Tags.Select(t => t.Id).ToList();
            tagsId.Remove(5);
            tagsId.Add(6);
            tagsId.Add(7);
            var images = itemDetails.Images.Select(i => new UpdateItemImageDto(i.Id, i.Name, i.ImageSource)).ToList();
            images.Remove(images.LastOrDefault());
            images.Add(new UpdateItemImageDto(0, "test3.png", "SW1hZ2VTb3VyY2U="));
            var dto = new UpdateItemDto
            {
                Id = id,
                Name = name,
                Cost = cost,
                Quantity = itemDetails.Quantity,
                Description = itemDetails.Description,
                BrandId = itemDetails.Brand.Id,
                TypeId = itemDetails.Type.Id,
                CurrencyId = itemDetails.Currency.Id,
                Warranty = itemDetails.Warranty,
                TagsId = tagsId,
                Images = images,
            };

            var response = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .PutJsonAsync(dto);

            var itemUpdated = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<ItemDetailsDto>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            itemUpdated.Name.ShouldBe(name);
            itemUpdated.Cost.ShouldBe(cost);
            itemUpdated.Tags.ShouldNotBeNull();
            itemUpdated.Tags.ShouldNotBeEmpty();
            itemUpdated.Tags.Count.ShouldBe(dto.TagsId.Count());
            itemUpdated.Images.ShouldNotBeNull();
            itemUpdated.Images.ShouldNotBeEmpty();
            itemUpdated.Images.Count().ShouldBe(dto.Images.Count());
        }

        [Fact]
        public async Task given_invalid_item_when_update_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItem(189);

            var response = await client.Request($"api/items/{item.Id}")
                .AllowAnyHttpStatus()
                .PutJsonAsync(item);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_id_should_delete_item() 
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItem(0);
            var id = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item)
                .ReceiveJson<int>();

            var response = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var responseAfterDelete = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            responseAfterDelete.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_page_size_number_and_search_string_when_paginate_should_return_items()
        {
            var client = await _factory.GetAuthenticatedClient();
            int pageSize = 20;
            int pageNo = 1; 
            string searchString = "Item";

            var response = await client.Request($"api/items?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var items = JsonConvert.DeserializeObject<ListForItemVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            items.Count.ShouldBeGreaterThan(0);
            items.Items.Count.ShouldBeGreaterThan(0);
            items.Items.Where(i => i.Name == "Item4").FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public async Task given_page_size_number_and_invalid_search_string_when_paginate_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            int pageSize = 20;
            int pageNo = 1;
            string searchString = "Abxsat23";

            var response = await client.Request($"api/items?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        private async Task<int> AddDefaultItem()
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItemDto();
            item.Images = new List<AddItemImageDto> { new AddItemImageDto("test.png", "SW1hZ2VTb3VyY2U="), new AddItemImageDto("test2.png", "SW1hZ2VTb3VyY2U=") };
            item.TagsId = new List<int> { 5 };

            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            return JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
        }

        private static AddItemDto CreateItemDto()
        {
            return new AddItemDto
            {
                BrandId = 1,
                CurrencyId = 1,
                TypeId = 1,
                Warranty = "10",
                Name = "Abc",
                Cost = 100M,
                Description = "This is description",
                Quantity = 1,
            };
        }

        private static ItemVm CreateItem(int id)
        {
            var item = new ItemVm
            {
                Id = id,
                BrandId = 1,
                CurrencyId = 1,
                Description = "Opis",
                Cost = new decimal(100),
                Name = "ItemFirst",
                TypeId = 1,
                Quantity = 10
            };
            return item;
        }

        public void Dispose()
        {
            var imageService = _factory.Services.GetService(typeof(IImageService)) as IImageService;
            var images = imageService.GetAll().Where(i => i.ItemId is not null);

            foreach (var image in images)
            {
                imageService.Delete(image.Id);
            }
            GC.SuppressFinalize(this);
        }
    }
}
