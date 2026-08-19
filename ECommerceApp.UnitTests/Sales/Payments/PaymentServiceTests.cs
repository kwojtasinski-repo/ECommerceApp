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
            EntityIdSetter.Set(payment, new PaymentId(id));
            return payment;
        }

        private void SetupPendingPaymentConfirmation(Payment payment, Mock<IOutboxTransaction> transaction)
        {
            _paymentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
            _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
            transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _outboxWriter.Setup(w => w.EnqueueAsync(It.IsAny<IMessage>(), It.IsAny<IOutboxTransaction>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _paymentRepo.Setup(r => r.UpdateAsync(payment, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        private void SetupPaymentNotFound(int paymentId)
        {
            _paymentRepo.Setup(r => r.GetByIdAsync(paymentId, It.IsAny<CancellationToken>())).ReturnsAsync((Payment)null);
        }

        [Fact]
        public async Task ConfirmAsync_PendingPayment_EnqueuesAndCommits()
        {
            // Arrange
            var payment = CreatePendingPayment(id: 1, orderId: 42);
            var txMock = new Mock<IOutboxTransaction>();
            SetupPendingPaymentConfirmation(payment, txMock);
            var svc = CreateService();

            // Act
            var result = await svc.ConfirmAsync(new ConfirmPaymentDto(1, "TX-1"));

            // Assert
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
            // Arrange
            SetupPaymentNotFound(999);
            var svc = CreateService();

            // Act
            var result = await svc.ConfirmAsync(new ConfirmPaymentDto(999, "TX-1"));

            // Assert
            result.Should().Be(PaymentOperationResult.PaymentNotFound);
            _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
