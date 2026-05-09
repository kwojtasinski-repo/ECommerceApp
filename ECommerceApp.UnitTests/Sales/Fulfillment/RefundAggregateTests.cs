using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Shared;
using AwesomeAssertions;
using System;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Fulfillment
{
    public class RefundAggregateTests
    {
        private static RefundItem[] DefaultItems() => new[] { RefundItem.Create(1, 2) };

        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnRequestedRefund()
        {
            var items = new[] { RefundItem.Create(10, 3), RefundItem.Create(20, 1) };

            var refund = Refund.Create(99, "Defective product", true, items, "user-1");

            refund.OrderId.Should().Be(99);
            refund.Reason.Should().Be("Defective product");
            refund.OnWarranty.Should().BeTrue();
            refund.Status.Should().Be(RefundStatus.Requested);
            refund.RequestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            refund.ProcessedAt.Should().BeNull();
            refund.Items.Should().HaveCount(2);
            refund.Items[0].ProductId.Should().Be(10);
            refund.Items[0].Quantity.Should().Be(3);
            refund.Items[1].ProductId.Should().Be(20);
            refund.Items[1].Quantity.Should().Be(1);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_InvalidOrderId_ShouldThrowDomainException(int orderId)
        {
            var act = () => Refund.Create(orderId, "reason", false, DefaultItems(), "user-1");

            act.Should().Throw<DomainException>().WithMessage("*OrderId*positive*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_InvalidReason_ShouldThrowDomainException(string reason)
        {
            var act = () => Refund.Create(1, reason!, false, DefaultItems(), "user-1");

            act.Should().Throw<DomainException>().WithMessage("*Reason*required*");
        }

        [Fact]
        public void Create_NullItems_ShouldThrowDomainException()
        {
            var act = () => Refund.Create(1, "reason", false, null!, "user-1");

            act.Should().Throw<DomainException>().WithMessage("*Items*required*");
        }

        [Fact]
        public void Create_EmptyItems_ShouldThrowDomainException()
        {
            var act = () => Refund.Create(1, "reason", false, Array.Empty<RefundItem>(), "user-1");

            act.Should().Throw<DomainException>().WithMessage("*At least one*");
        }

        // ── Approve ───────────────────────────────────────────────────────────

        [Fact]
        public void Approve_RequestedRefund_ShouldSetStatusToApproved()
        {
            var refund = Refund.Create(1, "reason", false, DefaultItems(), "user-1");

            refund.Approve();

            refund.Status.Should().Be(RefundStatus.Approved);
            refund.ProcessedAt.Should().NotBeNull();
            refund.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Approve_AlreadyApprovedRefund_ShouldThrowDomainException()
        {
            var refund = Refund.Create(1, "reason", false, DefaultItems(), "user-1");
            refund.Approve();

            var act = () => refund.Approve();

            act.Should().Throw<DomainException>().WithMessage("*Cannot approve*Approved*");
        }

        [Fact]
        public void Approve_RejectedRefund_ShouldThrowDomainException()
        {
            var refund = Refund.Create(1, "reason", false, DefaultItems(), "user-1");
            refund.Reject();

            var act = () => refund.Approve();

            act.Should().Throw<DomainException>().WithMessage("*Cannot approve*Rejected*");
        }

        // ── Reject ────────────────────────────────────────────────────────────

        [Fact]
        public void Reject_RequestedRefund_ShouldSetStatusToRejected()
        {
            var refund = Refund.Create(1, "reason", false, DefaultItems(), "user-1");

            refund.Reject();

            refund.Status.Should().Be(RefundStatus.Rejected);
            refund.ProcessedAt.Should().NotBeNull();
            refund.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Reject_AlreadyRejectedRefund_ShouldThrowDomainException()
        {
            var refund = Refund.Create(1, "reason", false, DefaultItems(), "user-1");
            refund.Reject();

            var act = () => refund.Reject();

            act.Should().Throw<DomainException>().WithMessage("*Cannot reject*Rejected*");
        }

        [Fact]
        public void Reject_ApprovedRefund_ShouldThrowDomainException()
        {
            var refund = Refund.Create(1, "reason", false, DefaultItems(), "user-1");
            refund.Approve();

            var act = () => refund.Reject();

            act.Should().Throw<DomainException>().WithMessage("*Cannot reject*Approved*");
        }

        // ── RefundItem.Create ─────────────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void RefundItem_Create_InvalidProductId_ShouldThrowDomainException(int productId)
        {
            var act = () => RefundItem.Create(productId, 1);

            act.Should().Throw<DomainException>().WithMessage("*ProductId*positive*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void RefundItem_Create_InvalidQuantity_ShouldThrowDomainException(int quantity)
        {
            var act = () => RefundItem.Create(1, quantity);

            act.Should().Throw<DomainException>().WithMessage("*Quantity*positive*");
        }

        [Fact]
        public void RefundItem_Create_ValidParameters_ShouldSetProperties()
        {
            var item = RefundItem.Create(42, 5);

            item.ProductId.Should().Be(42);
            item.Quantity.Should().Be(5);
        }
    }
}
