using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.ViewModels.Customer;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Services.Customer
{
    public class CustomerServiceTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<ICustomerRepository> _customerRepository;

        public CustomerServiceTests()
        {
            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            _mapper = configurationProvider.CreateMapper();
            _customerRepository = new Mock<ICustomerRepository>();
        }

        [Fact]
        public void given_valid_customer_should_add()
        {
            var customer = CreateNewCustomerVm(0, Guid.NewGuid().ToString());
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            customerService.AddCustomer(customer);

            _customerRepository.Verify(c => c.Add(It.IsAny<Domain.Model.Customer>()), Times.Once);
        }

        [Fact]
        public void given_invalid_customer_should_throw_an_exception()
        {
            var customer = CreateNewCustomerVm(1, Guid.NewGuid().ToString());
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            Action action = () => customerService.AddCustomer(customer);

            action.Should().Throw<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }

        [Fact]
        public void given_null_customer_when_update_should_throw_an_exception()
        {
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            Action action = () => customerService.Update(null);

            action.Should().Throw<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_null_customer_when_delete_should_throw_an_exception()
        {
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            Action action = () => customerService.Delete(null);

            action.Should().Throw<BusinessException>().Which.Message.Contains("cannot be null");
        }

        [Fact]
        public void given_valid_customer_id_and_user_id_customer_should_exists()
        {
            var id = 1;
            var userId = Guid.NewGuid().ToString();
            var customer = CreateCustomer(id, userId);
            _customerRepository.Setup(c => c.CustomerExists(id, userId)).Returns(true);
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            var exists = customerService.CustomerExists(id, userId);

            exists.Should().BeTrue();
        }

        [Fact]
        public void given_invalid_customer_id_customer_shouldnt_exists()
        {
            var id = 1;
            var userId = Guid.NewGuid().ToString();
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            var exists = customerService.CustomerExists(id, userId);

            exists.Should().BeFalse();
        }

        [Fact]
        public void given_null_customer_when_add_should_throw_an_exception()
        {
            var customerService = new CustomerService(_customerRepository.Object, _mapper);

            Action action = () => customerService.AddCustomer(null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("cannot be null");
        }

        private Domain.Model.Customer CreateCustomer(int id, string userId)
        {
            var customer = new Domain.Model.Customer
            {
                Id = id,
                NIP = "",
                IsCompany = false,
                CompanyName = "",
                FirstName = "Carl",
                LastName = "Johnson",
                UserId = userId
            };
            return customer;
        }

        private NewCustomerVm CreateNewCustomerVm(int id, string userId)
        {
            var customer = new NewCustomerVm
            {
                Id = id,
                NIP = "",
                IsCompany = false,
                CompanyName = "",
                FirstName = "Tommy",
                LastName = "Vercetti",
                UserId = userId
            };
            return customer;
        }
    }
}
