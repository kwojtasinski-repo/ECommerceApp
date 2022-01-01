using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Order;
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
    public class OrderControllerTests : IClassFixture<BaseApiTest<Startup>>
    {
        private readonly FlurlClient _client;

        public OrderControllerTests(BaseApiTest<Startup> baseApiTest)
        {
            _client = baseApiTest.Client;
        }

        [Fact]
        public async Task given_valid_id_should_return_order()
        {
            var id = 1;

            var response = await _client.Request($"api/orders/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var order = JsonConvert.DeserializeObject<OrderDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            order.ShouldNotBeNull();
            order.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var id = 1543;

            var response = await _client.Request($"api/items/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_orders_should_return_user_orders()
        {
            var response = await _client.Request($"api/orders/by-user")
                .AllowAnyHttpStatus()
                .GetAsync();
            var orders = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_orders_when_searching_by_customer_id_should_return_orders()
        {
            var customerId = 1;

            var response = await _client.Request($"api/orders/by-customer/{customerId}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var orders = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_customer_id_when_searching_by_customer_id_should_return_status_code_not_found()
        {
            var customerId = 1432;

            var response = await _client.Request($"api/orders/by-customer/{customerId}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_orders_in_db_should_return_all_orders()
        {
            var response = await _client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .GetAsync();
            var orders = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_order_items_by_user_with_promo_code_should_add_order_with_order_items()
        {
            var cost = 4500;
            var order = new OrderDto { Id = 0, CurrencyId = 1, CustomerId = 1, PromoCode = "AGEWEDSGFEW" };

            var response = await _client.Request($"api/orders/with-all-order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            var orderAdded = await _client.Request($"api/orders/{id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<OrderDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            orderAdded.ShouldNotBeNull();
            orderAdded.Cost.ShouldBe(cost);
        }

        [Fact]
        public async Task given_order_items_code_should_add_order()
        {
            var cost = 5000;
            var order = new OrderDto { Id = 0, CurrencyId = 1, CustomerId = 1, OrderItems = new List<OrderItemsIdsVm> { new OrderItemsIdsVm { Id = 2 }, new OrderItemsIdsVm { Id = 3 } } };

            var response = await _client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            var orderAdded = await _client.Request($"api/orders/{id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<OrderDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            orderAdded.ShouldNotBeNull();
            orderAdded.Cost.ShouldBe(cost);
        }

        [Fact]
        public async Task given_order_items_with_promo_code_should_add_order()
        {
            var cost = 4500;
            var order = new OrderDto { Id = 0, CurrencyId = 1, CustomerId = 1, OrderItems = new List<OrderItemsIdsVm> { new OrderItemsIdsVm { Id = 2 }, new OrderItemsIdsVm { Id = 3 } }, PromoCode = "AGEWEDSGFEW" };

            var response = await _client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            var orderAdded = await _client.Request($"api/orders/{id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<OrderDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            orderAdded.ShouldNotBeNull();
            orderAdded.Cost.ShouldBe(cost);
        }

        [Fact]
        public async Task given_valid_order_should_update()
        {
            var cost = new decimal(2500);
            var order = new OrderDto { Id = 1, CurrencyId = 1, CustomerId = 1, OrderItems = new List<OrderItemsIdsVm> { new OrderItemsIdsVm { Id = 1} , new OrderItemsIdsVm { Id = 2 }, new OrderItemsIdsVm { Id = 3 } }, PromoCode = "AGEWEDSGFEW" };

            var response = await _client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PutJsonAsync(order);

            var orderAdded = await _client.Request($"api/orders/{order.Id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<OrderDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            orderAdded.ShouldNotBeNull();
            orderAdded.Cost.ShouldBeGreaterThan(cost);
        }
    }
}
