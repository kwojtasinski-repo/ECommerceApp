using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Handlers;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Presale.Checkout;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Presale.Checkout
{
    public class OrderPlacementFailedHandlerTests
    {
        private readonly Mock<ICartService> _cartService;
        private readonly Mock<ILogger<OrderPlacementFailedHandler>> _logger;
        private readonly OrderPlacementFailedHandler _handler;

        public OrderPlacementFailedHandlerTests()
        {
            _cartService = new Mock<ICartService>();
            _logger = new Mock<ILogger<OrderPlacementFailedHandler>>();
            _handler = new OrderPlacementFailedHandler(_cartService.Object, _logger.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldCallRestoreAsyncWithMessageItems()
        {
            var message = new OrderPlacementFailed(
                OrderId: 3,
                Reason: "inventory handler threw",
                Items: new List<OrderPlacedItem> { new OrderPlacedItem(ProductId: 10, Quantity: 1) },
                UserId: "user-1");

            _cartService
                .Setup(s => s.RestoreAsync(
                    It.IsAny<PresaleUserId>(),
                    It.IsAny<IReadOnlyList<CartRestoreItem>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _cartService.Verify(s => s.RestoreAsync(
                It.Is<PresaleUserId>(id => id.Value == "user-1"),
                It.Is<IReadOnlyList<CartRestoreItem>>(items =>
                    items.SequenceEqual(new[] { new CartRestoreItem(10, 1) })),
                It.IsAny<CancellationToken>()), Times.Once);

            VerifyLog(LogLevel.Information, Times.Once(), "Cart for user user-1 restored");
        }

        [Fact]
        public async Task HandleAsync_WhenRestoreAsyncThrows_ShouldLogAndNotThrow()
        {
            var message = new OrderPlacementFailed(
                OrderId: 3,
                Reason: "inventory handler threw",
                Items: new List<OrderPlacedItem> { new OrderPlacedItem(ProductId: 10, Quantity: 1) },
                UserId: "user-1");

            _cartService
                .Setup(s => s.RestoreAsync(
                    It.IsAny<PresaleUserId>(),
                    It.IsAny<IReadOnlyList<CartRestoreItem>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var act = async () => await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            await act.Should().NotThrowAsync();
            VerifyLog(LogLevel.Error, Times.Once(), "Failed to restore cart for user user-1");
        }

        private void VerifyLog(LogLevel logLevel, Times times, string expectedMessage)
        {
            _logger.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                times);
        }
    }
}
