using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Application.ViewModels.OrderItem;
using ECommerceApp.Application.ViewModels.Payment;
using ECommerceApp.Domain.Model;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Newtonsoft.Json;
using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class PaymentControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public PaymentControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_payment()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/payments/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            var payment = JsonConvert.DeserializeObject<PaymentDetailsDto>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            payment.ShouldNotBeNull();
            payment.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 152345;

            var response = await client.Request($"api/payments/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_payment_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var payment = new AddPaymentDto() { CurrencyId = 1, OrderId = 3 };

            var response = await client.Request("api/payments")
                .PostJsonAsync(payment);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_currency_when_pay_for_order_should_return_status_code_bad_request()
        {
            var client = await _factory.GetAuthenticatedClient();
            var orderId = await AddOrder(1);
            var payment = new AddPaymentDto() { OrderId = orderId, CurrencyId = 1000 };

            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .PostJsonAsync(payment);

            response.StatusCode.ShouldBe((int) HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_paid_order_when_pay_for_order_should_return_status_code_bad_request()
        {
            var client = await _factory.GetAuthenticatedClient();
            var payment = new AddPaymentDto() { OrderId = 1, CurrencyId = 1000 };

            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .PostJsonAsync(payment);

            response.StatusCode.ShouldBe((int) HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_invalid_order_when_pay_for_order_should_return_status_code_bad_request()
        {
            var client = await _factory.GetAuthenticatedClient();
            var payment = new AddPaymentDto() { OrderId = 1000 };

            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .PostJsonAsync(payment);

            response.StatusCode.ShouldBe((int) HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_payments_in_db_should_return_payments()
        {
            var client = await _factory.GetAuthenticatedClient();
            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .GetAsync();
            
            var payments = JsonConvert.DeserializeObject<List<PaymentDto>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            payments.Count.ShouldBeGreaterThan(0);
        }

        private async Task<int> AddOrder(int itemId)
        {
            var orderItem = new OrderItem
            {
                ItemId = itemId,
                ItemOrderQuantity = 1
            };
            var client = await _factory.GetAuthenticatedClient();
            var response = await client.Request("api/order-items")
                .AllowAnyHttpStatus()
                .PostJsonAsync(orderItem);
            var orderItemId = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            orderItemId.ShouldBeGreaterThan(0);
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            var order = new OrderDto { Id = 0, CurrencyId = 1, CustomerId = 1, OrderItems = new List<OrderItemDto> { new OrderItemDto { Id = orderItemId } } };
            var responseAddOrder = await client.Request($"api/orders")
                .AllowAnyHttpStatus()
                .PostJsonAsync(order);
            var id = JsonConvert.DeserializeObject<int>(await responseAddOrder.ResponseMessage.Content.ReadAsStringAsync());
            return id;
        }
    }
}
