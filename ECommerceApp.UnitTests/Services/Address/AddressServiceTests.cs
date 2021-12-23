using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Address;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Xunit;

namespace ECommerceApp.UnitTests.Services.Address
{
    public class AddressServiceTests
    {
        private readonly Mock<IAddressRepository> _addressRepository;
        private readonly Mock<ICustomerService> _customerService;
        private readonly IMapper _mapper;

        public AddressServiceTests()
        {
            _addressRepository = new Mock<IAddressRepository>();
            _customerService = new Mock<ICustomerService>();

            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
        }

        [Fact]
        public void given_valid_address_should_add()
        {
            var address = CreateAddressVm();
            address.Id = 0;
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            addressService.AddAddress(address);

            _addressRepository.Verify(a => a.AddAddress(It.IsAny<Domain.Model.Address>()), Times.Once);
        }

        [Fact]
        public void given_invalid_address_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressVm();
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_address_with_invalid_customer_when_adding_should_throw_an_exception()
        {
            var address = CreateAddressVm();
            address.Id = 0;
            address.CustomerId = 0;
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            Action action = () => addressService.AddAddress(address);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Given ivalid customer id");
        }

        [Fact]
        public void given_valid_address_and_user_id_when_adding_should_add()
        {
            var address = CreateAddressVm();
            address.Id = 0;
            var userId = Guid.NewGuid().ToString();
            var customers = CreateCustomers();
            var customer = customers.Where(c => c.Id == address.CustomerId).FirstOrDefault();
            customer.UserId = userId;
            _customerService.Setup(c => c.GetAllCustomers(It.IsAny<Expression<Func<Domain.Model.Customer, bool>>>())).Returns(customers.AsEnumerable());
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            addressService.AddAddress(address, userId);

            _addressRepository.Verify(a => a.AddAddress(It.IsAny<Domain.Model.Address>()), Times.Once);
        }

        [Fact]
        public void given_ivalid_address_and_user_id_when_adding_should_add()
        {
            var address = CreateAddressVm();
            var userId = Guid.NewGuid().ToString();
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            Action action = () => addressService.AddAddress(address, userId);

            action.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_valid_address_and_invalid_user_id_when_adding_should_add()
        {
            var address = CreateAddressVm();
            address.Id = 0;
            var userId = Guid.NewGuid().ToString();
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            Action action = () => addressService.AddAddress(address, userId);

            action.Should().ThrowExactly<BusinessException>().WithMessage("Cannot add address check your customer id");
        }

        [Fact]
        public void given_valid_address_id_should_return_true()
        {
            var id = 1;
            var customerId = 1;
            var address = CreateAddress(id, customerId);
            _addressRepository.Setup(a => a.GetAddressById(It.IsAny<int>())).Returns(address);
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            var exists = addressService.AddressExists(id);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_address_id_should_return_false()
        {
            var id = 1;
            var addressService = new AddressService(_addressRepository.Object, _mapper, _customerService.Object);

            var exists = addressService.AddressExists(id);

            exists.Should().BeFalse();
        }

        private AddressVm CreateAddressVm()
        {
            var vm = new AddressVm();
            vm.Id = 1;
            vm.Street = "Long";
            vm.BuildingNumber = "2";
            vm.FlatNumber = 1;
            vm.ZipCode = 11241;
            vm.City = "Gistok";
            vm.Country = "Poland";
            vm.CustomerId = 1;
            return vm;
        }

        private Domain.Model.Address CreateAddress(int id, int customerId)
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
                Customer = new Domain.Model.Customer { Id = customerId }
            };
            return address;
        }

        private List<CustomerVm> CreateCustomers() 
        {
            var customers = new List<CustomerVm>
            {
                CreateCustomer(1),
                CreateCustomer(2),
                CreateCustomer(3)
            };
            return customers;
        }

        private CustomerVm CreateCustomer(int id)
        {
            var customer = new CustomerVm
            {
                Id = id,
                UserId = Guid.NewGuid().ToString()
            };
            return customer;
        }
    }
}
