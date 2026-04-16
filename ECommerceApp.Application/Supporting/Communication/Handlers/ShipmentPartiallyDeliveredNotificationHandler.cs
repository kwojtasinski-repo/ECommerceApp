using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    /// <summary>
    /// Routes ShipmentPartiallyDelivered operator alerts — logging only.
    /// Replaces the implicit chain: OrderShipmentPartiallyDeliveredHandler → OrderRequiresAttention → here.
    /// </summary>
    internal sealed class ShipmentPartiallyDeliveredNotificationHandler : IMessageHandler<ShipmentPartiallyDelivered>
    {
        private readonly ILogger<ShipmentPartiallyDeliveredNotificationHandler> _logger;

        public ShipmentPartiallyDeliveredNotificationHandler(ILogger<ShipmentPartiallyDeliveredNotificationHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "[Communication][OperatorAlert] OrderId={OrderId} ShipmentId={ShipmentId} partially delivered — {Count} item(s) failed.",
                message.OrderId, message.ShipmentId, message.FailedItems.Count);
            return Task.CompletedTask;
        }
    }
}
