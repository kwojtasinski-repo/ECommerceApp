using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class OrderPlacementFailedHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly OrderPlacementFailedHandler _handler;

        public OrderPlacementFailedHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _handler = new OrderPlacementFailedHandler(_stockService.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldCallReleaseForEachItem()
        {
            var message = new OrderPlacementFailed(
                OrderId: 5,
                Reason: "handler threw",
                Items: new List<OrderPlacedItem>
                {
                    new OrderPlacedItem(ProductId: 10, Quantity: 2),
                    new OrderPlacedItem(ProductId: 20, Quantity: 1)
                },
                UserId: "user-1");

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAsync(5, 10, 2, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.Verify(s => s.ReleaseAsync(5, 20, 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_EmptyItems_ShouldNotCallRelease()
        {
            var message = new OrderPlacementFailed(
                OrderId: 5,
                Reason: "handler threw",
                Items: new List<OrderPlacedItem>(),
                UserId: "user-1");

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ShouldNotCallOtherStockMethods()
        {
            var message = new OrderPlacementFailed(
                OrderId: 5,
                Reason: "handler threw",
                Items: new List<OrderPlacedItem> { new OrderPlacedItem(ProductId: 10, Quantity: 2) },
                UserId: "user-1");

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReleaseAsync(5, 10, 2, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.VerifyNoOtherCalls();
        }
    }
}
