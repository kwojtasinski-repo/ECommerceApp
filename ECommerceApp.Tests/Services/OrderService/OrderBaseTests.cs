using AutoMapper;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Services.OrderService
{
    public class OrderBaseTests : BaseTest<Order>
    {
        protected readonly IOrderService _service;

        public OrderBaseTests()
        {
            var repo = new Infrastructure.Repositories.OrderRepository(_context);

            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            var mapper = configurationProvider.CreateMapper();
            _service = new Application.Services.OrderService(repo, mapper);
        }
    }
}
