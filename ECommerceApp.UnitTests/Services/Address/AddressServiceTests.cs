using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Permissions;
using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Address
{
    public class AddressServiceTests : BaseTest
    {
        private readonly Mock<IAddressRepository> _addressRepository;
        private readonly Mock<ICustomerRepository> _customerRepository;
        private readonly HttpContextAccessorTest _contextAccessor;

        public AddressServiceTests()
        {
            _addressRepository = new Mock<IAddressRepository>();
            _customerRepository = new Mock<ICustomerRepository>();
            _contextAccessor = new HttpContextAccessorTest();
        }

        private AddressService CreateAddressService()
            => new(_addressRepository.Object, _customerRepository.Object, _mapper, new UserContextTest(_contextAccessor));

        [Fact]
        public void given_invalid_address_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressDto();
            var addressService = CreateAddressService();

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_address_with_invalid_customer_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressDto();
            address.Id = 0;
            address.CustomerId = 0;
            var addressService = CreateAddressService();

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Given ivalid customer id");
        }

        [Fact]
        public void given_valid_address_and_user_id_when_adding_should_add()
        {
            var address = CreateAddressDto();
            address.Id = 0;
            var userId = Guid.NewGuid().ToString();
            _contextAccessor.SetUserId(userId);
            var customers = CreateCustomers();
            var customer = customers.Where(c => c.Id == address.CustomerId).FirstOrDefault();
            customer.UserId = userId;
            _customerRepository.Setup(c => c.CustomerExists(It.IsAny<int>(), It.IsAny<string>())).Returns(true);
            var addressService = CreateAddressService();

            addressService.AddAddress(address);

            _addressRepository.Verify(a => a.AddAddress(It.IsAny<Domain.Model.Address>()), Times.Once);
        }

        [Fact]
        public void given_ivalid_address_and_user_id_when_adding_should_add()
        {
            var address = CreateAddressDto();
            _contextAccessor.SetUserId(Guid.NewGuid().ToString());
            var addressService = CreateAddressService();

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_address_and_not_existing_customer_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressDto();
            address.Id = 0;
            _contextAccessor.SetUserId(Guid.NewGuid().ToString());
            var addressService = CreateAddressService();

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Cannot add address check your customer id");
        }

        [Fact]
        public void given_invalid_address_id_should_return_false()
        {
            var id = 1;
            var addressService = CreateAddressService();

            var exists = addressService.AddressExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_address_when_add_should_throw_an_exception()
        {
            var addressService = CreateAddressService();

            Action action = () => addressService.AddAddress(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_address_when_add_with_user_id_should_throw_an_exception()
        {
            _contextAccessor.SetUserId("");
            var addressService = CreateAddressService();

            Action action = () => addressService.AddAddress(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_address_when_update_should_throw_an_exception()
        {
            var addressService = CreateAddressService();

            Action action = () => addressService.UpdateAddress(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_valid_dto_with_existing_address_when_update_should_return_true()
        {
            var dto = CreateAddressDto();
            _contextAccessor.SetUserRole(UserPermissions.Roles.Administrator);
            var addressService = CreateAddressService();
            var address = CreateAddress(dto.Id.Value, dto.CustomerId);
            _addressRepository.Setup(a => a.GetAddressById(address.Id)).Returns(address);

            var result = addressService.UpdateAddress(dto);

            result.Should().BeTrue();
            _addressRepository.Verify(a => a.UpdateAddress(It.IsAny<Domain.Model.Address>()), Times.Once);
        }

        [Fact]
        public void given_valid_dto_with_not_existing_address_when_update_should_return_true()
        {
            var dto = CreateAddressDto();
            _contextAccessor.SetUserRole(UserPermissions.Roles.Administrator);
            var addressService = CreateAddressService();

            var result = addressService.UpdateAddress(dto);

            result.Should().BeFalse();
            _addressRepository.Verify(a => a.GetAddressById(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void given_valid_dto_and_not_existing_address_for_current_user_should_return_false()
        {
            var dto = CreateAddressDto();
            var addressService = CreateAddressService();

            var result = addressService.UpdateAddress(dto);

            result.Should().BeFalse();
            _addressRepository.Verify(a => a.GetAddressById(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void given_not_existing_address_when_delete_should_return_false()
        {
            var addressService = CreateAddressService();

            var result = addressService.DeleteAddress(1);

            result.Should().BeFalse();
            _addressRepository.Verify(a => a.GetCountByIdAndUserId(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void given_only_one_address_for_current_user_when_delete_should_throw_an_exception()
        {
            _addressRepository.Setup(a => a.GetCountByIdAndUserId(It.IsAny<int>(), It.IsAny<string>())).Returns(1);
            var addressService = CreateAddressService();

            var action = () => addressService.DeleteAddress(1);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("You cannot delete address if you only have 1");
        }

        [Fact]
        public void given_valid_address_when_delete_should_delete()
        {
            _addressRepository.Setup(a => a.GetCountByIdAndUserId(It.IsAny<int>(), It.IsAny<string>())).Returns(2);
            var addressService = CreateAddressService();

            addressService.DeleteAddress(1);

            _addressRepository.Verify(a => a.DeleteAddress(It.IsAny<int>()), Times.Once);
        }

        private static AddressDto CreateAddressDto()
        {
            return new AddressDto
            {
                Id = 1,
                Street = "Long",
                BuildingNumber = "2",
                FlatNumber = 1,
                ZipCode = "11-241",
                City = "Gistok",
                Country = "Poland",
                CustomerId = 1
            };
        }

        private static Domain.Model.Address CreateAddress(int id, int customerId, string userId = null)
        {
            var address = new Domain.Model.Address
            {
                Id = id,
                BuildingNumber = "2b",
                FlatNumber = 12,
                City = "Zagorze",
                Country = "Poland",
                CustomerId = 1,
                Street = "Warszawska",
                ZipCode = 13425,
                Customer = new Domain.Model.Customer { Id = customerId, UserId = userId }
            };
            return address;
        }

        private static List<CustomerDto> CreateCustomers()
        {
            var customers = new List<CustomerDto>
            {
                CreateCustomer(1),
                CreateCustomer(2),
                CreateCustomer(3)
            };
            return customers;
        }

        private static CustomerDto CreateCustomer(int id)
        {
            var customer = new CustomerDto
            {
                Id = id,
                UserId = Guid.NewGuid().ToString()
            };
            return customer;
        }
    }
}
