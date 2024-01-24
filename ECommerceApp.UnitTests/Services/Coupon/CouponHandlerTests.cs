using ECommerceApp.Domain.Model;
using System.Collections.Generic;
using System;
using ECommerceApp.Application.Services.Coupons;
using ECommerceApp.Domain.Interface;
using Moq;
using ECommerceApp.Application.ViewModels.Coupon;
using Xunit;
using ECommerceApp.Application.Exceptions;
using FluentAssertions;
using System.Linq;

namespace ECommerceApp.UnitTests.Services.Coupon
{
    public class CouponHandlerTests
    {
        private readonly Mock<ICouponRepository> couponRepository;
        private readonly Mock<ICouponUsedRepository> couponUsedRepository;

        public CouponHandlerTests()
        {
            couponRepository = new Mock<ICouponRepository>();
            couponUsedRepository = new Mock<ICouponUsedRepository>();
        }

        private CouponHandler CreateCouponHandler()
            => new(couponRepository.Object, couponUsedRepository.Object);

        [Fact]
        public void given_null_order_when_handle_changes_on_order_should_throw_an_exception()
        {
            var handler = CreateCouponHandler();

            Action action = () => handler.HandleCouponChangesOnOrder(null, null, null);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("order cannot be null");
        }

        [Fact]
        public void given_null_dto_when_handle_changes_on_order_should_throw_an_exception()
        {
            var handler = CreateCouponHandler();

            Action action = () => handler.HandleCouponChangesOnOrder(null, CreateDefaultOrder(), null);
            
            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("dto cannot be null");
        }

        [Fact]
        public void given_order_without_coupon_empty_dto_and_null_coupon_when_handle_changes_on_order_should_not_do_anything()
        {
            var order = CreateDefaultOrder();
            var dto = HandleCouponChangesDto.Of();
            var handler = CreateCouponHandler();

            handler.HandleCouponChangesOnOrder(null, order, dto);

            couponUsedRepository.Verify(c => c.Delete(It.IsAny<int>()), Times.Never);
            couponUsedRepository.Verify(c => c.Add(It.IsAny<Domain.Model.CouponUsed>()), Times.Never);
            couponUsedRepository.Verify(c => c.Update(It.IsAny<Domain.Model.CouponUsed>()), Times.Never);
        }

        //TODO: Consider change logic in handler, should take I think only 2 objects order and dto, condition is probably invalid with those values
        [Fact]
        public void given_order_without_coupon_dto_with_coupon_and_null_coupon_when_handle_changes_on_order_should_not_do_anything()
        {
            var order = CreateDefaultOrder();
            var dto = HandleCouponChangesDto.Of(1);
            var handler = CreateCouponHandler();

            handler.HandleCouponChangesOnOrder(null, order, dto);

            couponUsedRepository.Verify(c => c.Delete(It.IsAny<int>()), Times.Never);
            couponUsedRepository.Verify(c => c.Add(It.IsAny<Domain.Model.CouponUsed>()), Times.Never);
            couponUsedRepository.Verify(c => c.Update(It.IsAny<Domain.Model.CouponUsed>()), Times.Never);
        }

        [Fact]
        public void given_order_without_coupon_empty_dto_and_coupon_with_values_when_handle_changes_on_order_should_not_do_anything()
        {
            var order = CreateDefaultOrder();
            var coupon = CreateCoupon();
            var dto = HandleCouponChangesDto.Of();
            var handler = CreateCouponHandler();

            handler.HandleCouponChangesOnOrder(coupon, order, dto);

            couponUsedRepository.Verify(c => c.Delete(It.IsAny<int>()), Times.Never);
            couponUsedRepository.Verify(c => c.Add(It.IsAny<Domain.Model.CouponUsed>()), Times.Never);
            couponUsedRepository.Verify(c => c.Update(It.IsAny<Domain.Model.CouponUsed>()), Times.Never);
        }

