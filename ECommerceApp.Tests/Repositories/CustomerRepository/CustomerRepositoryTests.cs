using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Repositories.CustomerRepository
{
    public class CustomerRepositoryTests : BaseTest<Customer>
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerRepositoryTests()
        {
            _customerRepository = new Infrastructure.Repositories.CustomerRepository(_context);
        }

        [Fact]
        public void CanReturnCustomerByIdFromDb()
        {
            var id = 1;

            var customerThatExists = _customerRepository.GetCustomerById(id);

            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeOfType(typeof(Customer));
        }

        [Fact]
        public void CantReturnCustomerByIdFromDb()
        {
            var id = 1100;

            var customerThatExists = _customerRepository.GetCustomerById(id);

            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCustomerByIdUserIdFromDb()
        {
            var id = 1;
            var userId = "123"; 

            var customerThatExists = _customerRepository.GetCustomerById(id, userId);

            customerThatExists.Should().NotBeNull();
            customerThatExists.Should().BeOfType(typeof(Customer));
        }

        [Fact]
        public void CantReturnCustomerByIdUserIdFromDb()
        {
            var id = 1;
            var userId = "123abc";

            var customerThatExists = _customerRepository.GetCustomerById(id, userId);

            customerThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnCustomersFromDb()
        {
            var customers = new List<Customer>();

            var customersThatExists = _customerRepository.GetAllCustomers().ToList();

            customersThatExists.Should().NotBeNull();
            customersThatExists.Count.Should().BeGreaterThan(customers.Count);
            customersThatExists.Should().HaveCount(1);
        }
    }
}
