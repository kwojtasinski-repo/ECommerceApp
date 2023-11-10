using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class AddressServiceTests : BaseTest<IAddressService>
    {
        private readonly string PROPER_CUSTOMER_ID = "a85e6eb8-242d-4bbe-9ce6-b2fbb2ddbb4e";

        [Fact]
        public void given_valid_id_should_return_address()
        {
            var id = 1;
            var zipCode = 67100;

            var address = _service.GetAddress(id);

            address.ShouldNotBeNull();
            address.ShouldBeOfType<AddressVm>();
            address.ZipCode.ShouldBe(zipCode);
        }

        [Fact]
        public void given_invalid_id_shouldnt_return_address()
        {
            var id = 123;

            var address = _service.GetAddress(id);

            address.ShouldBeNull();
        }

        [Fact]
        public void given_valid_id_and_user_id_should_return_address_details()
        {
            var id = 1;
            var zipCode = 67100;

            var address = _service.GetAddressDetail(id, PROPER_CUSTOMER_ID);

            address.ShouldNotBeNull();
            address.ShouldBeOfType<AddressVm>();
            address.ZipCode.ShouldBe(zipCode);
        }

        [Fact]
        public void given_valid_id_and_invalid_user_id_shouldnt_return_null()
        {
            var id = 1;
            var userId = "";

            var address = _service.GetAddressDetail(id, userId);

            address.ShouldBeNull();
        }

        [Fact]
        public void given_valid_address_should_add()
        {
            var address = CreateAddress(0);

            var id = _service.AddAddress(address);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_address_when_add_should_throw_an_exception()
        {
            var address = CreateAddress(0);
            address.CustomerId = 0;

            var exception = Should.Throw<BusinessException>(() => _service.AddAddress(address));

            exception.Message.ShouldBe("Given ivalid customer id");
        }

        [Fact]
        public void given_valid_address_with_user_id_should_add()
        {
            SetHttpContextUserId(PROPER_CUSTOMER_ID);
            var address = CreateAddress(0);

            var id = _service.AddAddress(address);

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_address_with_invalid_user_id_should_throw_an_exception()
        {
            SetHttpContextUserId("");
            var address = CreateAddress(0);

            var exception = Should.Throw<BusinessException>(() => _service.AddAddress(address));

            exception.Message.ShouldBe("Cannot add address check your customer id");
        }

        [Fact]
        public void given_valid_id_address_should_exists()
        {
            var id = 1;

            var exists = _service.AddressExists(id);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_invalid_id_address_shouldnt_exists()
        {
            var id = 1;

            var exists = _service.AddressExists(id);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_valid_id_and_user_id_address_should_exists()
        {
            var id = 1;

            var exists = _service.AddressExists(id, PROPER_CUSTOMER_ID);

            exists.ShouldBeTrue();
        }

        [Fact]
        public void given_valid_id_and_invalid_user_id_address_shouldnt_exists()
        {
            var id = 1;

            var exists = _service.AddressExists(id, "");

            exists.ShouldBeFalse();
        }

        [Fact]
        public void given_valid_expression_should_return_addresses()
        {
            var addresses = _service.GetAllAddresss(a => true);

            addresses.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_expression_should_return_empty_addresses()
        {
            var addresses = _service.GetAllAddresss(a => a.Country == "abcsadwrwqwe");

            addresses.Count().ShouldBe(0);
        }

        [Fact]
        public void given_valid_address_should_update()
        {
            var address = CreateAddress(0);
            var id = _service.AddAddress(address);
            address = _service.Get(id);
            var street = "Ul. Ma";
            address.Street = street;

            _service.UpdateAddress(address);

            var addressUpdated = _service.GetAddress(id);
            addressUpdated.ShouldNotBeNull();
            addressUpdated.Street.ShouldBe(street);
        }

        [Fact]
        public void given_valid_address_should_delete()
        {
            var address = CreateAddress(0);
            var id = _service.AddAddress(address);

            _service.DeleteAddress(id);

            var addressDeleted = _service.GetAddress(id);
            addressDeleted.ShouldBeNull();
        }

        private AddressVm CreateAddress(int id)
        {
            var address = new AddressVm
            {
                Id = id,
                City = "ZG",
                Street = "Street 1",
                ZipCode = 65010,
                Country = "PL",
                BuildingNumber = "2a",
                FlatNumber = 1,
                CustomerId = 1
            };
            return address;
        }

        protected override void OverrideServicesImplementation(IServiceCollection services)
        {
           services.AddSingleton<IHttpContextAccessor, HttpContextAccessorTest>();
        }
    }
}
