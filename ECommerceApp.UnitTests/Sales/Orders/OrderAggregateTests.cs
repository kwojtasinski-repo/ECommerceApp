using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderAggregateTests
    {
        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@example.com", "123456789",
            false, null, null,
            "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnOrderWithPlacedStatus()
        {
            var number = OrderNumber.Generate();
            var order = Order.Create(1, 1, "user1", number, CreateCustomer());

            order.CustomerId.Should().Be(1);
            order.CurrencyId.Should().Be(1);
            order.UserId.Value.Should().Be("user1");
            order.Number.Value.Should().StartWith("ORD-");
            order.Cost.Should().Be(0);
            order.Status.Should().Be(OrderStatus.Placed);
            order.DiscountPercent.Should().BeNull();
            order.OrderItems.Should().BeEmpty();
            order.Events.Should().ContainSingle(e => e.EventType == OrderEventType.OrderPlaced);
        }

        [Fact]
        public void Create_ZeroCustomerId_ShouldThrowDomainException()
        {
            var act = () => Order.Create(0, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            act.Should().Throw<DomainException>().WithMessage("*CustomerId*positive*");
        }

        [Fact]
        public void Create_ZeroCurrencyId_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 0, "user1", OrderNumber.Generate(), CreateCustomer());

            act.Should().Throw<DomainException>().WithMessage("*CurrencyId*positive*");
        }

        [Fact]
        public void Create_EmptyUserId_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 1, "", OrderNumber.Generate(), CreateCustomer());

            act.Should().Throw<DomainException>().WithMessage("*UserId*required*");
        }

        [Fact]
        public void Create_NullNumber_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 1, "user1", null!, CreateCustomer());

            act.Should().Throw<DomainException>().WithMessage("*number*required*");
        }

        [Fact]
        public void Create_NullCustomer_ShouldThrowDomainException()
        {
            var act = () => Order.Create(1, 1, "user1", OrderNumber.Generate(), null!);

            act.Should().Throw<DomainException>().WithMessage("*customer*required*");
        }

        // ── CalculateCost ─────────────────────────────────────────────────────

        [Fact]
        public void CalculateCost_WithItems_ShouldSumUnitCostAmountTimesQuantity()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(OrderItem.Create(1, 2, new UnitCost(10m), "user1")); // 20
            order.AddItem(OrderItem.Create(2, 3, new UnitCost(5m), "user1"));  // 15

            order.CalculateCost();

            order.Cost.Should().Be(35m);
        }

        [Fact]
        public void CalculateCost_WithNoItems_ShouldBeZero()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            order.CalculateCost();

            order.Cost.Should().Be(0);
        }

        [Fact]
        public void CalculateCost_WithDiscountPercent_ShouldApplyDiscount()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(OrderItem.Create(1, 1, new UnitCost(100m), "user1"));
            order.AssignCoupon(1, 10); // 10% discount

            order.Cost.Should().Be(90m);
        }

        // ── ConfirmPayment ────────────────────────────────────────────────────

        [Fact]
        public void ConfirmPayment_PlacedOrder_ShouldTransitionToPaymentConfirmedAndAppendEvent()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            order.ConfirmPayment(5);

            order.Status.Should().Be(OrderStatus.PaymentConfirmed);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.OrderPaymentConfirmed);
        }

        [Fact]
        public void ConfirmPayment_AlreadyPaidOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.ConfirmPayment(5);

            var act = () => order.ConfirmPayment(6);

            act.Should().Throw<DomainException>().WithMessage("*cannot confirm payment*");
        }

        [Fact]
        public void ConfirmPayment_ZeroPaymentId_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            var act = () => order.ConfirmPayment(0);

            act.Should().Throw<DomainException>().WithMessage("*PaymentId*positive*");
        }

        // ── Fulfill ───────────────────────────────────────────────────────────

        [Fact]
        public void Fulfill_PaymentConfirmedOrder_ShouldTransitionToFulfilledAndAppendEvent()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.ConfirmPayment(5);

            order.Fulfill();

            order.Status.Should().Be(OrderStatus.Fulfilled);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.OrderFulfilled);
        }

        [Fact]
        public void Fulfill_UnpaidOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            var act = () => order.Fulfill();

            act.Should().Throw<DomainException>().WithMessage("*cannot be fulfilled*");
        }

        [Fact]
        public void Fulfill_AlreadyFulfilledOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.ConfirmPayment(5);
            order.Fulfill();

            var act = () => order.Fulfill();

            act.Should().Throw<DomainException>().WithMessage("*cannot be fulfilled*");
        }

        // ── ExpirePayment ─────────────────────────────────────────────────────

        [Fact]
        public void ExpirePayment_PlacedOrder_ShouldTransitionToCancelledAndAppendEvent()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            order.ExpirePayment();

            order.Status.Should().Be(OrderStatus.Cancelled);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.OrderPaymentExpired);
        }

        [Fact]
        public void ExpirePayment_AlreadyCancelledOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.ExpirePayment();

            var act = () => order.ExpirePayment();

            act.Should().Throw<DomainException>().WithMessage("*cannot expire*");
        }

        // ── AssignCoupon ──────────────────────────────────────────────────────

        [Fact]
        public void AssignCoupon_ValidDiscount_ShouldSetCouponAndRecalculateCost()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(OrderItem.Create(1, 1, new UnitCost(200m), "user1"));

            order.AssignCoupon(7, 25);

            order.CouponUsedId.Should().Be(7);
            order.DiscountPercent.Should().Be(25);
            order.Cost.Should().Be(150m);
            order.OrderItems[0].CouponUsedId.Should().Be(7);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.CouponApplied);
        }

        [Fact]
        public void AssignCoupon_DiscountAbove100_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            var act = () => order.AssignCoupon(1, 101);

            act.Should().Throw<DomainException>().WithMessage("*0 and 100*");
        }

        [Fact]
        public void AssignCoupon_NegativeDiscount_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            var act = () => order.AssignCoupon(1, -1);

            act.Should().Throw<DomainException>().WithMessage("*0 and 100*");
        }

        // ── RemoveCoupon ──────────────────────────────────────────────────────

        [Fact]
        public void RemoveCoupon_WithCouponAssigned_ShouldClearCouponAndRecalculate()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.AddItem(OrderItem.Create(1, 1, new UnitCost(100m), "user1"));
            order.AssignCoupon(3, 20);

            order.RemoveCoupon();

            order.CouponUsedId.Should().BeNull();
            order.DiscountPercent.Should().BeNull();
            order.Cost.Should().Be(100m);
            order.OrderItems[0].CouponUsedId.Should().BeNull();
            order.Events.Should().Contain(e => e.EventType == OrderEventType.CouponRemoved);
        }

        [Fact]
        public void RemoveCoupon_NoCouponAssigned_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            var act = () => order.RemoveCoupon();

            act.Should().Throw<DomainException>().WithMessage("*no coupon*");
        }

        // ── AssignRefund ──────────────────────────────────────────────────────

        [Fact]
        public void AssignRefund_ValidRefundId_ShouldAppendRefundAssignedEvent()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            order.AssignRefund(9);

            order.Events.Should().Contain(e => e.EventType == OrderEventType.RefundAssigned);
        }

        [Fact]
        public void AssignRefund_ZeroRefundId_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            var act = () => order.AssignRefund(0);

            act.Should().Throw<DomainException>().WithMessage("*RefundId*positive*");
        }

        // ── RemoveRefund ──────────────────────────────────────────────────────

        [Fact]
        public void RemoveRefund_ShouldAppendRefundRemovedEvent()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.AssignRefund(9);

            order.RemoveRefund();

            order.Events.Should().Contain(e => e.EventType == OrderEventType.RefundRemoved);
        }

        // ── Cancel ────────────────────────────────────────────────────────────

        [Fact]
        public void Cancel_PlacedOrder_ShouldTransitionToCancelledAndAppendEvent()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());

            order.Cancel("CustomerRequest");

            order.Status.Should().Be(OrderStatus.Cancelled);
            order.Events.Should().Contain(e => e.EventType == OrderEventType.OrderCancelled);
        }

        [Fact]
        public void Cancel_AlreadyCancelledOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.Cancel("CustomerRequest");

            var act = () => order.Cancel("CustomerRequest");

            act.Should().Throw<DomainException>().WithMessage("*cannot be cancelled*");
        }

        [Fact]
        public void Cancel_PaidOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.ConfirmPayment(1);

            var act = () => order.Cancel("CustomerRequest");

            act.Should().Throw<DomainException>().WithMessage("*cannot be cancelled*");
        }

        [Fact]
        public void Cancel_FulfilledOrder_ShouldThrowDomainException()
        {
            var order = Order.Create(1, 1, "user1", OrderNumber.Generate(), CreateCustomer());
            order.ConfirmPayment(1);
            order.Fulfill();

            var act = () => order.Cancel("CustomerRequest");

            act.Should().Throw<DomainException>().WithMessage("*cannot be cancelled*");
        }
    }
}
