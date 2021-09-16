using AutoMapper;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Domain.Model;
using ECommerceApp.Tests.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Services.ItemService
{
    public class ItemBaseTests : BaseTest<Item>
    {
        protected readonly IItemService _service;

        public ItemBaseTests()
        {
            var repo = new Infrastructure.Repositories.ItemRepository(_context);

            var configurationProvider = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MappingProfile>();
            });

            var mapper = configurationProvider.CreateMapper();
            _service = new Application.Services.ItemService(repo, mapper);
        }
    }
}
