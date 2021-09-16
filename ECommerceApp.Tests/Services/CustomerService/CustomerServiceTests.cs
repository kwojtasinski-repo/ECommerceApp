using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Customer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.Tests.Services.CustomerService
{
    public class CustomerServiceTests : CustomerBaseTests
    {
        [Fact]
        public void CanReturnCustomer()
        {
            var id = 1;

            var coupon = _service.Get(id);

            coupon.Should().NotBeNull();
            coupon.Should().BeOfType(typeof(CustomerVm));
        }

        [Fact]
        public void ShouldAddCustomer()
        {
            var customer = new CustomerVm
            {
                Id = 0,
                FirstName = "K",
                LastName = "W",
                IsCompany = false,
                UserId = "123"
            };

            var id = _service.Add(customer);
            var itemFromDb = _context.Customers.Where(i => i.Id == id).AsNoTracking().FirstOrDefault();

            itemFromDb.Should().NotBeNull();
            itemFromDb.Id.Should().Be(id);
        }

        [Fact]
        public void ShouldntAddCustomer()
        {
            var customer = new CustomerVm { Id = 1000 };

            Action act = () => _service.Add(customer);

            act.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }
    }
}
