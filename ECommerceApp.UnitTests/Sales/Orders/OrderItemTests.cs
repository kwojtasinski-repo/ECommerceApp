using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Orders
{
    public class OrderItemTests
    {
        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnOrderItem()
        {
            var item = OrderItem.Create(1, 3, new UnitCost(19.99m), "user1");

            item.ItemId.Value.Should().Be(1);
            item.Quantity.Should().Be(3);
            item.UnitCost.Amount.Should().Be(19.99m);
            item.UserId.Value.Should().Be("user1");
            item.OrderId.Should().BeNull();
            item.CouponUsedId.Should().BeNull();
            item.Snapshot.Should().BeNull();
        }

        [Fact]
        public void Create_ZeroItemId_ShouldThrowDomainException()
        {
            var act = () => OrderItem.Create(0, 1, new UnitCost(10m), "user1");

            act.Should().Throw<DomainException>().WithMessage("*ItemId*positive*");
        }

        [Fact]
        public void Create_ZeroQuantity_ShouldThrowDomainException()
        {
            var act = () => OrderItem.Create(1, 0, new UnitCost(10m), "user1");

            act.Should().Throw<DomainException>().WithMessage("*Quantity*positive*");
        }

        [Fact]
        public void Create_NegativeUnitCost_ShouldThrowDomainException()
        {
            var act = () => OrderItem.Create(1, 1, new UnitCost(-1m), "user1");

            act.Should().Throw<DomainException>().WithMessage("*UnitCost*negative*");
        }

        [Fact]
        public void Create_EmptyUserId_ShouldThrowDomainException()
        {
            var act = () => OrderItem.Create(1, 1, new UnitCost(10m), "");

            act.Should().Throw<DomainException>().WithMessage("*UserId*required*");
        }

        // ── SetSnapshot ───────────────────────────────────────────────────────

        [Fact]
        public void SetSnapshot_ValidSnapshot_ShouldSetSnapshot()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");
            var snapshot = new OrderProductSnapshot("Widget", "image.jpg", "/api/images/1");

            item.SetSnapshot(snapshot);

            item.Snapshot!.ProductName.Should().Be("Widget");
            item.Snapshot.ImageFileName.Should().Be("image.jpg");
            item.Snapshot.ImageUrl.Should().Be("/api/images/1");
        }

        [Fact]
        public void SetSnapshot_NullSnapshot_ShouldThrowDomainException()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");

            var act = () => item.SetSnapshot(null!);

            act.Should().Throw<DomainException>().WithMessage("*snapshot*required*");
        }

        // ── UpdateQuantity ────────────────────────────────────────────────────

        [Fact]
        public void UpdateQuantity_ValidQuantity_ShouldUpdateQuantity()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");

            item.UpdateQuantity(5);

            item.Quantity.Should().Be(5);
        }

        [Fact]
        public void UpdateQuantity_ZeroQuantity_ShouldThrowDomainException()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");

            var act = () => item.UpdateQuantity(0);

            act.Should().Throw<DomainException>().WithMessage("*Quantity*positive*");
        }

        // ── ApplyCoupon ───────────────────────────────────────────────────────

        [Fact]
        public void ApplyCoupon_ValidCouponUsedId_ShouldSetCouponUsedId()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");

            item.ApplyCoupon(3);

            item.CouponUsedId.Should().Be(3);
        }

        [Fact]
        public void ApplyCoupon_ZeroCouponUsedId_ShouldThrowDomainException()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");

            var act = () => item.ApplyCoupon(0);

            act.Should().Throw<DomainException>().WithMessage("*CouponUsedId*positive*");
        }

        // ── RemoveCoupon ──────────────────────────────────────────────────────

        [Fact]
        public void RemoveCoupon_AfterApply_ShouldClearCouponUsedId()
        {
            var item = OrderItem.Create(1, 1, new UnitCost(10m), "user1");
            item.ApplyCoupon(3);

            item.RemoveCoupon();

            item.CouponUsedId.Should().BeNull();
        }
    }
}
