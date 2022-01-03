using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class OrderItemControllerTests : IClassFixture<BaseApiTest<Startup>>
    {
        private readonly FlurlClient _client;

        public OrderItemControllerTests(BaseApiTest<Startup> baseApiTest)
        {
            _client = baseApiTest.Client;
        }

        [Fact]
        public async Task given_valid_id_should_return_order_item()
        {
            var id = 1;

            var response = await _client.Request($"api/order-items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            var orderItem = JsonConvert.DeserializeObject<OrderItemDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orderItem.ShouldNotBeNull();
            orderItem.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var id = 13523;

            var response = await _client.Request($"api/order-items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_order_item_should_add()
        {
            var orderItem = CreateOrderItem(0);
            
            var response = await _client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(orderItem);
            
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            id.ShouldBeGreaterThan(0);
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_invalid_order_item_should_return_status_conflict()
        {
            var orderItem = CreateOrderItem(1);

            var response = await _client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(orderItem);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_order_item_should_update()
        {
            var orderItem = CreateOrderItem(0);
            var id = await _client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(orderItem)
                .ReceiveJson<int>();
            orderItem.Id = id;
            var quantity = 20;
            orderItem.ItemOrderQuantity = quantity;

            var response = await _client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PutJsonAsync(orderItem);

            var orderItemUpdated = await _client.Request($"api/order-items/{id}")
                .AllowAnyHttpStatus()
                .GetJsonAsync<OrderItemDetailsVm>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orderItemUpdated.ItemOrderQuantity.ShouldBe(quantity);
        }

        [Fact]
        public async Task given_invalid_order_item_should_return_status_code_conflict()
        {
            var orderItem = CreateOrderItem(12452);

            var response = await _client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PutJsonAsync(orderItem);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_item_id_should_return_order_items()
        {
            var itemId = 1;

            var response = await _client.Request($"api/order-items/by-items/{itemId}")
                .AllowAnyHttpStatus()
                .GetAsync();

            var orderItems = JsonConvert.DeserializeObject<List<OrderItemForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orderItems.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_item_id_should_return_code_not_found()
        {
            var itemId = 15346;

            var response = await _client.Request($"api/order-items/by-items/{itemId}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_items_added_to_cart_should_return_order_items()
        {
            var response = await _client.Request("api/order-items/by-user")
                .AllowAnyHttpStatus()
                .GetAsync();

            var orderItems = JsonConvert.DeserializeObject<List<OrderItemForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orderItems.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_order_items_in_db_to_should_return_order_items()
        {
            var response = await _client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .GetAsync();

            var orderItems = JsonConvert.DeserializeObject<List<OrderItemForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orderItems.Count.ShouldBeGreaterThan(0);
        }

        private OrderItemDto CreateOrderItem(int id)
        {
            var orderItem = new OrderItemDto
            {
                Id = id,
                ItemId = 1,
                ItemOrderQuantity = 1
            };
            return orderItem;
        }
    }
}
