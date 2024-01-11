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
            var orders = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orders.Count.ShouldBeGreaterThan(0);
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
            var orders = JsonConvert.DeserializeObject<List<OrderForListVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            orders.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_order_items_by_user_with_promo_code_should_add_order_with_order_items()
        {
            var client = await _factory.GetAuthenticatedClient();
            var cost = 13500;
            var order = new AddOrderDto { Id = 0, CustomerId = 1, PromoCode = "AGEWEDSGFEW" };

            var response = await client.Request($"api/orders/with-all-order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            var orderAdded = await client.Request($"api/orders/{id}")
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
            var client = await _factory.GetAuthenticatedClient();
            var cost = 5000;
            var orderItem1 = await AddDefaultOrderItem();
            var orderItem2 = await AddDefaultOrderItem();
            var order = new AddOrderDto { Id = 0, CustomerId = 1, OrderItems = new List<OrderItemsIdsDto> { new OrderItemsIdsDto { Id = orderItem1 }, new OrderItemsIdsDto { Id = orderItem2 } } };

            var response = await client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            var orderAdded = await client.Request($"api/orders/{id}")
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
            var client = await _factory.GetAuthenticatedClient();
            var cost = 4500;
            var orderItem1 = await AddDefaultOrderItem();
            var orderItem2 = await AddDefaultOrderItem();
            var order = new AddOrderDto { Id = 0, CustomerId = 1, OrderItems = new List<OrderItemsIdsDto> { new OrderItemsIdsDto { Id = orderItem2 }, new OrderItemsIdsDto { Id = orderItem1 } }, PromoCode = "AGEWEDSGFEWX" };

            var response = await client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            var orderAdded = await client.Request($"api/orders/{id}")
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
            var client = await _factory.GetAuthenticatedClient();
            var orderCost = 2500M;
            var order = new UpdateOrderDto { 
                Id = 1,
                CustomerId = 1,
                Ordered = DateTime.Now,
                OrderNumber = "ZAM/11/01/2024/1",
                PromoCode = "AGEWEDSGFEW",
                Payment = new PaymentInfoDto { CurrencyId = 1 },
                IsDelivered = true,
                OrderItems = new List<AddOrderItemDto> {
                    new AddOrderItemDto { Id = 1, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { ItemId = 5, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { ItemId = 6, ItemOrderQuantity = 1 },
                    new AddOrderItemDto { ItemId = 1, ItemOrderQuantity = 2 },
                },
            };
            var discount = 10;
            var expectedCost = (1 - (discount/ 100M)) * 12500M;
            var expectedOrderItems = 4;

            var response = await client.Request($"api/orders/{order.Id}")
                .AllowHttpStatus(HttpStatusCode.NoContent)
                .PutJsonAsync(order);

            var orderAdded = await client.Request($"api/orders/{order.Id}")
                .AllowHttpStatus(HttpStatusCode.OK)
                .GetAsync()
                .ReceiveJson<OrderDetailsVm>();
            orderAdded.ShouldNotBeNull();
            orderAdded.Number.ShouldBe(order.OrderNumber);
            orderAdded.Ordered.ShouldBe(order.Ordered.Value);
            orderAdded.Cost.ShouldBeGreaterThan(orderCost);
            orderAdded.Cost.ShouldBe(expectedCost);
            orderAdded.PaymentId.ShouldNotBeNull();
            orderAdded.PaymentId.Value.ShouldBeGreaterThan(0);
            orderAdded.CouponUsedId.HasValue.ShouldBeTrue();
            orderAdded.CouponUsedId.Value.ShouldBeGreaterThan(0);
            orderAdded.IsDelivered.ShouldBeTrue();
            orderAdded.Delivered.HasValue.ShouldBeTrue();
            orderAdded.Delivered.Value.ShouldBeGreaterThan(order.Ordered.Value);
            orderAdded.Delivered.Value.ShouldBeLessThan(DateTime.Now);
            orderAdded.OrderItems.ShouldNotBeEmpty();
            orderAdded.OrderItems.Count.ShouldBe(expectedOrderItems);
            orderAdded.OrderItems.ShouldContain(oi => oi.ItemId == 1);
            orderAdded.OrderItems.ShouldContain(oi => oi.ItemId == 5);
            orderAdded.OrderItems.ShouldContain(oi => oi.ItemId == 6);
            var firstOrderItem = orderAdded.OrderItems.FirstOrDefault(oi => oi.Id == 1);
            firstOrderItem.ShouldNotBeNull();
            firstOrderItem.Id.ShouldBeGreaterThan(0);
            firstOrderItem.ItemCost.ShouldBe(2500);
            firstOrderItem.ItemOrderQuantity.ShouldBe(1);
            var secondOrderItem = orderAdded.OrderItems.FirstOrDefault(oi => oi.ItemId == 5);
            secondOrderItem.ShouldNotBeNull();
            secondOrderItem.Id.ShouldBeGreaterThan(0);
            secondOrderItem.ItemCost.ShouldBe(2500);
            secondOrderItem.ItemOrderQuantity.ShouldBe(1);
            var thirdOrderItem = orderAdded.OrderItems.FirstOrDefault(oi => oi.ItemId == 6);
            thirdOrderItem.ShouldNotBeNull();
            thirdOrderItem.Id.ShouldBeGreaterThan(0);
            thirdOrderItem.ItemCost.ShouldBe(2500);
            thirdOrderItem.ItemOrderQuantity.ShouldBe(1);
            var fourthOrderItem = orderAdded.OrderItems.FirstOrDefault(oi => oi.Id != 1 && oi.ItemId == 1);
            fourthOrderItem.ShouldNotBeNull();
            fourthOrderItem.Id.ShouldBeGreaterThan(0);
            fourthOrderItem.ItemCost.ShouldBe(2500);
            fourthOrderItem.ItemOrderQuantity.ShouldBe(2);
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
