using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Sales.Payments;
using AwesomeAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Payments
{
    public class OrderPlacedHandlerTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepo;
        private readonly Mock<IDeferredJobScheduler> _scheduler;
        private readonly Mock<IPaymentsUnitOfWork> _unitOfWork;
        private readonly Mock<IOutboxTransaction> _transaction;
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard;

        public OrderPlacedHandlerTests()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _scheduler = new Mock<IDeferredJobScheduler>();
            _unitOfWork = new Mock<IPaymentsUnitOfWork>();
            _transaction = new Mock<IOutboxTransaction>();
            _processedMessageGuard = new Mock<IProcessedMessageGuard>();
            SetupTransactionWorkflow();
        }

        private void SetupTransactionWorkflow()
        {
            _unitOfWork
                .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_transaction.Object);
            _processedMessageGuard
                .Setup(g => g.TryMarkProcessedAsync(
                    It.IsAny<long>(),
                    It.IsAny<string>(),
                    _transaction.Object,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

            private void SetupPaymentCapture(Action<Payment> capturePayment)
            {
                _paymentRepo
                .Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
                .Callback<Payment, CancellationToken>((payment, _) => capturePayment(payment))
                .Returns(Task.CompletedTask);
            }

            private void SetupJobScheduling(Action<string, DateTime> captureSchedule)
            {
                _scheduler
                .Setup(s => s.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, DateTime, CancellationToken>((name, _, scheduledAt, _) => captureSchedule(name, scheduledAt))
                .Returns(Task.CompletedTask);
            }

        private OrderPlacedHandler CreateHandler()
            => new(
                _paymentRepo.Object,
                _scheduler.Object,
                _unitOfWork.Object,
                _processedMessageGuard.Object);

        private static OrderPlaced CreateMessage(int orderId = 1, decimal total = 99.99m, int currencyId = 1)
        {
            var expiresAt = DateTime.UtcNow.AddDays(3);
            return new OrderPlaced(orderId, new List<OrderPlacedItem>(), "user-1", expiresAt, DateTime.UtcNow, total, currencyId);
        }

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCreatePaymentAndPersist()
        {
            // Arrange
            Payment savedPayment = null;
            SetupPaymentCapture(payment => savedPayment = payment);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(orderId: 7, total: 49.99m, currencyId: 2), 1, TestContext.Current.CancellationToken);

            // Assert
            savedPayment.Should().NotBeNull();
            savedPayment!.OrderId.Value.Should().Be(7);
            savedPayment.TotalAmount.Should().Be(49.99m);
            savedPayment.CurrencyId.Should().Be(2);
            savedPayment.Status.Should().Be(PaymentStatus.Pending);
            savedPayment.PaymentId.Should().NotBe(Guid.Empty);
            savedPayment.UserId.Should().Be("user-1");
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldScheduleJobWithPaymentWindowExpiredJobName()
        {
            // Arrange
            string scheduledJobName = null;
            SetupJobScheduling((name, _) => scheduledJobName = name);

            // Act
            await CreateHandler().HandleAsync(CreateMessage(), 1, TestContext.Current.CancellationToken);

            // Assert
            scheduledJobName.Should().Be(PaymentWindowExpiredJob.JobTaskName);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldScheduleJobAtExpiresAt()
        {
            // Arrange
            var expiresAt = DateTime.UtcNow.AddDays(5);
            var message = new OrderPlaced(1, new List<OrderPlacedItem>(), "user-1", expiresAt, DateTime.UtcNow, 99m, 1);
            DateTime? scheduledAt = null;
            SetupJobScheduling((_, at) => scheduledAt = at);

            // Act
            await CreateHandler().HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            scheduledAt.Should().Be(expiresAt);
        }
    }
}
