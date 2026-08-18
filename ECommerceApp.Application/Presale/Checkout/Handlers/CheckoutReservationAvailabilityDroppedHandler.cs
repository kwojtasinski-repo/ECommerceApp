using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Services;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class CheckoutReservationAvailabilityDroppedHandler : IMessageHandler<CheckoutReservationAvailabilityDropped>
    {
        private readonly ISoftReservationService _softReservationService;

        public CheckoutReservationAvailabilityDroppedHandler(ISoftReservationService softReservationService)
        {
            _softReservationService = softReservationService;
        }

        public Task HandleAsync(CheckoutReservationAvailabilityDropped message, CancellationToken ct = default)
            => _softReservationService.InvalidateExcessForProductAsync(message.ProductId, message.AvailableQuantity, ct);
    }
}