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
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            return new(_notifications.Object, _processedMessageGuard.Object);
        }

        private static OrderPlaced Message(int orderId = 1, string userId = "user-1")
            => new(orderId, new List<OrderPlacedItem>(), userId, DateTime.UtcNow.AddDays(3), DateTime.UtcNow, 99.99m, 1);

        [Fact]
        public async Task HandleAsync_PushesNotificationToOrderOwner()
        {
            await CreateHandler().HandleAsync(Message(orderId: 42, userId: "user-42"), 1, TestContext.Current.CancellationToken);

            _notifications.Verify(n => n.NotifyAsync(
                "user-42",
                "OrderPlaced",
                It.Is<string>(s => s.Contains("42")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_MessageContainsOrderId()
        {
            await CreateHandler().HandleAsync(Message(orderId: 7), 1, TestContext.Current.CancellationToken);

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
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            return new(_notifications.Object, _resolver.Object, _processedMessageGuard.Object);
        }

        private static OrderCancelled Message(int orderId = 1)
            => new(orderId, new List<OrderCancelledItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(5, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-5");

            await CreateHandler().HandleAsync(Message(orderId: 5), 1, TestContext.Current.CancellationToken);

            _notifications.Verify(n => n.NotifyAsync(
                "user-5",
                "OrderCancelled",
                It.Is<string>(s => s.Contains("5")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string)null);

            await CreateHandler().HandleAsync(Message(), 1, TestContext.Current.CancellationToken);

            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class OrderRequiresAttentionNotificationHandlerTests
    {
        [Fact]
        public async Task HandleAsync_CompletesWithoutException()
        {
            var handler = new OrderRequiresAttentionNotificationHandler(NullLogger<OrderRequiresAttentionNotificationHandler>.Instance);
            var message = new OrderRequiresAttention(99, "Shipment failed", DateTime.UtcNow);

            await handler.HandleAsync(message, TestContext.Current.CancellationToken);
        }
    }
}
