using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Payments.Messages;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class PaymentConfirmedHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly PaymentConfirmedHandler _handler;

        public PaymentConfirmedHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _handler = new PaymentConfirmedHandler(_stockService.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldConfirmReservationsByOrder()
        {
            var message = new PaymentConfirmed(
                PaymentId: 1,
                OrderId: 42,
                Items: new[] { new PaymentConfirmedItem(10, 2), new PaymentConfirmedItem(20, 1) },
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _stockService.Verify(s => s.ConfirmReservationsByOrderAsync(
                42, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ShouldNotCallPerItemConfirm()
        {
            var message = new PaymentConfirmed(
                PaymentId: 1,
                OrderId: 42,
                Items: new[] { new PaymentConfirmedItem(10, 2) },
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _stockService.Verify(s => s.ConfirmAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_EmptyItems_ShouldStillCallConfirmByOrder()
        {
            var message = new PaymentConfirmed(
                PaymentId: 1,
                OrderId: 99,
                Items: Array.Empty<PaymentConfirmedItem>(),
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _stockService.Verify(s => s.ConfirmReservationsByOrderAsync(
                99, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
