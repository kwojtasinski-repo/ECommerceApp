using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Application.Sales.Payments;
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
        private readonly Mock<IPaymentsUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();

        private PaymentWindowExpiredJob CreateJob()
            => new(_paymentRepo.Object, _unitOfWork.Object, _outboxWriter.Object, NullLogger<PaymentWindowExpiredJob>.Instance);

        private static JobExecutionContext Context(string entityId = "1")
            => new(entityId, Guid.NewGuid().ToString());

        private static Payment CreatePendingPayment(int paymentId = 1, int orderId = 10)
        {
            var payment = Payment.Create(new PaymentOrderId(orderId), 99m, 1, DateTime.UtcNow.AddDays(3), "user-1");
            EntityIdSetter.Set(payment, new PaymentId(paymentId));
            return payment;
        }

        private void SetupPaymentLookup(int paymentId, Payment payment)
        {
            _paymentRepo.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);
        }

        private void SetupTransactionAndPaymentExpiredOutbox(Action<PaymentExpired> observe = null)
        {
            var txMock = new Mock<IOutboxTransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            _outboxWriter
                .Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<IMessage, IOutboxTransaction, CancellationToken>((msg, _, _) =>
                {
                    if (msg is PaymentExpired paymentExpired)
                    {
                        observe?.Invoke(paymentExpired);
                    }
                })
                .Returns(Task.CompletedTask);
        }

        // ── CorrelationId stamped on publish ──────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_PendingPayment_ShouldPublishWithNonEmptyCorrelationId()
        {
            // Arrange
            PaymentExpired captured = null;
            SetupPaymentLookup(1, CreatePendingPayment(paymentId: 1, orderId: 10));
            SetupTransactionAndPaymentExpiredOutbox(paymentExpired => captured = paymentExpired);

            // Act
            await CreateJob().ExecuteAsync(Context("1"), CancellationToken.None);

            // Assert
            captured.Should().NotBeNull();
            captured!.CorrelationId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task ExecuteAsync_TwoSeparateRuns_ShouldPublishDifferentCorrelationIds()
        {
            // Arrange
            var payment1 = CreatePendingPayment(paymentId: 1, orderId: 10);
            var payment2 = CreatePendingPayment(paymentId: 2, orderId: 20);

            SetupPaymentLookup(1, payment1);
            SetupPaymentLookup(2, payment2);

            var correlationIds = new System.Collections.Generic.List<Guid>();
            SetupTransactionAndPaymentExpiredOutbox(paymentExpired => correlationIds.Add(paymentExpired.CorrelationId));

            // Act
            await CreateJob().ExecuteAsync(Context("1"), CancellationToken.None);
            await CreateJob().ExecuteAsync(Context("2"), CancellationToken.None);

            // Assert
            correlationIds.Count.Should().Be(2);
            correlationIds[0].Should().NotBe(Guid.Empty);
            correlationIds[1].Should().NotBe(Guid.Empty);
            correlationIds[0].Should().NotBe(correlationIds[1]);
        }

        [Fact]
        public async Task ExecuteAsync_PendingPayment_PublishedMessageShouldCarryCorrectPaymentAndOrderIds()
        {
            // Arrange
            PaymentExpired captured = null;
            SetupPaymentLookup(5, CreatePendingPayment(paymentId: 5, orderId: 42));
            SetupTransactionAndPaymentExpiredOutbox(paymentExpired => captured = paymentExpired);

            // Act
            await CreateJob().ExecuteAsync(Context("5"), CancellationToken.None);

            // Assert
            captured.Should().NotBeNull();
            captured!.PaymentId.Should().Be(5);
            captured.OrderId.Should().Be(42);
        }

        // ── No publish when guards fire ────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_WhenPaymentNotPending_ShouldNotStampACorrelationId()
        {
            // Arrange
            var payment = CreatePendingPayment(paymentId: 1, orderId: 10);
            payment.Confirm();
            SetupPaymentLookup(1, payment);

            // Act
            await CreateJob().ExecuteAsync(Context("1"), CancellationToken.None);

            // Assert
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
