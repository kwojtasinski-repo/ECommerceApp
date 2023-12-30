using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Services.Addresses;
using ECommerceApp.Application.Services.Customers;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using ECommerceApp.UnitTests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

        [Fact]
        public void given_invalid_address_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressDto();
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_address_with_invalid_customer_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressDto();
            address.Id = 0;
            address.CustomerId = 0;
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

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
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            addressService.AddAddress(address);

            _addressRepository.Verify(a => a.AddAddress(It.IsAny<Domain.Model.Address>()), Times.Once);
        }

        [Fact]
        public void given_ivalid_address_and_user_id_when_adding_should_add()
        {
            var address = CreateAddressDto();
            _contextAccessor.SetUserId(Guid.NewGuid().ToString());
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_address_and_invalid_user_id_when_adding_should_add()
        {
            var address = CreateAddressDto();
            address.Id = 0;
            _contextAccessor.SetUserId(Guid.NewGuid().ToString());
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Cannot add address check your customer id");
        }

        [Fact]
        public void given_valid_address_id_should_return_true()
        {
            var id = 1;
            var customerId = 1;
            var userId = Guid.NewGuid().ToString();
            _contextAccessor.SetUserId(userId);
            var address = CreateAddress(id, customerId, userId);
            _addressRepository.Setup(a => a.GetAll()).Returns(new List<Domain.Model.Address> { address }.AsQueryable());
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            var exists = addressService.AddressExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_address_id_should_return_false()
        {
            var id = 1;
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            var exists = addressService.AddressExists(id);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_address_when_add_should_throw_an_exception()
        {
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            Action action = () => addressService.AddAddress(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_address_when_add_with_user_id_should_throw_an_exception()
        {
            _contextAccessor.SetUserId("");
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            Action action = () => addressService.AddAddress(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_address_when_update_should_throw_an_exception()
        {
            var addressService = new AddressService(_addressRepository.Object, _customerRepository.Object, _mapper, _contextAccessor);

            Action action = () => addressService.UpdateAddress(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private AddressDto CreateAddressDto()
        {
            var dto = new AddressDto();
            dto.Id = 1;
            dto.Street = "Long";
            dto.BuildingNumber = "2";
            dto.FlatNumber = 1;
            dto.ZipCode = "11-241";
            dto.City = "Gistok";
            dto.Country = "Poland";
            dto.CustomerId = 1;
            return dto;
        }

        private Domain.Model.Address CreateAddress(int id, int customerId, string userId = null)
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
