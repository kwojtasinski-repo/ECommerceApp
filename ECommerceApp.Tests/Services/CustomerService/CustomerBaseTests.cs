using AutoMapper;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Services.CustomerService
{
    public class CustomerBaseTests : BaseTest<Customer>
    {
        protected readonly ICustomerService _service;

        public CustomerBaseTests()
        {
            var repo = new Infrastructure.Repositories.CustomerRepository(_context);

            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            var mapper = configurationProvider.CreateMapper();
            _service = new Application.Services.CustomerService(repo, mapper);
        }
    }
}
