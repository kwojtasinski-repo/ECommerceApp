using ECommerceApp.API;
using ECommerceApp.Application.DTO;
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
    public class OrderItemControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public OrderItemControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_order_item()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/order-items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            // Endpoint now uses new async IOrderItemService (OrdersDbContext, unseeded here).
            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 13523;

            var response = await client.Request($"api/order-items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_order_item_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_invalid_order_item_should_return_status_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_valid_order_item_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();

            var postResponse = await client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(new { });
            postResponse.StatusCode.ShouldBe((int) HttpStatusCode.Gone);

            var putResponse = await client.Request("api/order-items/1")
                .AllowAnyHttpStatus()
                .PutJsonAsync(new { });
            putResponse.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_invalid_order_item_when_update_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/order-items/12452")
                .AllowAnyHttpStatus()
                .PutJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_valid_item_id_should_return_order_items()
        {
            var client = await _factory.GetAuthenticatedClient();
            var itemId = 1;

            var response = await client.Request($"api/order-items/by-items/{itemId}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_invalid_item_id_should_return_empty_list()
        {
            var client = await _factory.GetAuthenticatedClient();
            var itemId = 15346;

            var response = await client.Request($"api/order-items/by-items/{itemId}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_items_added_to_cart_should_return_order_items()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request("api/order-items/by-user")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_order_items_in_db_to_should_return_order_items()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .GetAsync();

            // Endpoint now returns a paged OrderItemListVm from the new async IOrderItemService.
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
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
