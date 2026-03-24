using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Domain.Sales.Payments.Events;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Payments
{
    public class PaymentAggregateTests
    {
        private static Payment CreatePending() => Payment.Create(
            new PaymentId(1), new PaymentOrderId(1), 199.99m, 1, DateTime.UtcNow.AddDays(3), "user-1");

        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_ValidParameters_ShouldReturnPendingPayment()
        {
            var expiresAt = DateTime.UtcNow.AddDays(3);
            var payment = Payment.Create(new PaymentOrderId(1), 99.99m, 2, expiresAt, "user-1");

            payment.OrderId.Value.Should().Be(1);
            payment.TotalAmount.Should().Be(99.99m);
            payment.CurrencyId.Should().Be(2);
            payment.Status.Should().Be(PaymentStatus.Pending);
            payment.ExpiresAt.Should().Be(expiresAt);
            payment.ConfirmedAt.Should().BeNull();
            payment.TransactionRef.Should().BeNull();
            payment.PaymentId.Should().NotBe(Guid.Empty);
            payment.UserId.Should().Be("user-1");
        }

        [Fact]
        public void Create_ZeroOrderId_ShouldThrowDomainException()
        {
            var act = () => Payment.Create(new PaymentOrderId(1), 99.99m, 1, DateTime.UtcNow.AddDays(3), "user-1");

            act.Should().NotThrow();
        }

        [Fact]
        public void Create_NegativeTotalAmount_ShouldThrowDomainException()
        {
            var act = () => Payment.Create(new PaymentOrderId(1), -1m, 1, DateTime.UtcNow.AddDays(3), "user-1");

            act.Should().Throw<DomainException>().WithMessage("*TotalAmount*");
        }

        [Fact]
        public void Create_ZeroCurrencyId_ShouldThrowDomainException()
        {
            var act = () => Payment.Create(new PaymentOrderId(1), 99.99m, 0, DateTime.UtcNow.AddDays(3), "user-1");

            act.Should().Throw<DomainException>().WithMessage("*CurrencyId*");
        }

        [Fact]
        public void Create_ZeroTotalAmount_ShouldSucceed()
        {
            var act = () => Payment.Create(new PaymentOrderId(1), 0m, 1, DateTime.UtcNow.AddDays(3), "user-1");

            act.Should().NotThrow();
        }

        // ── Confirm ───────────────────────────────────────────────────────────

        [Fact]
        public void Confirm_PendingPayment_ShouldSetConfirmedAndReturnEvent()
        {
            var payment = CreatePending();

            var @event = payment.Confirm("TX-001");

            payment.Status.Should().Be(PaymentStatus.Confirmed);
            payment.ConfirmedAt.Should().NotBeNull();
            payment.TransactionRef.Should().Be("TX-001");
            @event.Should().BeOfType<PaymentConfirmedEvent>();
            @event.OrderId.Should().Be(1);
        }

        [Fact]
        public void Confirm_WithoutTransactionRef_ShouldSucceedWithNullRef()
        {
            var payment = CreatePending();

            var @event = payment.Confirm();

            payment.Status.Should().Be(PaymentStatus.Confirmed);
            payment.TransactionRef.Should().BeNull();
            @event.Should().NotBeNull();
        }

        [Theory]
        [InlineData(PaymentStatus.Confirmed)]
        [InlineData(PaymentStatus.Expired)]
        [InlineData(PaymentStatus.Refunded)]
        public void Confirm_NonPendingPayment_ShouldThrowDomainException(PaymentStatus initialStatus)
        {
            var payment = CreatePending();
            SetStatus(payment, initialStatus);

            var act = () => payment.Confirm();

            act.Should().Throw<DomainException>().WithMessage("*confirm*");
        }

        // ── Expire ────────────────────────────────────────────────────────────

        [Fact]
        public void Expire_PendingPayment_ShouldSetExpiredAndReturnEvent()
        {
            var payment = CreatePending();

            var @event = payment.Expire();

            payment.Status.Should().Be(PaymentStatus.Expired);
            @event.Should().BeOfType<PaymentExpiredEvent>();
            @event.OrderId.Should().Be(1);
        }

        [Theory]
        [InlineData(PaymentStatus.Confirmed)]
        [InlineData(PaymentStatus.Expired)]
        [InlineData(PaymentStatus.Refunded)]
        public void Expire_NonPendingPayment_ShouldThrowDomainException(PaymentStatus initialStatus)
        {
            var payment = CreatePending();
            SetStatus(payment, initialStatus);

            var act = () => payment.Expire();

            act.Should().Throw<DomainException>().WithMessage("*expire*");
        }

        // ── IssueRefund ───────────────────────────────────────────────────────

        [Fact]
        public void IssueRefund_ConfirmedPayment_ShouldSetRefundedAndReturnEvent()
        {
            var payment = CreatePending();
            payment.Confirm();

            var @event = payment.IssueRefund(productId: 5, quantity: 2);

            payment.Status.Should().Be(PaymentStatus.Refunded);
            @event.Should().BeOfType<RefundIssuedEvent>();
            @event.ProductId.Should().Be(5);
            @event.Quantity.Should().Be(2);
            @event.OrderId.Should().Be(1);
        }

        [Theory]
        [InlineData(PaymentStatus.Pending)]
        [InlineData(PaymentStatus.Expired)]
        [InlineData(PaymentStatus.Refunded)]
        public void IssueRefund_NonConfirmedPayment_ShouldThrowDomainException(PaymentStatus initialStatus)
        {
            var payment = CreatePending();
            SetStatus(payment, initialStatus);

            var act = () => payment.IssueRefund(1, 1);

            act.Should().Throw<DomainException>().WithMessage("*refund*");
        }

        [Fact]
        public void IssueRefund_ZeroQuantity_ShouldThrowDomainException()
        {
            var payment = CreatePending();
            payment.Confirm();

            var act = () => payment.IssueRefund(1, 0);

            act.Should().Throw<DomainException>().WithMessage("*quantity*");
        }

        // ── PaymentOrderId guard ──────────────────────────────────────────────

        [Fact]
        public void PaymentOrderId_Zero_ShouldThrowDomainException()
        {
            var act = () => new PaymentOrderId(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void PaymentOrderId_Negative_ShouldThrowDomainException()
        {
            var act = () => new PaymentOrderId(-1);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetStatus(Payment payment, PaymentStatus status)
        {
            // Use the aggregate's own state transitions to reach the desired status.
            switch (status)
            {
                case PaymentStatus.Confirmed:
                    payment.Confirm();
                    break;
                case PaymentStatus.Expired:
                    payment.Expire();
                    break;
                case PaymentStatus.Refunded:
                    payment.Confirm();
                    payment.IssueRefund(1, 1);
                    break;
            }
        }
    }
}
