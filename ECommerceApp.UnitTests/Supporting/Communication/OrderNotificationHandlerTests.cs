using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Handlers;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.Communication.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Communication
{
    public class OrderPlacedNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private OrderPlacedNotificationHandler CreateHandler()
        {
            SetupMessageAsUnprocessed();
            return new(_notifications.Object, _processedMessageGuard.Object);
        }

        private void SetupMessageAsUnprocessed()
        {
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        private static OrderPlaced Message(int orderId = 1, string userId = "user-1")
            => new(orderId, new List<OrderPlacedItem>(), userId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow, 99.99m, 1);

        [Fact]
        public async Task HandleAsync_PushesNotificationToOrderOwner()
        {
            // Arrange
            var handler = CreateHandler();
            var message = Message(orderId: 42, userId: "user-42");

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                "user-42",
                "OrderPlaced",
                It.Is<string>(s => s.Contains("42")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_MessageContainsOrderId()
        {
            // Arrange
            var handler = CreateHandler();
            var message = Message(orderId: 7);

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(),
                "OrderPlaced",
                It.Is<string>(s => s.Contains("7")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class OrderCancelledNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private OrderCancelledNotificationHandler CreateHandler()
        {
            SetupMessageAsUnprocessed();
            return new(_notifications.Object, _resolver.Object, _processedMessageGuard.Object);
        }

        private void SetupMessageAsUnprocessed()
        {
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        private static OrderCancelled Message(int orderId = 1)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            // Arrange
            SetupUserResolution("user-5");
            var handler = CreateHandler();
            var message = Message(orderId: 5);

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                "user-5",
                "OrderCancelled",
                It.Is<string>(s => s.Contains("5")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsNotification()
        {
            // Arrange
            SetupUserResolution(null);
            var handler = CreateHandler();
            var message = Message();

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

            private void SetupUserResolution(string userId)
            {
                _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(userId);
            }
    }

    public class OrderRequiresAttentionNotificationHandlerTests
    {
        [Fact]
        public async Task HandleAsync_CompletesWithoutException()
        {
            // Arrange
            var handler = new OrderRequiresAttentionNotificationHandler(NullLogger<OrderRequiresAttentionNotificationHandler>.Instance);
            var message = new OrderRequiresAttention(99, "Shipment failed", DateTime.UtcNow);

            // Act Assert
            await handler.HandleAsync(message, TestContext.Current.CancellationToken);
        }
    }
}