        [Fact]
        public void given_order_with_coupon_empty_dto_and_null_coupon_when_handle_changes_on_order_should_delete_coupon_from_order()
        {
            var order = CreateDefaultOrder();
            order.CouponUsed = CreateDefaultCouponUsed();
            order.CouponUsedId = order.CouponUsed.Id;
            var dto = HandleCouponChangesDto.Of();
            var handler = CreateCouponHandler();

            handler.HandleCouponChangesOnOrder(null, order, dto);

            order.CouponUsedId.Should().BeNull();
            order.CouponUsed.Should().BeNull();
        }

        [Fact]
        public void given_order_with_coupon_and_order_items_empty_dto_and_null_coupon_when_handle_changes_on_order_should_delete_coupon_from_order_and_order_items()
        {
            var order = CreateDefaultOrder();
            order.CouponUsed = CreateDefaultCouponUsed();
            order.CouponUsedId = order.CouponUsed.Id;
            var orderItems = new List<Domain.Model.OrderItem> { CreateDefaultOrderItem(), CreateDefaultOrderItem() };
            orderItems.ForEach(oi => { oi.CouponUsed = order.CouponUsed; oi.CouponUsedId = order.CouponUsedId; });
            order.OrderItems = orderItems;
            var dto = HandleCouponChangesDto.Of();
            var handler = CreateCouponHandler();

            handler.HandleCouponChangesOnOrder(null, order, dto);

            order.CouponUsedId.Should().BeNull();
            order.CouponUsed.Should().BeNull();
            order.OrderItems.All(oi => oi.CouponUsed is null && oi.CouponUsedId is null).Should().BeTrue();
        }

        [Fact]
        public void given_not_existing_coupon_when_handle_changes_on_order_should_throw_an_exception()
        {
            var order = CreateDefaultOrder();
            var dto = HandleCouponChangesDto.Of("abc");
            var coupon = CreateCoupon();
            var handler = CreateCouponHandler();

            Action action = () => handler.HandleCouponChangesOnOrder(coupon, order, dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains($"Coupon with id '{coupon.Id}' was not found");
        }

        [Fact]
        public void given_coupon_used_when_handle_changes_on_order_should_throw_an_exception()
        {
            var order = CreateDefaultOrder();
            var dto = HandleCouponChangesDto.Of("abc");
            var coupon = CreateCoupon();
            AddCoupon(new Domain.Model.Coupon { Id = coupon.Id, CouponUsedId = 1 });
            var handler = CreateCouponHandler();

            Action action = () => handler.HandleCouponChangesOnOrder(coupon, order, dto);

            action.Should().ThrowExactly<BusinessException>().Which.Message.Contains("Cannot assign used coupon");
        }

        [Fact]
        public void given_order_with_order_items_without_coupon_dto_with_new_coupon_when_handle_changes_on_order_should_add_coupon_to_order()
        {
            var order = CreateDefaultOrder();
            var orderItems = new List<Domain.Model.OrderItem> { CreateDefaultOrderItem(), CreateDefaultOrderItem() };
            orderItems.ForEach(oi => { oi.CouponUsed = order.CouponUsed; oi.CouponUsedId = order.CouponUsedId; });
            order.OrderItems = orderItems;
            var dto = HandleCouponChangesDto.Of("abc");
            var coupon = CreateCoupon();
            AddCoupon(new Domain.Model.Coupon { Id = coupon.Id, Discount = coupon.Discount });
            var handler = CreateCouponHandler();
            var currentCost = 200M;

            handler.HandleCouponChangesOnOrder(coupon, order, dto);

            order.CouponUsedId.Should().NotBeNull();
            order.CouponUsed.Should().NotBeNull();
            order.OrderItems.All(oi => oi.CouponUsed is not null && oi.CouponUsedId is not null).Should().BeTrue();
            order.Cost.Should().BeLessThan(currentCost);
            couponUsedRepository.Verify(c => c.Add(It.IsAny<Domain.Model.CouponUsed>()), Times.Once);
            couponRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Coupon>()), Times.Once);
        }

