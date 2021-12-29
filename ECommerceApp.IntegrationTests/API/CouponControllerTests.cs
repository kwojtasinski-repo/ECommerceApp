using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Coupon;
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
    public class CouponControllerTests : IClassFixture<BaseApiTest<Startup>>
    {
        private readonly FlurlClient _client;

        public CouponControllerTests(BaseApiTest<Startup> baseApiTest)
        {
            _client = baseApiTest.Client;
        }

        [Fact]
        public async Task given_valid_id_should_return_coupon()
        {
            var id = 1;
            var code = "AGEWEDSGFEW";

            var response = await _client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var brand = JsonConvert.DeserializeObject<CouponDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            brand.ShouldNotBeNull();
            brand.Code.ShouldBe(code);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var id = 21;

            var response = await _client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_coupon_should_add()
        {
            var brand = CreateDefaultCouponVm(0);

            var response = await _client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_invalid_coupon_should_return_status_code_conflict()
        {
            var brand = CreateDefaultCouponVm(53);

            var response = await _client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_valid_coupon_should_update()
        {
            var coupon = CreateDefaultCouponVm(1);
            var code = "TestBrand";
            coupon.Code = code;

            var response = await _client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(coupon);

            var couponUpdated = await _client.Request($"api/coupons/{coupon.Id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<CouponDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            couponUpdated.Code.ShouldBe(code);
        }

        [Fact]
        public async Task given_not_existed_coupon_should_return_status_code_conflict()
        {
            var id = 223;
            var brand = CreateDefaultCouponVm(id);

            var response = await _client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_id_should_delete_coupon()
        {
            var brand = CreateDefaultCouponVm(0);
            var id = await _client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand)
                .ReceiveJson<int>();

            var response = await _client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var responseAfterUpdate = await _client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            responseAfterUpdate.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_brands_in_db_should_return_coupons()
        {
            var brands = await _client.Request($"api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<ListForCouponVm>();

            brands.Count.ShouldBeGreaterThan(0);
        }

        private CouponVm CreateDefaultCouponVm(int id)
        {
            var coupon = new CouponVm
            {
                Id = id,
                Code = "AGEWEDSGFE23526432",
                CouponTypeId = 1,
                Description = "DesciprtionText",
                Discount = 20
            };
            return coupon;
        }
    }
}
