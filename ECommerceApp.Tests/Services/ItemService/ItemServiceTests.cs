using AutoMapper;
using ECommerceApp.Application.Services;
using ECommerceApp.Application.Mapping;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using FluentAssertions;
using ECommerceApp.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using ECommerceApp.Application.Exceptions;

namespace ECommerceApp.Tests.Services.ItemService
{
    public class ItemServiceTests : ItemBaseTests
    {
        [Fact]
        public void CanReturnItem()
        {
            var id = 1;

            var coupon = _service.Get(id);

            coupon.Should().NotBeNull();
            coupon.Should().BeOfType(typeof(ItemVm));
        }

        [Fact]
        public void ShouldAddItem()
        {
            var item = new ItemVm
            {
                Id = 0,
                BrandId = 1,
                Cost = new decimal(1000),
                Description = "Opis",
                Name = "Testowy",
                Quantity = 10,
                TypeId = 1,
                Warranty = "123",
                ItemTags = new List<ItemTagVm> { new ItemTagVm { TagId = 1 }, new ItemTagVm { TagId = 2 } }
            };

            var id = _service.Add(item);
            var itemFromDb = _context.Items.Where(i => i.Id == id).Include(it => it.ItemTags).AsNoTracking().FirstOrDefault();

            itemFromDb.Should().NotBeNull();
            itemFromDb.ItemTags.Count.Should().Be(item.ItemTags.Count);
        }

        [Fact]
        public void ShouldntAddItem()
        {
            var item = new ItemVm { Id = 1000 };

            Action act = () => _service.Add(item);

            act.Should().ThrowExactly<BusinessException>().WithMessage("When adding object Id should be equals 0");
        }
    }
}
