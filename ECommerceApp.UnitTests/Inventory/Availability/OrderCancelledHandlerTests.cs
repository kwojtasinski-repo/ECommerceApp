using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class OrderCancelledHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly OrderCancelledHandler _handler;

        public OrderCancelledHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _handler = new OrderCancelledHandler(_stockService.Object);
        }

        [Fact]
        public async Task HandleAsync_MultipleItems_ShouldReleaseEachItem()
        {
            var items = new[]
            {
                new OrderCancelledItem(10, 2),
                new OrderCancelledItem(20, 5)
            };
            var message = new OrderCancelled(OrderId: 1, Items: items, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _stockService.Verify(s => s.ReleaseAsync(1, 10, 2, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.Verify(s => s.ReleaseAsync(1, 20, 5, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SingleItem_ShouldReleaseOnce()
        {
            var items = new[] { new OrderCancelledItem(30, 1) };
            var message = new OrderCancelled(OrderId: 7, Items: items, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _stockService.Verify(s => s.ReleaseAsync(7, 30, 1, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_EmptyItems_ShouldNotCallRelease()
        {
            var message = new OrderCancelled(OrderId: 1, Items: Array.Empty<OrderCancelledItem>(), OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message);

            _stockService.Verify(s => s.ReleaseAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
