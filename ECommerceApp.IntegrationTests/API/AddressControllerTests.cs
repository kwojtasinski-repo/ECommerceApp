using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Address;
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
    public class AddressControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public AddressControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_address()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;
            var buildingNumber = "2";
            var flatNumber = 10;
            var city = "Nowa Sól";

            var response = await client.Request($"api/addresses/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var address = JsonConvert.DeserializeObject<AddressVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            address.ShouldNotBeNull();
            address.BuildingNumber.ShouldBe(buildingNumber);
            address.FlatNumber.ShouldBe(flatNumber);
            address.City.ShouldBe(city);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 234;

            var response = await client.Request($"api/addresses/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_address_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var address = CreateAddress(0);
            var id = await client.Request($"api/addresses")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(address)
                .ReceiveJson<int>();
            address.Id = id;
            address.Street = "StrTest";

            var response = await client.Request($"api/addresses")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(address);

            var addressUpdated = await client.Request($"api/addresses/{address.Id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<AddressVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            addressUpdated.ShouldNotBeNull();
            addressUpdated.Street.ShouldBe(address.Street);
        }

        [Fact]
        public async Task given_invalid_address_when_update_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var address = CreateAddress(100);

            var response = await client.Request($"api/addresses")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(address);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_address_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var address = CreateAddress(0);

            var response = await client.Request($"api/addresses")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(address);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(1);
        }

        [Fact]
        public async Task given_invalid_when_add_address_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var address = CreateAddress(1);

            var response = await client.Request($"api/addresses")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(address);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        private AddressVm CreateAddress(int id)
        {
            var address = new AddressVm 
            { 
                Id = id,
                BuildingNumber = "1",
                City = "ZG",
                Country = "PL",
                CustomerId = 1,
                FlatNumber = 10,
                Street = "Testowa",
                ZipCode = 65010
            };
            return address;
        }
    }
}
