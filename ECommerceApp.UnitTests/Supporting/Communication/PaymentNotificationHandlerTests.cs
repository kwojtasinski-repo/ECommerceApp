using ECommerceApp.Application.Sales.Payments.Messages;
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
    public class PaymentConfirmedNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private PaymentConfirmedNotificationHandler CreateHandler()
        {
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            return new(_notifications.Object, _resolver.Object, _processedMessageGuard.Object);
        }

        private static PaymentConfirmed Message(int paymentId = 1, int orderId = 10)
            => new(paymentId, orderId, new List<PaymentConfirmedItem>(), DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");

            await CreateHandler().HandleAsync(Message(orderId: 10), 1, TestContext.Current.CancellationToken);

            _notifications.Verify(n => n.NotifyAsync(
                "user-10",
                "PaymentConfirmed",
                It.Is<string>(s => s.Contains("10")),
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

    public class PaymentExpiredNotificationHandlerTests
    {
        private readonly Mock<INotificationService> _notifications = new();
        private readonly Mock<IOrderUserResolver> _resolver = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        private PaymentExpiredNotificationHandler CreateHandler()
        {
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            return new(_notifications.Object, _resolver.Object, NullLogger<PaymentExpiredNotificationHandler>.Instance, _processedMessageGuard.Object);
        }

        private static PaymentExpired Message(int paymentId = 1, int orderId = 10)
            => new(paymentId, orderId, DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_WhenUserResolved_PushesNotification()
        {
            _resolver.Setup(r => r.GetUserIdForOrderAsync(10, It.IsAny<CancellationToken>()))
                     .ReturnsAsync("user-10");

            await CreateHandler().HandleAsync(Message(orderId: 10), 1, TestContext.Current.CancellationToken);

            _notifications.Verify(n => n.NotifyAsync(
                "user-10",
                "PaymentExpired",
                It.Is<string>(s => s.Contains("10")),
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
}

