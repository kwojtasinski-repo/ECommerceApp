using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Sales.Orders.Messages;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class OrderPlacementFailedHandlerTests
    {
        private readonly Mock<ILogger<OrderPlacementFailedHandler>> _logger;
        private readonly OrderPlacementFailedHandler _handler;

        public OrderPlacementFailedHandlerTests()
        {
            _logger = new Mock<ILogger<OrderPlacementFailedHandler>>();
            _handler = new OrderPlacementFailedHandler(_logger.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldCompleteWithoutThrowing()
        {
            var message = new OrderPlacementFailed(
                OrderId: 3,
                Reason: "inventory handler threw",
                Items: new List<OrderPlacedItem> { new OrderPlacedItem(ProductId: 10, Quantity: 1) },
                UserId: "user-1");

            var act = async () => await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnCompletedTask()
        {
            var message = new OrderPlacementFailed(
                OrderId: 3,
                Reason: "inventory handler threw",
                Items: new List<OrderPlacedItem>(),
                UserId: "user-1");

            var result = _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            result.Should().BeSameAs(Task.CompletedTask);
            await result;
        }
    }
}
