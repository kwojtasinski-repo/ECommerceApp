using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Sales.Payments;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Payments
{
    /// <summary>
    /// Verifies that <see cref="PaymentWindowExpiredJob"/> stamps every published
    /// <see cref="PaymentExpired"/> with a non-empty <see cref="Guid"/> CorrelationId
    /// so all 5 downstream handlers can be correlated in logs.
    /// </summary>
    public class PaymentExpiredCorrelationTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepo = new();
        private readonly Mock<IMessageBroker> _broker = new();

        private PaymentWindowExpiredJob CreateJob()
            => new(_paymentRepo.Object, _broker.Object, NullLogger<PaymentWindowExpiredJob>.Instance);

        private static JobExecutionContext Context(string entityId = "1")
            => new(entityId, Guid.NewGuid().ToString());

        private static Payment CreatePendingPayment(int paymentId = 1, int orderId = 10)
        {
            var payment = Payment.Create(new PaymentOrderId(orderId), 99m, 1, DateTime.UtcNow.AddDays(3), "user-1");
            typeof(Payment).GetProperty(nameof(Payment.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(payment, new object[] { new PaymentId(paymentId) });
            return payment;
        }

        // ── CorrelationId stamped on publish ──────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_PendingPayment_ShouldPublishWithNonEmptyCorrelationId()
        {
            PaymentExpired captured = null;
            _paymentRepo
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePendingPayment(paymentId: 1, orderId: 10));
            _broker
                .Setup(b => b.PublishAsync(It.IsAny<IMessage[]>()))
                .Callback<IMessage[]>(msgs => captured = msgs[0] as PaymentExpired)
                .Returns(Task.CompletedTask);

            await CreateJob().ExecuteAsync(Context("1"), CancellationToken.None);

            captured.Should().NotBeNull();
            captured!.CorrelationId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task ExecuteAsync_TwoSeparateRuns_ShouldPublishDifferentCorrelationIds()
        {
            var payment1 = CreatePendingPayment(paymentId: 1, orderId: 10);
            var payment2 = CreatePendingPayment(paymentId: 2, orderId: 20);

            _paymentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(payment1);
            _paymentRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(payment2);

            var correlationIds = new System.Collections.Generic.List<Guid>();
            _broker
                .Setup(b => b.PublishAsync(It.IsAny<IMessage[]>()))
                .Callback<IMessage[]>(msgs =>
                {
                    if (msgs[0] is PaymentExpired pe)
                        correlationIds.Add(pe.CorrelationId);
                })
                .Returns(Task.CompletedTask);

            await CreateJob().ExecuteAsync(Context("1"), CancellationToken.None);
            await CreateJob().ExecuteAsync(Context("2"), CancellationToken.None);

            correlationIds.Count.Should().Be(2);
            correlationIds[0].Should().NotBe(Guid.Empty);
            correlationIds[1].Should().NotBe(Guid.Empty);
            correlationIds[0].Should().NotBe(correlationIds[1]);
        }

        [Fact]
        public async Task ExecuteAsync_PendingPayment_PublishedMessageShouldCarryCorrectPaymentAndOrderIds()
        {
            PaymentExpired captured = null;
            _paymentRepo
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePendingPayment(paymentId: 5, orderId: 42));
            _broker
                .Setup(b => b.PublishAsync(It.IsAny<IMessage[]>()))
                .Callback<IMessage[]>(msgs => captured = msgs[0] as PaymentExpired)
                .Returns(Task.CompletedTask);

            await CreateJob().ExecuteAsync(Context("5"), CancellationToken.None);

            captured.Should().NotBeNull();
            captured!.PaymentId.Should().Be(5);
            captured.OrderId.Should().Be(42);
        }

        // ── No publish when guards fire ────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_WhenPaymentNotPending_ShouldNotStampACorrelationId()
        {
            var payment = CreatePendingPayment(paymentId: 1, orderId: 10);
            payment.Confirm();
            _paymentRepo
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);

            await CreateJob().ExecuteAsync(Context("1"), CancellationToken.None);

            _broker.Verify(b => b.PublishAsync(It.IsAny<PaymentExpired>()), Times.Never);
        }
    }
}
