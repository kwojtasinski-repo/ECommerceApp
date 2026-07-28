using AwesomeAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Sales.Payments;
using ECommerceApp.Application.Sales.Payments.DTOs;
using ECommerceApp.Application.Sales.Payments.Messages;
using ECommerceApp.Application.Sales.Payments.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Sales.Payments;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Payments
{
    public class PaymentServiceTests
    {
        private readonly Mock<IPaymentRepository> _paymentRepo = new();
        private readonly Mock<IPaymentsUnitOfWork> _uow = new();
        private readonly Mock<IOutboxWriter> _outboxWriter = new();

        private PaymentService CreateService()
        {
            return new PaymentService(_paymentRepo.Object, _uow.Object, _outboxWriter.Object);
        }

        private static Payment CreatePendingPayment(int id, int orderId)
        {
            var payment = Payment.Create(new PaymentOrderId(orderId), 100m, 1, DateTime.UtcNow.AddHours(24), "user-1");
            typeof(Payment).GetProperty("Id")!.SetValue(payment, new PaymentId(id));
            return payment;
        }

        [Fact]
        public async Task ConfirmAsync_PendingPayment_EnqueuesAndCommits()
        {
            var payment = CreatePendingPayment(id: 1, orderId: 42);
            _paymentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

            var txMock = new Mock<IOutboxTransaction>();
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(txMock.Object);
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _paymentRepo.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var svc = CreateService();
            var result = await svc.ConfirmAsync(new ConfirmPaymentDto(1, "TX-1"));

            result.Should().Be(PaymentOperationResult.Success);
            _paymentRepo.Verify(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
            _outboxWriter.Verify(w => w.EnqueueAsync(
                It.Is<PaymentConfirmed>(m => m.PaymentId == 1 && m.OrderId == 42),
                txMock.Object,
                It.IsAny<CancellationToken>()), Times.Once);
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ConfirmAsync_NonExistentPayment_ReturnsPaymentNotFound_AndDoesNotOpenTransaction()
        {
            _paymentRepo.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Payment)null);

            var svc = CreateService();
            var result = await svc.ConfirmAsync(new ConfirmPaymentDto(999, "TX-1"));

            result.Should().Be(PaymentOperationResult.PaymentNotFound);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
