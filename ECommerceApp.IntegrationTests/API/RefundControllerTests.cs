using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Refund;
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
    public class RefundControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public RefundControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_refund()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/refunds/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            var payment = JsonConvert.DeserializeObject<RefundDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            payment.ShouldNotBeNull();
            payment.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1534534;

            var response = await client.Request($"api/refunds/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_payment_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var refund = new CreateRefundVm() { OrderId = 5, Reason = "ReasonTest" };

            var response = await client.Request("api/refunds")
                .PostJsonAsync(refund);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_payment_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var refund = new CreateRefundVm() { Id = 1, OrderId = 5, Reason = "ReasonTest" };

            var response = await client.Request("api/refunds")
                .AllowAnyHttpStatus()
                .PostJsonAsync(refund);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_payment_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var refund = new CreateRefundVm() { OrderId = 5, Reason = "ReasonTest" };
            var id = await client.Request("api/refunds")
                .PostJsonAsync(refund)
                .ReceiveJson<int>();
            refund.Id = id;
            string reason = "This is reason";
            refund.Reason = reason;

            var response = await client.Request("api/refunds")
                .PutJsonAsync(refund);

            var refundUpdated = await client.Request($"api/refunds/{id}")
                .GetJsonAsync<RefundDetailsVm>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            refundUpdated.ShouldNotBeNull();
            refundUpdated.Reason.ShouldBe(reason);
        }

        [Fact]
        public async Task given_invalid_payment_when_update_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var refund = new CreateRefundVm() { Id = 20423423 };

            var response = await client.Request("api/refunds")
                .AllowAnyHttpStatus()
                .PutJsonAsync(refund);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_search_string_should_return_refunds()
        {
            var client = await _factory.GetAuthenticatedClient();
            int pageSize = 20;
            int pageNo = 1;
            var searchString = "Test";

            var response = await client.Request($"api/refunds?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var refunds = JsonConvert.DeserializeObject<ListForRefundVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            refunds.Count.ShouldBe(1);
            refunds.Refunds.Count.ShouldBe(1);
            refunds.Refunds.Where(r => r.Id == 1).FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public async Task given_invalid_search_string_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            int pageSize = 20;
            int pageNo = 1;
            var searchString = "Tester4235";

            var response = await client.Request($"api/refunds?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }
    }
}
