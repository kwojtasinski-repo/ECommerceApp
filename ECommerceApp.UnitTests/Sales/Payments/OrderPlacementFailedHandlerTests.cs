using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using ECommerceApp.Application.Messaging;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Payments
{
    public class OrderPlacementFailedHandlerTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepo;
        private readonly Mock<IDeferredJobScheduler> _scheduler;
        private readonly Mock<IPaymentsUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOutboxTransaction> _transaction = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        public OrderPlacementFailedHandlerTests()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _scheduler = new Mock<IDeferredJobScheduler>();
            SetupProcessingDefaults();
        }

        private OrderPlacementFailedHandler CreateHandler()
            => new(_paymentRepo.Object, _scheduler.Object, _unitOfWork.Object, _processedMessageGuard.Object);

        private static OrderPlacementFailed CreateMessage(int orderId = 1)
            => new(orderId, "handler threw", new List<OrderPlacedItem>(), "user-1");

        private static Payment CreatePendingPayment(int paymentId = 42, int orderId = 1)
            => Payment.Create(new PaymentId(paymentId), new PaymentOrderId(orderId), 99.99m, 1, DateTime.UtcNow.AddDays(3), "user-1");

        private void SetupProcessingDefaults()
        {
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_transaction.Object);
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(
                It.IsAny<long>(), It.IsAny<string>(), _transaction.Object, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        private void SetupPayment(Payment payment)
        {
            _paymentRepo.Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        }

        private void SetupPaymentUpdate(Action<Payment> onUpdated = null)
        {
            _paymentRepo
                .Setup(r => r.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
                .Callback<Payment, CancellationToken>((payment, _) => onUpdated?.Invoke(payment))
                .Returns(Task.CompletedTask);
        }

            private void SetupCancellation(Action<string, string> onCancelled = null)
            {
                _scheduler
                .Setup(s => s.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((job, id, _) => onCancelled?.Invoke(job, id))
                .Returns(Task.CompletedTask);
            }

        // ── payment found ─────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentFound_ShouldCancelPayment()
        {
            // Arrange
            Payment updatedPayment = null;
            var payment = CreatePendingPayment();
            SetupPayment(payment);
            SetupPaymentUpdate(p => updatedPayment = p);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), 1, TestContext.Current.CancellationToken);

            // Assert
            updatedPayment.Should().NotBeNull();
            updatedPayment!.Status.Should().Be(PaymentStatus.Cancelled);
        }

        [Fact]
        public async Task HandleAsync_PaymentFound_ShouldCancelScheduledJob()
        {
            // Arrange
            string cancelledJobName = null;
            string cancelledEntityId = null;
            var payment = CreatePendingPayment(paymentId: 42);
            SetupPayment(payment);
            SetupPaymentUpdate();
            SetupCancellation((job, id) => { cancelledJobName = job; cancelledEntityId = id; });

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), 1, TestContext.Current.CancellationToken);

            // Assert
            cancelledJobName.Should().Be(PaymentWindowExpiredJob.JobTaskName);
            cancelledEntityId.Should().Be("42");
        }

        // ── payment not found ─────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentNotFound_ShouldNotCallUpdateOrCancel()
        {
            // Arrange
            SetupPayment(null);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), 1, TestContext.Current.CancellationToken);

            // Assert
            _paymentRepo.Verify(r => r.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
            _scheduler.Verify(s => s.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
