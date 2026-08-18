using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Messages;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Domain.Presale.Checkout;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Presale.Checkout.Handlers
{
    internal sealed class CheckoutReservationRevertRequestedHandler : IMessageHandler<CheckoutReservationRevertRequested>
    {
        private readonly ISoftReservationService _softReservationService;

        public CheckoutReservationRevertRequestedHandler(ISoftReservationService softReservationService)
        {
            _softReservationService = softReservationService;
        }

        public Task HandleAsync(CheckoutReservationRevertRequested message, CancellationToken ct = default)
            => _softReservationService.RevertAllForUserAsync(new PresaleUserId(message.UserId), ct);
    }
}