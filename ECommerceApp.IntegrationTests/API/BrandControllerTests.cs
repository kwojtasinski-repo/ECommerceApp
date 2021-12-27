using ECommerceApp.API;
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
    public class BrandControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly HttpClient _httpClient;

        public BrandControllerTests(CustomWebApplicationFactory<Startup> fixture)
        {
            fixture.Server.PreserveExecutionContext = true;
            _httpClient = fixture.CreateClient();
        }

        [Fact]
        public async Task given_valid_id_should_return_brand()
        {
            var id = 1;
            var name = "Samsung";

            var response = await _httpClient.GetAsync($"api/brands/{id}");
            var brand = JsonConvert.DeserializeObject<BrandVm>(await response.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            brand.ShouldNotBeNull();
            brand.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_valid_id_should_return_brand_with_flurl()
        {
            var id = 1;
            var name = "Samsung";
            using var client = new FlurlClient(_httpClient);

            var response = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var brand = JsonConvert.DeserializeObject<BrandVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            brand.ShouldNotBeNull();
            brand.Name.ShouldBe(name);


            
        }
    }
}
