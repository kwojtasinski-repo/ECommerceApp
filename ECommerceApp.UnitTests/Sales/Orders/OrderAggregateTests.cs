using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.Events;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderAggregateTests
    {
        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnOrder()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            order.CustomerId.Should().Be(1);
            order.CurrencyId.Should().Be(1);
            order.UserId.Should().Be("user1");
            order.Number.Should().Be("ORD-001");
            order.Cost.Should().Be(0);
            order.IsPaid.Should().BeFalse();
            order.IsDelivered.Should().BeFalse();
            order.DiscountPercent.Should().BeNull();
            order.OrderItems.Should().BeEmpty();
        }

        [Fact]
        public void Create_ZeroCustomerId_ShouldThrowDomainException()
        {
            var act = () => Order.Create(0, 1, "user1", "ORD-001");

            act.Should().Throw<DomainException>().WithMessage("*CustomerId*positive*");
        }

        [Fact]
        public void Create_ZeroCurrencyId_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 0, "user1", "ORD-001");

            act.Should().Throw<DomainException>().WithMessage("*CurrencyId*positive*");
        }

        [Fact]
        public void Create_EmptyUserId_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 1, "", "ORD-001");

            act.Should().Throw<DomainException>().WithMessage("*UserId*required*");
        }

        [Fact]
        public void Create_EmptyNumber_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 1, "user1", "");

            act.Should().Throw<DomainException>().WithMessage("*number*required*");
        }

        [Fact]
        public void Create_NegativeCost_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 1, "user1", "ORD-001", -1m);

            act.Should().Throw<DomainException>().WithMessage("*Cost*negative*");
        }

        // ── CalculateCost ─────────────────────────────────────────────────────

        [Fact]
        public void CalculateCost_WithItems_ShouldSumUnitCostTimesQuantity()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.AddItem(OrderItem.Create(1, 2, 10m, "user1")); // 20
            order.AddItem(OrderItem.Create(2, 3, 5m, "user1"));  // 15

            order.CalculateCost();

            order.Cost.Should().Be(35m);
        }

        [Fact]
        public void CalculateCost_WithNoItems_ShouldBeZero()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            order.CalculateCost();

            order.Cost.Should().Be(0);
        }

        [Fact]
        public void CalculateCost_WithDiscountPercent_ShouldApplyDiscount()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.AddItem(OrderItem.Create(1, 1, 100m, "user1"));
            order.AssignCoupon(1, 10); // 10% discount

            order.Cost.Should().Be(90m); // AssignCoupon calls CalculateCost internally
        }

        // ── MarkAsPaid ────────────────────────────────────────────────────────

        [Fact]
        public void MarkAsPaid_UnpaidOrder_ShouldSetPaidAndReturnEvent()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            var @event = order.MarkAsPaid(5);

            order.IsPaid.Should().BeTrue();
            order.PaymentId.Should().Be(5);
            @event.Should().BeOfType<OrderPaid>();
            @event.PaymentId.Should().Be(5);
        }

        [Fact]
        public void MarkAsPaid_AlreadyPaidOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.MarkAsPaid(5);

            var act = () => order.MarkAsPaid(6);

            act.Should().Throw<DomainException>().WithMessage("*already paid*");
        }

        [Fact]
        public void MarkAsPaid_ZeroPaymentId_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            var act = () => order.MarkAsPaid(0);

            act.Should().Throw<DomainException>().WithMessage("*PaymentId*positive*");
        }

        // ── MarkAsDelivered ───────────────────────────────────────────────────

        [Fact]
        public void MarkAsDelivered_PaidOrder_ShouldSetDeliveredAndReturnEvent()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.MarkAsPaid(5);

            var @event = order.MarkAsDelivered();

            order.IsDelivered.Should().BeTrue();
            order.Delivered.Should().NotBeNull();
            @event.Should().BeOfType<OrderDelivered>();
        }

        [Fact]
        public void MarkAsDelivered_UnpaidOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            var act = () => order.MarkAsDelivered();

            act.Should().Throw<DomainException>().WithMessage("*not paid*");
        }

        [Fact]
        public void MarkAsDelivered_AlreadyDeliveredOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.MarkAsPaid(5);
            order.MarkAsDelivered();

            var act = () => order.MarkAsDelivered();

            act.Should().Throw<DomainException>().WithMessage("*already delivered*");
        }

        // ── AssignCoupon ──────────────────────────────────────────────────────

        [Fact]
        public void AssignCoupon_ValidDiscount_ShouldSetCouponAndRecalculateCost()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.AddItem(OrderItem.Create(1, 1, 200m, "user1"));

            order.AssignCoupon(7, 25);

            order.CouponUsedId.Should().Be(7);
            order.DiscountPercent.Should().Be(25);
            order.Cost.Should().Be(150m); // 200 * 0.75
            order.OrderItems[0].CouponUsedId.Should().Be(7);
        }

        [Fact]
        public void AssignCoupon_DiscountAbove100_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            var act = () => order.AssignCoupon(1, 101);

            act.Should().Throw<DomainException>().WithMessage("*0 and 100*");
        }

        [Fact]
        public void AssignCoupon_NegativeDiscount_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            var act = () => order.AssignCoupon(1, -1);

            act.Should().Throw<DomainException>().WithMessage("*0 and 100*");
        }

        // ── RemoveCoupon ──────────────────────────────────────────────────────

        [Fact]
        public void RemoveCoupon_WithCouponAssigned_ShouldClearCouponAndRecalculate()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.AddItem(OrderItem.Create(1, 1, 100m, "user1"));
            order.AssignCoupon(3, 20);

            order.RemoveCoupon();

            order.CouponUsedId.Should().BeNull();
            order.DiscountPercent.Should().BeNull();
            order.Cost.Should().Be(100m);
            order.OrderItems[0].CouponUsedId.Should().BeNull();
        }

        // ── AssignRefund ──────────────────────────────────────────────────────

        [Fact]
        public void AssignRefund_ValidRefundId_ShouldSetRefundOnOrderAndItems()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.AddItem(OrderItem.Create(1, 1, 50m, "user1"));

            order.AssignRefund(9);

            order.RefundId.Should().Be(9);
            order.OrderItems[0].RefundId.Should().Be(9);
        }

        [Fact]
        public void AssignRefund_ZeroRefundId_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");

            var act = () => order.AssignRefund(0);

            act.Should().Throw<DomainException>().WithMessage("*RefundId*positive*");
        }

        // ── RemoveRefund ──────────────────────────────────────────────────────

        [Fact]
        public void RemoveRefund_WithRefundAssigned_ShouldClearRefundOnOrderAndItems()
        {
            var order = Order.Create(1, 1, "user1", "ORD-001");
            order.AddItem(OrderItem.Create(1, 1, 50m, "user1"));
            order.AssignRefund(9);

            order.RemoveRefund();

            order.RefundId.Should().BeNull();
            order.OrderItems[0].RefundId.Should().BeNull();
        }
    }
}
