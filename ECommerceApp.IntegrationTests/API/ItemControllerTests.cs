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
    public class ItemControllerTests : IClassFixture<BaseApiTest<Startup>>
    {
        private readonly FlurlClient _client;

        public ItemControllerTests(BaseApiTest<Startup> baseApiTest)
        {
            _client = baseApiTest.Client;
        }

        [Fact]
        public async Task given_valid_id_should_return_item()
        {
            var id = 1;

            var response = await _client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var item = JsonConvert.DeserializeObject<ItemDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            item.ShouldNotBeNull();
            item.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var id = 153;

            var response = await _client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_item_should_add()
        {
            var item = CreateItem(0);

            var response = await _client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_item_should_return_status_code_conflict()
        {
            var item = CreateItem(1);

            var response = await _client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_item_should_update()
        {
            var item = CreateItem(0);
            var response = await _client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            var name = "NameChanged";
            var cost = new decimal(199.99);
            item.Name = name;
            item.Cost = cost;
            item.Id = id;

            response = await _client.Request("api/items")
                .AllowAnyHttpStatus()
                .PutJsonAsync(item);

            var itemUpdated = await _client.Request($"api/items/{id}")
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
            var customer = CreateItem(189);

            var response = await _client.Request("api/items")
                .AllowAnyHttpStatus()
                .PutJsonAsync(customer);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_id_should_delete_item() 
        {
            var item = CreateItem(0);
            var id = await _client.Request("api/items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(item)
                .ReceiveJson<int>();

            var response = await _client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var responseAfterDelete = await _client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            responseAfterDelete.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_page_size_number_and_search_string_when_paginate_should_return_items()
        {
            int pageSize = 20;
            int pageNo = 1; 
            string searchString = "Item";

            var response = await _client.Request($"api/items?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var items = JsonConvert.DeserializeObject<ListForItemVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            items.Count.ShouldBe(3);
            items.Items.Count.ShouldBe(3);
            items.Items.Where(i => i.Name == "Item4").FirstOrDefault().ShouldNotBeNull();
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
