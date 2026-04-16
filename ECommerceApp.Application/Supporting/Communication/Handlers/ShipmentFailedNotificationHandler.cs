using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    /// <summary>
    /// Routes ShipmentFailed operator alerts — logging only.
    /// Replaces the implicit chain: OrderShipmentFailedHandler → OrderRequiresAttention → here.
    /// </summary>
    internal sealed class ShipmentFailedNotificationHandler : IMessageHandler<ShipmentFailed>
    {
        private readonly ILogger<ShipmentFailedNotificationHandler> _logger;

        public ShipmentFailedNotificationHandler(ILogger<ShipmentFailedNotificationHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(ShipmentFailed message, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "[Communication][OperatorAlert] OrderId={OrderId} ShipmentId={ShipmentId} failed — {Count} item(s) affected.",
                message.OrderId, message.ShipmentId, message.Items.Count);
            return Task.CompletedTask;
        }
    }
}
