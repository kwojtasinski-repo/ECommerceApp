using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class ItemControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
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
            var item = CreateItem(0);

            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_item_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItem(1);

            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_item_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var item = CreateItem(0);
            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            var name = "NameChanged";
            var cost = new decimal(199.99);
            item.Name = name;
            item.Cost = cost;
            item.Id = id;

            response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PutJsonAsync(item);

            var itemUpdated = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<ItemDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            itemUpdated.Name.ShouldBe(name);
            itemUpdated.Cost.ShouldBe(cost);
        }

        [Fact]
        public async Task given_invalid_item_when_update_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var customer = CreateItem(189);

            var response = await client.Request("api/items")
                .AllowAnyHttpStatus()
                .PutJsonAsync(customer);

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

        private ItemVm CreateItem(int id)
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
    }
}
