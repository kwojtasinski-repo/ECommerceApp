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
    public class OrderShippedHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly OrderShippedHandler _handler;

        public OrderShippedHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _handler = new OrderShippedHandler(_stockService.Object);
        }

        [Fact]
        public async Task HandleAsync_MultipleItems_ShouldFulfillEachItem()
        {
            var items = new[]
            {
                new OrderShippedItem(10, 2),
                new OrderShippedItem(20, 3)
            };
            var message = new OrderShipped(OrderId: 5, Items: items, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.FulfillAsync(5, 10, 2, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.Verify(s => s.FulfillAsync(5, 20, 3, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SingleItem_ShouldFulfillOnce()
        {
            var items = new[] { new OrderShippedItem(30, 1) };
            var message = new OrderShipped(OrderId: 8, Items: items, OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.FulfillAsync(8, 30, 1, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_EmptyItems_ShouldNotCallFulfill()
        {
            var message = new OrderShipped(OrderId: 1, Items: Array.Empty<OrderShippedItem>(), OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.FulfillAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
