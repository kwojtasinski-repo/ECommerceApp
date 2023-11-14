using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.ContactDetail;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class CustomerServiceTests : BaseTest<ICustomerService>
    {
        [Fact]
        public void given_valid_id_should_return_customer_for_edit()
        {
            var customerId = 1;

            var customer = _service.GetCustomerForEdit(customerId);

            customer.Id.ShouldBe(customerId);
        }

        [Fact]
        public void given_valid_customer_should_update()
        {
            var customer = GetSampleCustomer();
            var firstName = "Stanley";
            customer.FirstName = firstName;
            var lastName = "Stanton";
            customer.LastName = lastName;

            _service.UpdateCustomer(customer);

            var customerUpdated = _service.Get(customer.Id);
            customerUpdated.FirstName.ShouldBe(firstName);
            customerUpdated.LastName.ShouldBe(lastName);
        }

        [Fact]
        public void given_valid_id_should_delete()
        {
            var customer = GetSampleCustomer();
            customer.Id = 0;
            customer.Addresses = new List<AddressDto>();
            customer.ContactDetails = new List<NewContactDetailVm>();
            var customerId = _service.AddCustomer(customer);

            _service.DeleteCustomer(customerId);

            var customerDelted = _service.Get(customerId);
            customerDelted.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_should_return_customer_information()
        {
            var id = 1;

            var customerInformation = _service.GetCustomerInformationById(id);

            customerInformation.ShouldNotBeNull();
            customerInformation.Information.Length.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_id_should_return_null_customer_information()
        {
            var id = 1457457865;

            var customerInformation = _service.GetCustomerInformationById(id);

            customerInformation.ShouldBeNull();
        }

        [Fact]
        public void given_valid_user_id_should_return_customers_informations()
        {
            var customersInformation = _service.GetCustomersInformationByUserId(PROPER_CUSTOMER_ID).ToList();

            customersInformation.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_user_id_should_return_empty_customers_informations()
        {
            var customersInformation = _service.GetCustomersInformationByUserId("").ToList();

            customersInformation.Count.ShouldBe(0);
        }

        [Fact]
        public void given_customers_in_db_should_return_all_customers()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var customers = _service.GetAllCustomersForList(pageSize, pageNo, searchString);

            customers.Count.ShouldBeGreaterThan(0);
            customers.Customers.Count.ShouldBeGreaterThan(0);
            customers.CurrentPage.ShouldBe(pageNo);
            customers.PageSize.ShouldBe(pageSize);
            customers.SearchString.ShouldBe(searchString);
        }

        [Fact]
        public void given_customers_valid_user_id_should_return_all_customers()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var customers = _service.GetAllCustomersForList(PROPER_CUSTOMER_ID, pageSize, pageNo, searchString);

            customers.Count.ShouldBeGreaterThan(0);
            customers.Customers.Count.ShouldBeGreaterThan(0);
            customers.CurrentPage.ShouldBe(pageNo);
            customers.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public void given_valid_id_should_return_customer()
        {
            var id = 1;

            var customer = _service.GetCustomerDetails(id);

            customer.ShouldNotBeNull();
        }

        [Fact]
        public void given_valid_id_and_user_id_should_return_customer()
        {
            var id = 1;

            var customer = _service.GetCustomerDetails(id, PROPER_CUSTOMER_ID);

            customer.ShouldNotBeNull();
        }

        private NewCustomerVm GetSampleCustomer()
        {
            var customer = new NewCustomerVm
            {
                Id = 1,
                FirstName = "Mr",
                LastName = "Tester",
                IsCompany = false,
                UserId = PROPER_CUSTOMER_ID,
                Addresses = new List<AddressDto>()
                {
                    new AddressDto { Id = 1, BuildingNumber = "2", FlatNumber = 10, City = "Nowa Sól", Country = "Poland", Street = "Testowa", CustomerId = 1, ZipCode = "67-100" }
                },
                ContactDetails = new List<NewContactDetailVm>()
                {
                    new NewContactDetailVm{ Id = 1, ContactDetailInformation = "867123563", ContactDetailTypeId = 1, CustomerId = 1 }
                }
            };
            return customer;
        }
    }
}
