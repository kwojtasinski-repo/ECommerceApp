using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Sales.Payments;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Supporting.TimeManagement.Models;
using ECommerceApp.Domain.Sales.Payments;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Payments
{
    public class PaymentWindowExpiredJobTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepo;
        private readonly Mock<IPaymentsUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxWriter> _outboxWriter;

        public PaymentWindowExpiredJobTests()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _unitOfWork = new Mock<IPaymentsUnitOfWork>();
            _outboxWriter = new Mock<IOutboxWriter>();
        }

        private PaymentWindowExpiredJob CreateJob()
            => new(_paymentRepo.Object, _unitOfWork.Object, _outboxWriter.Object, NullLogger<PaymentWindowExpiredJob>.Instance);

        private static JobExecutionContext Context(string entityId)
            => new(entityId, Guid.NewGuid().ToString());

        private static Payment CreatePendingPayment(int paymentId = 1, int orderId = 10)
        {
            var payment = Payment.Create(new PaymentOrderId(orderId), 99m, 1, DateTime.UtcNow.AddDays(3), "user-1");
            typeof(Payment).GetProperty(nameof(Payment.Id))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(payment, new object[] { new PaymentId(paymentId) });
            return payment;
        }

        // ── EntityId guards ───────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_NullEntityId_ShouldReportFailure()
        {
            var ctx = Context(null);

            await CreateJob().ExecuteAsync(ctx, CancellationToken.None);

            ctx.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _paymentRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NonIntegerEntityId_ShouldReportFailure()
        {
            var ctx = Context("not-a-number");

            await CreateJob().ExecuteAsync(ctx, CancellationToken.None);

            ctx.Outcome.Should().BeOfType<JobOutcome.Failure>();
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── No-op guards ──────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_PaymentNotFound_ShouldReportSuccessAndSkip()
        {
            _paymentRepo
                .Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Payment)null);
            var ctx = Context("42");

            await CreateJob().ExecuteAsync(ctx, CancellationToken.None);

            ctx.Outcome.Should().BeOfType<JobOutcome.Success>();
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(PaymentStatus.Confirmed)]
        [InlineData(PaymentStatus.Expired)]
        [InlineData(PaymentStatus.Refunded)]
        public async Task ExecuteAsync_NonPendingPayment_ShouldReportSuccessAndSkip(PaymentStatus status)
        {
            var payment = CreatePendingPayment();
            AdvanceToStatus(payment, status);
            _paymentRepo
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);
            var ctx = Context("1");

            await CreateJob().ExecuteAsync(ctx, CancellationToken.None);

            ctx.Outcome.Should().BeOfType<JobOutcome.Success>();
            _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── Happy path ────────────────────────────────────────────────────────

        [Fact]
        public async Task ExecuteAsync_PendingPayment_ShouldExpireAndPublishPaymentExpired()
        {
            var payment = CreatePendingPayment(paymentId: 5, orderId: 10);
            _paymentRepo
                .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(payment);
            var ctx = Context("5");

            // Arrange transaction + outbox expectations
            var txMock = new Mock<IOutboxTransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(txMock.Object);
            _outboxWriter
                .Setup(w => w.EnqueueAsync(It.IsAny<PaymentExpired>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            await CreateJob().ExecuteAsync(ctx, CancellationToken.None);

            payment.Status.Should().Be(PaymentStatus.Expired);
            _paymentRepo.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(It.Is<PaymentExpired>(msg => msg.OrderId == 10), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            ctx.Outcome.Should().BeOfType<JobOutcome.Success>();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AdvanceToStatus(Payment payment, PaymentStatus target)
        {
            switch (target)
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
