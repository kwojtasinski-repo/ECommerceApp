using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Coupon;
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
    public class CouponControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public CouponControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_coupon()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;
            var code = "AGEWEDSGFEW";

            var response = await client.Request($"api/coupons/{id}")
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
            var client = await _factory.GetAuthenticatedClient();
            var id = 21;

            var response = await client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_coupon_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultCouponVm(0);

            var response = await client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_invalid_coupon_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultCouponVm(53);

            var response = await client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_valid_coupon_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var coupon = CreateDefaultCouponVm(1);
            var code = "TestBrand";
            coupon.Code = code;

            var response = await client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(coupon);

            var couponUpdated = await client.Request($"api/coupons/{coupon.Id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<CouponDetailsVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            couponUpdated.Code.ShouldBe(code);
        }

        [Fact]
        public async Task given_not_existed_coupon_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 223;
            var brand = CreateDefaultCouponVm(id);

            var response = await client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_id_should_delete_coupon()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultCouponVm(0);
            var id = await client.Request("api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand)
                .ReceiveJson<int>();

            var response = await client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var responseAfterUpdate = await client.Request($"api/coupons/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            responseAfterUpdate.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_brands_in_db_should_return_coupons()
        {
            var client = await _factory.GetAuthenticatedClient();

            var brands = await client.Request($"api/coupons")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<ListForCouponVm>();

            brands.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_page_size_number_and_search_string_when_paginate_should_return_coupons()
        {
            var client = await _factory.GetAuthenticatedClient();
            int pageSize = 20;
            int pageNo = 1;
            string searchString = "AGEWEDSGFEW";

            var response = await client.Request($"api/coupons?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var coupons = JsonConvert.DeserializeObject<ListForCouponVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            coupons.Count.ShouldBeGreaterThan(0);
            coupons.Coupons.Count.ShouldBeGreaterThan(0);
            coupons.Coupons.Where(c => c.Id == 1).FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public async Task given_page_size_number_and_invalid_search_string_when_paginate_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            int pageSize = 20;
            int pageNo = 1;
            string searchString = "MABC";

            var response = await client.Request($"api/coupons?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
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
