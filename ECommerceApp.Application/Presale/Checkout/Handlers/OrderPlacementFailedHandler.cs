using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.Messages;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class OrderPlacementFailedHandler : IMessageHandler<OrderPlacementFailed>
    {
        private readonly ILogger<OrderPlacementFailedHandler> _logger;

        public OrderPlacementFailedHandler(ILogger<OrderPlacementFailedHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(OrderPlacementFailed message, CancellationToken ct = default)
        {
            // TODO: Restore cart items once ICartService.RestoreAsync is implemented.
            // Cart items cleared by Presale.OrderPlacedHandler cannot be automatically recovered.
            // Soft reservations removed by OrderPlacedHandler cannot be restored here either.
            // The user must re-add items to their cart manually.
            _logger.LogWarning(
                "OrderPlacementFailed for order {OrderId}. Cart for user {UserId} was already cleared and cannot be automatically restored. Reason: {Reason}",
                message.OrderId, message.UserId, message.Reason);
            return Task.CompletedTask;
        }
    }
}
