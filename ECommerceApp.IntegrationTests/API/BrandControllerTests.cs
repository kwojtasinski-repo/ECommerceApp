﻿using ECommerceApp.API;
using ECommerceApp.API.Controllers;
using ECommerceApp.Application.ViewModels.Brand;
using ECommerceApp.IntegrationTests.Common;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class BrandControllerTests : IClassFixture<BaseApiTest<Startup>>
    {
        private readonly FlurlClient _client;

        public BrandControllerTests(BaseApiTest<Startup> baseApiTest)
        {
            _client = baseApiTest.Client;
        }

        [Fact]
        public async Task given_valid_id_should_return_brand()
        {
            var id = 1;
            var name = "Samsung";

            var response = await _client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var brand = JsonConvert.DeserializeObject<BrandVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            brand.ShouldNotBeNull();
            brand.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var id = 21;

            var response = await _client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_brand_should_add()
        {
            var brand = CreateDefaultBrandVm(0);

            var response = await _client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_invalid_brand_should_return_status_code_conflict()
        {
            var brand = CreateDefaultBrandVm(53);

            var response = await _client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_brand_should_update()
        {
            var id = 2;
            var name = "TestBrand";
            var brand = await _client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<BrandVm>();
            brand.Name = name;

            var response = await _client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            var brandUpdated = await _client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<BrandVm>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            brandUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_not_existed_brand_should_return_status_code_conflict()
        {
            var id = 223;
            var brand = CreateDefaultBrandVm(id);

            var response = await _client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_id_should_delete_brand()
        {
            var brand = CreateDefaultBrandVm(0);
            var id = await _client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand)
                .ReceiveJson<int>();

            var response = await _client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var responseAfterUpdate = await _client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            responseAfterUpdate.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_brands_in_db_should_return_brands()
        {
            var brands = await _client.Request($"api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<List<BrandVm>>();

            brands.Count.ShouldBeGreaterThan(0);
            brands.Count.ShouldBe(3);
        }

        private BrandVm CreateDefaultBrandVm(int id)
        {
            var brand = new BrandVm
            {
                Id = id,
                Name = "BrandTest"
            };
            return brand;
        }
    }
}
