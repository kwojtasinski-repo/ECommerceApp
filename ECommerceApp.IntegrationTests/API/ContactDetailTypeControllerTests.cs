using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.ContactDetailType;
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
    public class ContactDetailTypeControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public ContactDetailTypeControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_contact_detail_type()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;
            var name = "PhoneNumber";

            var response = await client.Request($"api/contact-detail-types/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var brand = JsonConvert.DeserializeObject<ContactDetailTypeVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            brand.ShouldNotBeNull();
            brand.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 21;

            var response = await client.Request($"api/contact-detail-types/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_contact_detail_type_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultBrandVm(0);

            var response = await client.Request("api/contact-detail-types")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        }

        [Fact]
        public async Task given_invalid_contact_detail_type_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var brand = CreateDefaultBrandVm(53);

            var response = await client.Request("api/contact-detail-types")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_contact_detail_type_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 2;
            var name = "TestBrand";
            var brand = await client.Request($"api/contact-detail-types/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<ContactDetailTypeVm>();
            brand.Name = name;

            var response = await client.Request("api/contact-detail-types")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            var brandUpdated = await client.Request($"api/contact-detail-types/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<ContactDetailTypeVm>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            brandUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_not_existed_contact_detail_type_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 223;
            var brand = CreateDefaultBrandVm(id);

            var response = await client.Request("api/contact-detail-types")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(brand);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_contact_detail_types_in_db_should_return_contact_details()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/contact-detail-types")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var contactDetails = JsonConvert.DeserializeObject<List<ContactDetailTypeVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            contactDetails.Count.ShouldBeGreaterThan(0);
        }

        private ContactDetailTypeVm CreateDefaultBrandVm(int id)
        {
            var brand = new ContactDetailTypeVm
            {
                Id = id,
                Name = "ContactDetailTypeVmTest"
            };
            return brand;
        }
    }
}
