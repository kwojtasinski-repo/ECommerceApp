using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.DTOs;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Presale.Checkout;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class OrderPlacementFailedHandler : IMessageHandler<OrderPlacementFailed>
    {
        private readonly ICartService _cartService;
        private readonly ILogger<OrderPlacementFailedHandler> _logger;

        public OrderPlacementFailedHandler(ICartService cartService, ILogger<OrderPlacementFailedHandler> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        public async Task HandleAsync(OrderPlacementFailed message, CancellationToken ct = default)
        {
            try
            {
                var items = message.Items.Select(i => new CartRestoreItem(i.ProductId, i.Quantity)).ToList();
                await _cartService.RestoreAsync(new PresaleUserId(message.UserId), items, ct);
                _logger.LogInformation(
                    "OrderPlacementFailed for order {OrderId}. Cart for user {UserId} restored. Reason: {Reason}",
                    message.OrderId, message.UserId, message.Reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "OrderPlacementFailed for order {OrderId}. Failed to restore cart for user {UserId}.",
                    message.OrderId, message.UserId);
            }
        }
    }
}
