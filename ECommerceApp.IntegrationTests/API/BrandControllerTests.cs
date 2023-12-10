using ECommerceApp.API;
using ECommerceApp.Application.DTO;
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
    public class BrandControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public BrandControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_brand()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;
            var name = "Samsung";

            var response = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var brand = JsonConvert.DeserializeObject<BrandDto>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            brand.ShouldNotBeNull();
            brand.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 21;

            var response = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_brand_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultBrandVm(0);

            var response = await client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_invalid_brand_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultBrandVm(53);

            var response = await client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_brand_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 2;
            var name = "TestBrand";
            var brand = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<BrandDto>();
            brand.Name = name;

            var response = await client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            var brandUpdated = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<BrandDto>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            brandUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_not_existed_brand_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 223;
            var brand = CreateDefaultBrandVm(id);

            var response = await client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_id_should_delete_brand()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultBrandVm(0);
            var id = await client.Request("api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand)
                .ReceiveJson<int>();

            var response = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var responseAfterUpdate = await client.Request($"api/brands/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            responseAfterUpdate.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_brands_in_db_should_return_brands()
        {
            var client = await _factory.GetAuthenticatedClient();

            var brands = await client.Request($"api/brands")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<List<BrandDto>>();

            brands.Count.ShouldBeGreaterThan(0);
            brands.Count.ShouldBe(3);
        }

        private BrandDto CreateDefaultBrandVm(int id)
        {
            var brand = new BrandDto
            {
                Id = id,
                Name = "BrandTest"
            };
            return brand;
        }
    }
}
