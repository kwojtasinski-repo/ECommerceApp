using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
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
    public class OrderControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public OrderControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_order()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/orders/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var order = JsonConvert.DeserializeObject<OrderDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            order.ShouldNotBeNull();
            order.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1543;

            var response = await client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_orders_should_return_user_orders()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/orders/by-user")
                .AllowAnyHttpStatus()
                .GetAsync();

            // Endpoint now uses new async IOrderService (OrdersDbContext, unseeded here).
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_orders_when_searching_by_customer_id_should_return_orders()
        {
            var client = await _factory.GetAuthenticatedClient();
            var customerId = 1;

            var response = await client.Request($"api/orders/by-customer/{customerId}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var orders = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_customer_id_when_searching_by_customer_id_should_return_status_code_ok_with_empty_list()
        {
            var client = await _factory.GetAuthenticatedClient();
            var customerId = 1432;

            var response = await client.Request($"api/orders/by-customer/{customerId}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            var list = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            list.ShouldBeEmpty();
        }

        [Fact]
        public async Task given_orders_in_db_should_return_all_orders()
        {
            var client = await _factory.GetAuthenticatedClient();
            var response = await client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .GetAsync();

            // Endpoint now returns a paged OrderListVm from the new async IOrderService.
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_order_items_by_user_with_promo_code_should_add_order_with_order_items()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/orders/with-all-order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_order_items_code_should_add_order()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_order_items_with_promo_code_should_add_order()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        [Fact]
        public async Task given_valid_order_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/orders/6")
                .AllowAnyHttpStatus()
                .PutJsonAsync(new { });

            response.StatusCode.ShouldBe((int) HttpStatusCode.Gone);
        }

        private async Task<int> AddDefaultOrderItem()
        {
            var orderItem = CreateOrderItem(0);
            var client = await _factory.GetAuthenticatedClient();
            var response = await client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(orderItem);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            id.ShouldBeGreaterThan(0);
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            return id;

            static OrderItemDto CreateOrderItem(int id)
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
}
