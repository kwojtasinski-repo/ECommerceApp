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
                Name = "Test123", Quantity = 100, Warranty = "100", BrandId = 1, Brand = brand, Type = type, TypeId = 1, 
                ItemTags = new List<ECommerceApp.Domain.Model.ItemTag> { new Domain.Model.ItemTag { ItemId = 1, TagId = 1 }, 
                    new Domain.Model.ItemTag { ItemId = 1, TagId = 2 } }};
            context.Add(item);

            var coupontType1 = new ECommerceApp.Domain.Model.CouponType { Id = 1, Type = "Type1" };
            context.Add(coupontType1);
            var coupontType2 = new ECommerceApp.Domain.Model.CouponType { Id = 2, Type = "Type2" };
            context.Add(coupontType2);

            var coupon1 = new ECommerceApp.Domain.Model.Coupon() { Id = 1, Code = "sdgsdg3@GDSG", CouponTypeId = 1 };
            context.Add(coupon1);
            var coupon2 = new ECommerceApp.Domain.Model.Coupon() { Id = 2, Code = "KLNLGNL@$FA", CouponTypeId = 1 };
            context.Add(coupon2);
            var coupon3 = new ECommerceApp.Domain.Model.Coupon() { Id = 3, Code = "2353DGSBH@#", CouponTypeId = 2 };
            context.Add(coupon3);

            var item2 = new ECommerceApp.Domain.Model.Item {Id = 2, Cost = new decimal(100), BrandId = 1, Name = "Item2", Quantity = 100,
                TypeId = 1, Warranty = "123", Description = "TestDescription" };
            context.Add(item2);

            var couponUsed = new ECommerceApp.Domain.Model.CouponUsed { Id = 1, CouponId = 2, OrderId = 1 };
            context.Add(couponUsed);

            var customer = new ECommerceApp.Domain.Model.Customer { Id = 1, FirstName = "TT", LastName = "XD", IsCompany = false, UserId = "123" };
            context.Add(customer);

            var payment = new ECommerceApp.Domain.Model.Payment { Id = 1, CustomerId = 1, DateOfOrderPayment = DateTime.Now, Number = 4123, OrderId = 1 };
            context.Add(payment);

            var order = new ECommerceApp.Domain.Model.Order { Id = 1, CouponUsedId = 1, Cost = new decimal(1000), CustomerId = 1, UserId = "123", IsDelivered = false,
                IsPaid = true, Number = 12345, Ordered = DateTime.Now, PaymentId = 1, OrderItems = new List<Domain.Model.OrderItem> 
                { new Domain.Model.OrderItem { Id=1, ItemId=1, OrderId=1, UserId="123", CouponUsedId=1, ItemOrderQuantity=10 } } };
            context.Add(order);

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
