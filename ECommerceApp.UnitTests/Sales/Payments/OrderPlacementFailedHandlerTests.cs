using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Sales.Payments.Handlers;
using ECommerceApp.Application.Supporting.TimeManagement;
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
    public class OrderPlacementFailedHandlerTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepo;
        private readonly Mock<IDeferredJobScheduler> _scheduler;

        public OrderPlacementFailedHandlerTests()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _scheduler = new Mock<IDeferredJobScheduler>();
        }

        private OrderPlacementFailedHandler CreateHandler()
            => new(_paymentRepo.Object, _scheduler.Object);

        private static OrderPlacementFailed CreateMessage(int orderId = 1)
            => new(orderId, "handler threw", new List<OrderPlacedItem>(), "user-1");

        private static Payment CreatePendingPayment(int paymentId = 42, int orderId = 1)
            => Payment.Create(new PaymentId(paymentId), new PaymentOrderId(orderId), 99.99m, 1, DateTime.UtcNow.AddDays(3), "user-1");

        // ── payment found ─────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentFound_ShouldCancelPayment()
        {
            Payment updatedPayment = null;
            var payment = CreatePendingPayment();
            _paymentRepo.Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
            _paymentRepo
                .Setup(r => r.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
                .Callback<Payment, CancellationToken>((p, _) => updatedPayment = p)
                .Returns(Task.CompletedTask);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            updatedPayment.Should().NotBeNull();
            updatedPayment!.Status.Should().Be(PaymentStatus.Cancelled);
        }

        [Fact]
        public async Task HandleAsync_PaymentFound_ShouldCancelScheduledJob()
        {
            string cancelledJobName = null;
            string cancelledEntityId = null;
            var payment = CreatePendingPayment(paymentId: 42);
            _paymentRepo.Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
            _paymentRepo.Setup(r => r.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _scheduler
                .Setup(s => s.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, CancellationToken>((job, id, _) => { cancelledJobName = job; cancelledEntityId = id; })
                .Returns(Task.CompletedTask);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            cancelledJobName.Should().Be(PaymentWindowExpiredJob.JobTaskName);
            cancelledEntityId.Should().Be("42");
        }

        // ── payment not found ─────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_PaymentNotFound_ShouldNotCallUpdateOrCancel()
        {
            _paymentRepo.Setup(r => r.GetByOrderIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Payment)null);

            await CreateHandler().HandleAsync(CreateMessage(orderId: 1), TestContext.Current.CancellationToken);

            _paymentRepo.Verify(r => r.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
            _scheduler.Verify(s => s.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