        [Fact]
        public void given_order_with_order_items_and_coupon_dto_with_new_coupon_when_handle_changes_on_order_should_add_coupon_to_order()
        {
            var order = CreateDefaultOrder();
            order.CouponUsed = CreateDefaultCouponUsed();
            order.CouponUsedId = order.CouponUsed.Id;
            var orderItems = new List<Domain.Model.OrderItem> { CreateDefaultOrderItem(), CreateDefaultOrderItem() };
            orderItems.ForEach(oi => { oi.CouponUsed = order.CouponUsed; oi.CouponUsedId = order.CouponUsedId; });
            order.OrderItems = orderItems;
            var dto = HandleCouponChangesDto.Of("abc");
            var coupon = CreateCoupon();
            AddCoupon(new Domain.Model.Coupon { Id = coupon.Id, Discount = coupon.Discount });
            var handler = CreateCouponHandler();
            couponUsedRepository.Setup(c => c.Delete(order.CouponUsedId.Value)).Returns(true);
            var currentCost = 200M;

            handler.HandleCouponChangesOnOrder(coupon, order, dto);

            order.CouponUsedId.Should().NotBeNull();
            order.CouponUsed.Should().NotBeNull();
            order.OrderItems.All(oi => oi.CouponUsed is not null && oi.CouponUsedId is not null).Should().BeTrue();
            order.Cost.Should().BeLessThan(currentCost);
            couponUsedRepository.Verify(c => c.Delete(It.IsAny<int>()), Times.Once);
            couponUsedRepository.Verify(c => c.Add(It.IsAny<Domain.Model.CouponUsed>()), Times.Once);
            couponRepository.Verify(c => c.Update(It.IsAny<Domain.Model.Coupon>()), Times.Once);
        }

        private static CouponVm CreateCoupon()
        {
            return new CouponVm
            {
                Id = new Random().Next(1, 9999),
                Code = "123",
                CouponTypeId = 1,
                Description = "Description",
                Discount = new Random().Next(10, 30),
            };
        }

        private void AddCoupon(Domain.Model.Coupon coupon)
        {
            couponRepository.Setup(c => c.GetById(coupon.Id)).Returns(coupon);
            couponRepository.Setup(c => c.GetCouponById(coupon.Id)).Returns(coupon);
        }

        private static Domain.Model.CouponUsed CreateDefaultCouponUsed()
        {
            var coupon = new Domain.Model.Coupon
            {
                Id = new Random().Next(1, 9999),
                Code = Guid.NewGuid().ToString(),
                CouponTypeId = 1,
                Description = "Test",
                Discount = new Random().Next(10, 30),
            };
            return new Domain.Model.CouponUsed
            {
                Id = new Random().Next(1, 9999),
                CouponId = coupon.Id,
                Coupon = coupon
            };
        }

        private static Domain.Model.Order CreateDefaultOrder()
        {
            var order = new Domain.Model.Order
            {
                Id = new Random().Next(1, 9999),
                Number = "1234557567",
                Cost = new decimal(100),
                Ordered = DateTime.Now,
                IsPaid = false,
                IsDelivered = false,
                Delivered = null,
                CustomerId = 1,
                CurrencyId = 1,
                UserId = Guid.NewGuid().ToString(),
                OrderItems = new List<Domain.Model.OrderItem>(),
                Currency = new Domain.Model.Currency() { Id = 1 },
                Customer = new Domain.Model.Customer() { Id = 1 }
            };
            order.User = new ApplicationUser { Id = order.UserId };
            return order;
        }


        private static Domain.Model.OrderItem CreateDefaultOrderItem()
        {
            return new Domain.Model.OrderItem
            {
                Id = new Random().Next(1, 999),
                ItemId = 1,
                Item = new Item
                {
                    Id = 1,
                    Cost = 100M
                },
            };
        }
    }
}
