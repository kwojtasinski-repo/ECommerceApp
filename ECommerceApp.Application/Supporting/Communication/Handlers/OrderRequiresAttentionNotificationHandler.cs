using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Communication.Handlers
{
    /// <summary>
    /// Handles internal operator alerts — routes to logging only.
    /// Channel (Slack, email-to-ops, etc.) is deferred to a future infrastructure decision.
    /// </summary>
    internal sealed class OrderRequiresAttentionNotificationHandler : IMessageHandler<OrderRequiresAttention>
    {
        private readonly ILogger<OrderRequiresAttentionNotificationHandler> _logger;

        public OrderRequiresAttentionNotificationHandler(ILogger<OrderRequiresAttentionNotificationHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(OrderRequiresAttention message, CancellationToken ct = default)
        {
            _logger.LogWarning(
                "[Communication][OperatorAlert] OrderId={OrderId} requires attention: {Reason}",
                message.OrderId, message.Reason);
            return Task.CompletedTask;
        }
    }
}
