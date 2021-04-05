using System;
using System.Collections.Generic;
using System.Text;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Moq;
using Xunit;
using System.Collections.ObjectModel;
using System.Linq;
using ECommerceApp.Domain.Interface;

namespace ECommerceApp.Tests.Repositories.ItemRepository
{
    public class ItemRepositoryTests
    {
        [Fact]
        public void CanReturnItemFromDb()
        {
            var itemInMemoryDatabase = new List<Item>
            {
                new Item() {Id = 1, Name = "Biblia Inwestowania Maciej Wieczorek", Cost= new decimal(69.00),
                    Description = "Jest napisana przez 13 najbardziej rozpoznawalnych inwestorów w Polsce. Dzięki niej poznasz aż 13 różnych strategii zabezpieczania i pomnażania majątku.",
                    Warranty = "365", BrandId = 1, TypeId = 1, ItemTags=new List<ItemTag>() { new ItemTag() { ItemId = 1,TagId = 1}, new ItemTag() { ItemId = 1, TagId = 2 } }
                },
                new Item() {Id = 2, Name = "Samsung S20", Cost= new decimal(3499.99),
                    Description = "Wyświetlacz: 6.2\", 3200 x 1440px, Dynamic AMOLED\nProcesor: Exynos 990, Ośmiordzeniowy\nWersja systemu: Android 10\nPamięć RAM: 8 GB\nPamięć wbudowana [GB]: 128\nAparat Tylny: 64 Mpx + 2 x 12 Mpx, Przedni 10 Mpx\nKomunikacja: Wi-Fi, NFC, Bluetooth 5.0, USB C\nPojemność akumulatora [mAh]:4000",
                    Warranty = "730", BrandId = 2, TypeId = 2, ItemTags=new List<ItemTag>(){ new ItemTag() { ItemId = 2,TagId = 3 }, new ItemTag() { ItemId = 2, TagId = 4 } }
                },
                new Item() {Id = 3, Name = "AKG Y500 Wireless", Cost= new decimal(549.99),
                    Description = "Budowa słuchawek: nauszne\nŁączność: bezprzewodowe z opcją kabla 3,5mm, Bluetooth\nMikrofon / Regulacja głośności:  tak / tak\nPasmo przenoszenia: 16 - 22000 Hz",
                    Warranty = "365", BrandId = 3, TypeId = 2, ItemTags=new List<ItemTag>(){ new ItemTag() { ItemId = 3,TagId = 5}, new ItemTag() { ItemId = 3, TagId = 6 } }
                }
            };

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetItemById(It.IsAny<int>())).Returns((int i) => itemInMemoryDatabase.SingleOrDefault(bo => bo.Id == i));
            var repository = mock.Object;

            var itemThatExists = repository.GetItemById(3);
            itemThatExists.Should().NotBeNull();
            itemThatExists.Should().Be(itemInMemoryDatabase[2]);
            itemThatExists.Should().BeOfType(typeof(Item));
            itemThatExists.Should().BeSameAs(itemInMemoryDatabase[2]);
        }

        [Fact]
        public void CantReturnItemFromDb()
        {
            var itemInMemoryDatabase = new List<Item>
            {
                new Item() {Id = 1, Name = "Biblia Inwestowania Maciej Wieczorek", Cost= new decimal(69.00),
                    Description = "Jest napisana przez 13 najbardziej rozpoznawalnych inwestorów w Polsce. Dzięki niej poznasz aż 13 różnych strategii zabezpieczania i pomnażania majątku.",
                    Warranty = "365", BrandId = 1, TypeId = 1, ItemTags=new List<ItemTag>() { new ItemTag() { ItemId = 1,TagId = 1}, new ItemTag() { ItemId = 1, TagId = 2 } }
                },
                new Item() {Id = 2, Name = "Samsung S20", Cost= new decimal(3499.99),
                    Description = "Wyświetlacz: 6.2\", 3200 x 1440px, Dynamic AMOLED\nProcesor: Exynos 990, Ośmiordzeniowy\nWersja systemu: Android 10\nPamięć RAM: 8 GB\nPamięć wbudowana [GB]: 128\nAparat Tylny: 64 Mpx + 2 x 12 Mpx, Przedni 10 Mpx\nKomunikacja: Wi-Fi, NFC, Bluetooth 5.0, USB C\nPojemność akumulatora [mAh]:4000",
                    Warranty = "730", BrandId = 2, TypeId = 2, ItemTags=new List<ItemTag>(){ new ItemTag() { ItemId = 2,TagId = 3 }, new ItemTag() { ItemId = 2, TagId = 4 } }
                },
                new Item() {Id = 3, Name = "AKG Y500 Wireless", Cost= new decimal(549.99),
                    Description = "Budowa słuchawek: nauszne\nŁączność: bezprzewodowe z opcją kabla 3,5mm, Bluetooth\nMikrofon / Regulacja głośności:  tak / tak\nPasmo przenoszenia: 16 - 22000 Hz",
                    Warranty = "365", BrandId = 3, TypeId = 2, ItemTags=new List<ItemTag>(){ new ItemTag() { ItemId = 3,TagId = 5}, new ItemTag() { ItemId = 3, TagId = 6 } }
                }
            };

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetItemById(It.IsAny<int>())).Returns((int i) => itemInMemoryDatabase.SingleOrDefault(bo => bo.Id == i));
            var repository = mock.Object;

