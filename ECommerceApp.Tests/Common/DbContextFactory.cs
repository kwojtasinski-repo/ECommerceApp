using ECommerceApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Tests.Common
{
    public static class DbContextFactory
    {
        public static Mock<Context> Create()
        {
            var options = new DbContextOptionsBuilder<Context>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

            var mock = new Mock<Context>(options) { CallBase = true };

            var context = mock.Object;

            context.Database.EnsureCreated(); // sprawdzam czy baza jest utworzona

            // -------------- zestaw danych -------------- (bierze pod uwage seed)

            var brand = new ECommerceApp.Domain.Model.Brand { Id = 1, Name = "BrandTest" };

            context.Add(brand);

            var type = new ECommerceApp.Domain.Model.Type { Id = 1, Name = "TypeTest" };

            context.Add(type);

            var tag1 = new ECommerceApp.Domain.Model.Tag { Id = 1, Name = "Tag1" };
            context.Add(tag1);
            var tag2 = new ECommerceApp.Domain.Model.Tag { Id = 2, Name = "Tag2" };
            context.Add(tag2);

            var image1 = new ECommerceApp.Domain.Model.Image { Id = 1, ItemId = 1, Name = "Img1.jpg", SourcePath = "Test1" };
            context.Add(image1);
            var image2 = new ECommerceApp.Domain.Model.Image { Id = 2, ItemId = 1, Name = "Img2.jpg", SourcePath = "Test2" };
            context.Add(image2);

            var item = new ECommerceApp.Domain.Model.Item { Id = 1, Cost = new decimal(100), Description = "testowy przedmiot", 
                Name = "Test123", Quantity = 10, Warranty = "100", BrandId = 1, Brand = brand, Type = type, TypeId = 1, 
                ItemTags = new List<ECommerceApp.Domain.Model.ItemTag> { new Domain.Model.ItemTag { ItemId = 1, TagId = 1 }, 
                    new Domain.Model.ItemTag { ItemId = 1, TagId = 2 } }};
            context.Add(item);

            // -------------- zestaw danych -------------- 

            context.SaveChanges();

            foreach (var entity in context.ChangeTracker.Entries())
            {
                entity.State = EntityState.Detached; // stan ustawiony dla testow, aby entity framework nie "trackowal", uciazliwe podczas update
            }

            return mock;
        }

        public static void Destroy(Context context)
        {
            context.Database.EnsureDeleted();
            context.Dispose();
        }
    }
}
