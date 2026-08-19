using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Handlers;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Supporting.Communication.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Communication
{
    public class RefundApprovedNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private RefundApprovedNotificationHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(_notifications.Object, _resolver.Object, _processedMessageGuard.Object);
        }

        private static RefundApproved Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, new List<RefundApprovedItem>(), DateTime.UtcNow);

        private void SetupUserResolution(int orderId, string userId)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(userId);
        }

        private void SetupUserNotResolved()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);
        }

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            // Arrange
            SetupUserResolution(10, "user-10");
            var handler = CreateHandler();
            var message = Message(refundId: 3, orderId: 10);

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                "user-10",
                "RefundApproved",
                It.Is<string>(s => s.Contains("3") && s.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsNotification()
        {
            // Arrange
            SetupUserNotResolved();
            var handler = CreateHandler();
            var message = Message();

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class RefundRejectedNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private void SetupMessageAccepted()
            => _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        private RefundRejectedNotificationHandler CreateHandler()
        {
            SetupMessageAccepted();
            return new(_notifications.Object, _resolver.Object, _processedMessageGuard.Object);
        }

        private static RefundRejected Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, DateTime.UtcNow);

        private void SetupUserResolution(int orderId, string userId)
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(orderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(userId);
        }

        private void SetupUserNotResolved()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);
        }

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            // Arrange
            SetupUserResolution(10, "user-10");
            var handler = CreateHandler();
            var message = Message(refundId: 5, orderId: 10);

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                "user-10",
                "RefundRejected",
                It.Is<string>(s => s.Contains("5") && s.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsNotification()
        {
            // Arrange
            SetupUserNotResolved();
            var handler = CreateHandler();
            var message = Message();

            // Act
            await handler.HandleAsync(message, 1, TestContext.Current.CancellationToken);

            // Assert
            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
