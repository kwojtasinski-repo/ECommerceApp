using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Customer;
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
    public class CustomerControllerTests : IClassFixture<BaseApiTest<Startup>>
    {
        private readonly FlurlClient _client;

        public CustomerControllerTests(BaseApiTest<Startup> baseApiTest)
        {
            _client = baseApiTest.Client;
        }

        [Fact]
        public async Task given_customers_in_db_should_return_customers()
        {
            var response = await _client.Request($"api/customers")
                .AllowAnyHttpStatus()
                .GetAsync();
            var customers = JsonConvert.DeserializeObject<ListForCustomerVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            customers.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_valid_id_should_return_customer()
        {
            var id = 1;

            var response = await _client.Request($"api/customers/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var customer = JsonConvert.DeserializeObject<CustomerDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            customer.ShouldNotBeNull();
            customer.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var id = 156;

            var response = await _client.Request($"api/customers/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_customer_should_add() 
        {
            var customer = CreateCustomer(0);

            var response = await _client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(customer);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());

            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_invalid_customer_should_return_status_code_conflict()
        {
            var customer = CreateCustomer(1);

            var response = await _client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(customer);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_customer_should_update()
        {
            var customer = CreateCustomer(0);
            var response = await _client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PostJsonAsync(customer);
            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            var firstName = "Janusz";
            var lastName = "Nosacz";
            customer.FirstName = firstName;
            customer.LastName = lastName;
            customer.Id = id;

            response = await _client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PutJsonAsync(customer);

            var customerUpdated = await _client.Request($"api/customers/{id}")
                .AllowAnyHttpStatus()
                .GetAsync()
                .ReceiveJson<CustomerDetailsVm>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            customerUpdated.FirstName.ShouldBe(firstName);
            customerUpdated.LastName.ShouldBe(lastName);
        }

        [Fact]
        public async Task given_invalid_customer_when_update_should_return_status_code_conflict()
        {
            var customer = CreateCustomer(189);

            var response = await _client.Request("api/customers")
                .AllowAnyHttpStatus()
                .PutJsonAsync(customer);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_page_size_number_and_search_string_when_paginate_should_return_customers()
        {
            int pageSize = 20;
            int pageNo = 1;
            string searchString = "M";

            var response = await _client.Request($"api/customers?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();
            var items = JsonConvert.DeserializeObject<ListForCustomerVm>(await response.ResponseMessage.Content.ReadAsStringAsync());

            items.Count.ShouldBe(1);
            items.Customers.Count.ShouldBe(1);
            items.Customers.Where(c => c.FirstName == "Mr").FirstOrDefault().ShouldNotBeNull();
        }

        [Fact]
        public async Task given_page_size_number_and_invalid_search_string_when_paginate_should_return_status_code_not_found()
        {
            int pageSize = 20;
            int pageNo = 1;
            string searchString = "MABC";

            var response = await _client.Request($"api/customers?=pageSize={pageSize}&pageNo={pageNo}&searchString={searchString}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        private CustomerVm CreateCustomer(int id)
        {
            var customer = new CustomerVm
            {
                Id = id,
                CompanyName = "Test sp. z.o.o.",
                IsCompany = true,
                FirstName = "TestFirstName",
                LastName = "TestLastName",
                NIP = "123456789",
                UserId = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e"
            };
            return customer;
        }
    }
}
