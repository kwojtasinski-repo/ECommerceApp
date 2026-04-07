using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Supporting.Communication.Contracts;
using ECommerceApp.Application.Supporting.Communication.Handlers;
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

        private RefundApprovedNotificationHandler CreateHandler()
            => new(_notifications.Object, _resolver.Object);

        private static RefundApproved Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, new List<RefundApprovedItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");

            await CreateHandler().HandleAsync(Message(refundId: 3, orderId: 10));

            _notifications.Verify(n => n.NotifyAsync(
                "user-10",
                "RefundApproved",
                It.Is<string>(s => s.Contains("3") && s.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string?)null);

            await CreateHandler().HandleAsync(Message());

            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class RefundRejectedNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();

        private RefundRejectedNotificationHandler CreateHandler()
            => new(_notifications.Object, _resolver.Object);

        private static RefundRejected Message(int refundId = 1, int orderId = 10)
            => new(refundId, orderId, DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");

            await CreateHandler().HandleAsync(Message(refundId: 5, orderId: 10));

            _notifications.Verify(n => n.NotifyAsync(
                "user-10",
                "RefundRejected",
                It.Is<string>(s => s.Contains("5") && s.Contains("10")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenUserNotResolved_SkipsNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((string?)null);

            await CreateHandler().HandleAsync(Message());

            _notifications.Verify(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