            var itemThatExists = repository.GetItemById(4);
            itemThatExists.Should().BeNull();
        }

        [Fact]
        public void CanReturnItemsFromDb()
        {
            var itemInMemoryDatabase = new List<Item>
            {
                new Item() {Id = 1, Name = "Biblia Inwestowania Maciej Wieczorek", Cost= new decimal(69.00),
                    Description = "Jest napisana przez 13 najbardziej rozpoznawalnych inwestorów w Polsce. Dzięki niej poznasz aż 13 różnych strategii zabezpieczania i pomnażania majątku.",
                    Warranty = "365", BrandId = 1, TypeId = 1, ItemTags=new List<ItemTag>() { new ItemTag() { ItemId = 1,TagId = 1}, new ItemTag() { ItemId = 1, TagId = 2 } }
                },
                new Item() {Id = 2, Name = "Samsung S20", Cost= new decimal(3499.99),
                    Description = "Wyświetlacz: 6.2\", 3200 x 1440px, Dynamic AMOLED\nProcesor: Exynos 990, Ośmiordzeniowy\nWersja systemu: Android 10\nPamięć RAM: 8 GB\nPamięć wbudowana [GB]: 128\nAparat Tylny: 64 Mpx + 2 x 12 Mpx, Przedni 10 Mpx\nKomunikacja: Wi-Fi, NFC, Bluetooth 5.0, USB C\nPojemność akumulatora [mAh]:4000",
                    Warranty = "730", BrandId = 2, TypeId = 2, ItemTags=new List<ItemTag>(){ new ItemTag() { ItemId = 2,TagId = 3 }, new ItemTag() { ItemId = 2, TagId = 4 } }
                },
                new Item() {Id = 3, Name = "AKG Y500 Wireless", Cost= new decimal(549.99),
                    Description = "Budowa słuchawek: nauszne\nŁączność: bezprzewodowe z opcją kabla 3,5mm, Bluetooth\nMikrofon / Regulacja głośności:  tak / tak\nPasmo przenoszenia: 16 - 22000 Hz",
                    Warranty = "365", BrandId = 3, TypeId = 2, ItemTags=new List<ItemTag>(){ new ItemTag() { ItemId = 3,TagId = 5}, new ItemTag() { ItemId = 3, TagId = 6 } }
                }
            };

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllItems()).Returns(itemInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var itemsThatExists = repository.GetAllItems();
            itemsThatExists.Should().NotBeNull();
            itemsThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnItemsFromDb()
        {
            var itemInMemoryDatabase = new List<Item>();

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllItems()).Returns(itemInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var itemsThatExists = repository.GetAllItems();
            itemsThatExists.Should().NotBeNull();
            itemsThatExists.Should().HaveCount(0);
        }       

        [Fact]
        public void CanReturnBrandsFromDb()
        {
            var brandInMemoryDatabase = new List<Brand>
            {
                new Brand() { Id =1, Name = "Expertia" },
                new Brand() { Id =2, Name = "Samsung" },
                new Brand() { Id =3, Name = "AKG" }
            };

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllBrands()).Returns(brandInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var brandsThatExists = repository.GetAllBrands();
            brandsThatExists.Should().NotBeNull();
            brandsThatExists.Should().HaveCount(3);
        }

        [Fact]
        public void CantReturnBrandsFromDb()
        {
            var brandInMemoryDatabase = new List<Brand>();

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllBrands()).Returns(brandInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var brandsThatExists = repository.GetAllBrands();
            brandsThatExists.Should().NotBeNull();
            brandsThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnTypesFromDb()
        {
            var typeInMemoryDatabase = new List<ECommerceApp.Domain.Model.Type>
            {
                new ECommerceApp.Domain.Model.Type() { Id = 1, Name = "Book" },
                new ECommerceApp.Domain.Model.Type() { Id = 2, Name = "Electric" },
            };

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllTypes()).Returns(typeInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var typesThatExists = repository.GetAllTypes();
            typesThatExists.Should().NotBeNull();
            typesThatExists.Should().HaveCount(2);
        }

        [Fact]
        public void CantReturnTypesFromDb()
        {
            var typeInMemoryDatabase = new List<ECommerceApp.Domain.Model.Type>();

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllTypes()).Returns(typeInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var typesThatExists = repository.GetAllTypes();
            typesThatExists.Should().NotBeNull();
            typesThatExists.Should().HaveCount(0);
        }

        [Fact]
        public void CanReturnTagsFromDb()
        {
            var tagInMemoryDatabase = new List<Tag>
            {
                new Tag() { Id = 1, Name = "Książka" },
                new Tag() { Id = 2, Name = "Inwestowanie" },
                new Tag() { Id = 3, Name = "Samsungo" },
                new Tag() { Id = 4, Name = "S20" },
                new Tag() { Id = 5, Name = "AKG" },
                new Tag() { Id = 6, Name = "Y500" },
            };

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllTags()).Returns(tagInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var tagsThatExists = repository.GetAllTags();
            tagsThatExists.Should().NotBeNull();
            tagsThatExists.Should().HaveCount(6);
        }

        [Fact]
        public void CantReturnTagsFromDb()
        {
            var tagInMemoryDatabase = new List<Tag>();

            var mock = new Mock<IItemRepository>();
            mock.Setup(x => x.GetAllTags()).Returns(tagInMemoryDatabase.AsQueryable);
            var repository = mock.Object;

            var tagsThatExists = repository.GetAllTags();
            tagsThatExists.Should().NotBeNull();
            tagsThatExists.Should().HaveCount(0);
        }
    }
}
