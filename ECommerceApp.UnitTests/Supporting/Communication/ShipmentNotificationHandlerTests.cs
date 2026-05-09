using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Supporting.Communication.Handlers;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Supporting.Communication
{
    public class ShipmentNotificationHandlerTests
    {
        // ── ShipmentFailedNotificationHandler ─────────────────────────────────

        [Fact]
        public async Task ShipmentFailed_ShouldLogWarning()
        {
            var logger = new Mock<ILogger<ShipmentFailedNotificationHandler>>();
            var handler = new ShipmentFailedNotificationHandler(logger.Object);
            var message = new ShipmentFailed(
                ShipmentId: 7,
                OrderId: 42,
                Items: new List<ShipmentLineItem> { new(ProductId: 1, Quantity: 2) },
                OccurredAt: DateTime.UtcNow);

            await handler.HandleAsync(message, TestContext.Current.CancellationToken);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("42") && v.ToString()!.Contains("7")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ShipmentFailed_ShouldCompleteWithoutThrowing()
        {
            var handler = new ShipmentFailedNotificationHandler(
                new Mock<ILogger<ShipmentFailedNotificationHandler>>().Object);
            var message = new ShipmentFailed(1, 1, new List<ShipmentLineItem>(), DateTime.UtcNow);

            var act = async () => await handler.HandleAsync(message, TestContext.Current.CancellationToken);

            await act.Invoke();
        }

        // ── ShipmentPartiallyDeliveredNotificationHandler ─────────────────────

        [Fact]
        public async Task ShipmentPartiallyDelivered_ShouldLogWarning()
        {
            var logger = new Mock<ILogger<ShipmentPartiallyDeliveredNotificationHandler>>();
            var handler = new ShipmentPartiallyDeliveredNotificationHandler(logger.Object);
            var message = new ShipmentPartiallyDelivered(
                ShipmentId: 8,
                OrderId: 55,
                DeliveredItems: new List<ShipmentLineItem> { new(ProductId: 1, Quantity: 1) },
                FailedItems: new List<ShipmentLineItem> { new(ProductId: 2, Quantity: 3) },
                OccurredAt: DateTime.UtcNow);

            await handler.HandleAsync(message, TestContext.Current.CancellationToken);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("55") && v.ToString()!.Contains("8")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ShipmentPartiallyDelivered_ShouldCompleteWithoutThrowing()
        {
            var handler = new ShipmentPartiallyDeliveredNotificationHandler(
                new Mock<ILogger<ShipmentPartiallyDeliveredNotificationHandler>>().Object);
            var message = new ShipmentPartiallyDelivered(
                1, 1,
                new List<ShipmentLineItem>(),
                new List<ShipmentLineItem>(),
                DateTime.UtcNow);

            var act = async () => await handler.HandleAsync(message, TestContext.Current.CancellationToken);

            await act.Invoke();
        }
    }
}
