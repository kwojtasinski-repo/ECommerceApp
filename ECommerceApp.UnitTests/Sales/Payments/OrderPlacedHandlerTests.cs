using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
using ECommerceApp.Domain.Sales.Payments;
using FluentAssertions;
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

        public OrderPlacedHandlerTests()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _scheduler = new Mock<IDeferredJobScheduler>();
        }

        private OrderPlacedHandler CreateHandler()
            => new(_paymentRepo.Object, _scheduler.Object);

        private static OrderPlaced CreateMessage(int orderId = 1, decimal total = 99.99m, int currencyId = 1)
        {
            var expiresAt = DateTime.UtcNow.AddDays(3);
            return new OrderPlaced(orderId, new List<OrderPlacedItem>(), "user-1", expiresAt, DateTime.UtcNow, total, currencyId);
        }

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCreatePaymentAndPersist()
        {
            Payment? savedPayment = null;
            _paymentRepo
                .Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
                .Callback<Payment, CancellationToken>((p, _) => savedPayment = p)
                .Returns(Task.CompletedTask);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 7, total: 49.99m, currencyId: 2));

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
            string? scheduledJobName = null;
            _scheduler
                .Setup(s => s.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, DateTime, CancellationToken>((name, _, _, _) => scheduledJobName = name)
                .Returns(Task.CompletedTask);

            await CreateHandler().HandleAsync(CreateMessage());

            scheduledJobName.Should().Be(PaymentWindowExpiredJob.JobTaskName);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldScheduleJobAtExpiresAt()
        {
            var expiresAt = DateTime.UtcNow.AddDays(5);
            var message = new OrderPlaced(1, new List<OrderPlacedItem>(), "user-1", expiresAt, DateTime.UtcNow, 99m, 1);
            DateTime? scheduledAt = null;
            _scheduler
                .Setup(s => s.ScheduleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, DateTime, CancellationToken>((_, _, at, _) => scheduledAt = at)
                .Returns(Task.CompletedTask);

            await CreateHandler().HandleAsync(message);

            scheduledAt.Should().Be(expiresAt);
        }
    }
}
