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


namespace ECommerceApp.Tests.Services.ItemService
{
    public class SetupClass : IDisposable
    {
        protected Dictionary<String, Object> items;
        protected IMapper mapper;

        public SetupClass()
        {
            // Do "global" initialization here; Only called once.
            items = SetItems();
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new MappingProfile());
            });
            mapper = config.CreateMapper();
        }

        public void Dispose()
        {
            // Do "global" teardown here; Only called once.
            items.Clear();
        }

        private Dictionary<String, Object> SetItems()
        {
            var items = new List<Item>() { new Item { Id = 1, BrandId = 1, Cost = new decimal(250.55), Name = "Buty Sportowe", Quantity = 50, TypeId = 1, Warranty = "365", Description = "Buty Sportowe do kupienia tylko i wyłącznie w internecie" } ,
                                           new Item { Id = 2, BrandId = 1, Cost = new decimal(350.25), Name = "Buty Sezonowe", Quantity = 50, TypeId = 1, Warranty = "365", Description = "Buty Sezonowe do kupienia tylko i wyłącznie w internecie" } ,
                                           new Item { Id = 3, BrandId = 1, Cost = new decimal(292.75), Name = "Buty do biegania", Quantity = 50, TypeId = 1, Warranty = "365", Description = "Buty do biegania do kupienia tylko i wyłącznie w internecie" } ,
                                           new Item { Id = 4, BrandId = 2, Cost = new decimal(3950.55), Name = "Samsung S20", Quantity = 20, TypeId = 2, Warranty = "365", Description = "Samsung S20 do kupienia tylko i wyłącznie w internecie" } ,
                                           new Item { Id = 5, BrandId = 2, Cost = new decimal(4250.55), Name = "Samsung S20+", Quantity = 10, TypeId = 2, Warranty = "365", Description = "Samsung S20+ do kupienia tylko i wyłącznie w internecie" } };
            var brands = new List<Brand>() { new Brand { Id = 1, Items = items.Where(it => it.Id < 4).ToList(), Name = "Addidas" }, new Brand { Id = 2, Items = items.Where(it => it.Id >= 4).ToList(), Name = "Samsung" } };
            var types = new List<Domain.Model.Type>() { new Domain.Model.Type { Id = 1, Name = "Buty", Items = items.Where(it => it.Id < 4).ToList() }, new Domain.Model.Type { Id = 1, Name = "Smartfon", Items = items.Where(it => it.Id >= 4).ToList() } };
            var tagsItemOne = new List<Tag>() { new Tag { Id = 1, Name = "Buty" }, new Tag { Id = 2, Name = "Sportowe" } };
            var tagsItemTwo = new List<Tag>() { new Tag { Id = 1, Name = "Buty" }, new Tag { Id = 3, Name = "Sezonowe" } };
            var tagsItemThree = new List<Tag>() { new Tag { Id = 1, Name = "Buty" }, new Tag { Id = 4, Name = "do biegania" } };
            var tagsItemFour = new List<Tag>() { new Tag { Id = 5, Name = "Smartfon" }, new Tag { Id = 6, Name = "S20" } };
            var tagsItemFive = new List<Tag>() { new Tag { Id = 5, Name = "Smartfon" }, new Tag { Id = 7, Name = "S20+" } };
            var tags = new List<Tag>();
            tags.AddRange(tagsItemOne);
            tags.Add(tagsItemTwo[1]);
            tags.Add(tagsItemThree[1]);
            tags.AddRange(tagsItemFour);
            tags.Add(tagsItemFive[1]);

            var dict = new Dictionary<String, Object>
            {
                { "items", items },
                { "brands", brands },
                { "types", types },
                { "tags", tags }
            };
            return dict;
        }
    }

    public class ItemServiceTests : SetupClass
    {
        /*public static MyDbContext InMemoryContext()
        {
            // SEE: https://docs.microsoft.com/en-us/ef/core/miscellaneous/testing/sqlite
            var connection = new SqlServerC("Data Source=:memory:");
            //var connection = new SqliteConnection("Data Source=:memory:");
            var options = new DbContextOptionsBuilder<Context>()
                .UseSqlite(connection)
                .Options;
            connection.Open();

            // create the schema
            using (var context = new MyDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            return new MyDbContext(options);

        }

        [Fact]
        public void CanReturnItemByIdFromDb()
        {
            var itemsInMemoryDatabase = new List<NewItemVm>
            {
                new NewItemVm() { Id = 1, Name = "T1" },
                new NewItemVm() { Id = 2, Name = "T2" },
                new NewItemVm() { Id = 3, Name = "T3" }
            };

            var mock = new Mock<ItemServiceAbstract>();
            mock.Setup(x => x.GetItemById(It.IsAny<int>())).Returns((int id) => itemsInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var itemThatExists = repository.GetItemById(3);
            itemThatExists.Should().NotBeNull();
            itemThatExists.Should().BeSameAs(itemsInMemoryDatabase[2]);
            itemThatExists.Should().Be(itemsInMemoryDatabase[2]);
            itemThatExists.Should().BeOfType(typeof(NewItemVm));
        }

        [Fact]
        public void CantReturnCouponByIdFromDb()
        {
            
            var itemsInMemoryDatabase = new List<NewItemVm>
            {
                new NewItemVm() { Id = 1, Name = "T1" },
                new NewItemVm() { Id = 2, Name = "T2" },
                new NewItemVm() { Id = 3, Name = "T3" }
            };

            var options = new DbContextOptionsBuilder<Context>()
                            .UseInMemoryDatabase(databaseName: "ECommerceApp")
                            .Options;
            var contextMock = new Mock<Context>();
            var itemRepoMock = new Mock<IItemRepository>();
            var mapperMock = new Mock<IMapper>();

            var mock = new Mock<ItemServiceAbstract>();
            mock.Setup(x => x.GetItemById(It.IsAny<int>())).Returns((int id) => itemsInMemoryDatabase.SingleOrDefault(i => i.Id == id));
            var repository = mock.Object;

            var itemThatExists = repository.GetItemById(10);
            itemThatExists.Should().BeNull();
        }*/

        [Fact]
        public void ShouldReturnItem()
        {
            //arrange
            var id = 1;
            var itemRepo = new Mock<IItemRepository>();
            var itemExpected = ((List<Item>)items["items"]).Where(i => i.Id == 1).FirstOrDefault();
            itemRepo.Setup(i => i.GetItemById(id)).Returns(itemExpected);
            var itemService = new ECommerceApp.Application.Services.ItemService(itemRepo.Object, mapper);
             
            //act
            var item = itemService.GetItemById(1);

            //assert
            item.Should().BeOfType(null);
            item.Should().BeOfType(typeof(Item));
            item.Should().NotBeNull();
            item.Should().BeSameAs(item);
            item.Name.Should().BeSameAs(itemExpected.Name);
            item.Id.Should().Be(itemExpected.Id);
            item.Cost.Should().Be(itemExpected.Cost);
        }

    }
}
