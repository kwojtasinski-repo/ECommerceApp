using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.ContactDetail;
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
    public class ContactDetailControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public ContactDetailControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_contact_detail()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;
            var phoneNumber = "867123563";

            var response = await client.Request($"api/contact-details/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();
            var contactDetail = JsonConvert.DeserializeObject<ContactDetailsForListVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            contactDetail.ShouldNotBeNull();
            contactDetail.ContactDetailInformation.ShouldBe(phoneNumber);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 212;

            var response = await client.Request($"api/contact-details/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_contact_detail_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var contactDetail = CreateContactDetail(0);
            var id = await client.Request("api/contact-details")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(contactDetail)
                .ReceiveJson<int>();
            contactDetail.Id = id;
            contactDetail.ContactDetailInformation = "895423143";

            var response = await client.Request($"api/contact-details/{id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(contactDetail);

            var contactDetailUpdated = await client.Request($"api/contact-details/{contactDetail.Id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .GetJsonAsync<ContactDetailDto>();
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            contactDetailUpdated.ShouldNotBeNull();
            contactDetailUpdated.ContactDetailInformation.ShouldBe(contactDetail.ContactDetailInformation);
        }

        [Fact]
        public async Task given_invalid_contact_detail_when_update_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var contactDetail = CreateContactDetail(100);

            var response = await client.Request($"api/contact-details/{contactDetail.Id}")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PutJsonAsync(contactDetail);

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_contact_detail_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var contactDetail = CreateContactDetail(0);

            var response = await client.Request($"api/contact-details")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(contactDetail);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(1);
        }

        [Fact]
        public async Task given_invalid_contact_detail_when_add_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var contactDetail = CreateContactDetail(1);

            var response = await client.Request($"api/contact-details")
                .WithHeader("content-type", "application/json")
                .AllowAnyHttpStatus()
                .PostJsonAsync(contactDetail);

            response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        }

        private ContactDetailDto CreateContactDetail(int id)
        {
            var contactDetail = new ContactDetailDto
            {
                Id = id,
                ContactDetailInformation = "567234123",
                ContactDetailTypeId = 1,
                CustomerId = 1
            };

            return contactDetail;
        }
    }
}
