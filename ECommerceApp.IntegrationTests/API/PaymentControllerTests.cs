using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Payment;
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

            var payment = JsonConvert.DeserializeObject<PaymentDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());
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
            var payment = new CreatePayment() { Id = 0, CurrencyId = 1, OrderId = 3 };

            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .PostJsonAsync(payment);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_payment_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var payment = new CreatePayment() { Id = 1 };

            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .PostJsonAsync(payment);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_payments_in_db_should_return_payments()
        {
            var client = await _factory.GetAuthenticatedClient();
            var response = await client.Request("api/payments")
                .AllowAnyHttpStatus()
                .GetAsync();
            
            var payments = JsonConvert.DeserializeObject<List<PaymentVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            payments.Count.ShouldBeGreaterThan(0);
        }
    }
}
