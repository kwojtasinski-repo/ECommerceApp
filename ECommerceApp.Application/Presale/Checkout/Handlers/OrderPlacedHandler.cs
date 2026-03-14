using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Sales.Orders.Messages;
using ECommerceApp.Domain.Presale.Checkout;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
    {
        private readonly ICartService _cartService;
        private readonly ISoftReservationService _softReservationService;

        public OrderPlacedHandler(
            ICartService cartService,
            ISoftReservationService softReservationService)
        {
            _cartService = cartService;
            _softReservationService = softReservationService;
        }

        public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
        {
            PresaleUserId userId = message.UserId;
            IReadOnlyList<PresaleProductId> productIds = message.Items.Select(i => new PresaleProductId(i.ProductId)).ToList();
            await _cartService.RemoveRangeAsync(userId, productIds, ct);
            await _softReservationService.RemoveCommittedForUserAsync(message.UserId, ct);
        }
    }
}
