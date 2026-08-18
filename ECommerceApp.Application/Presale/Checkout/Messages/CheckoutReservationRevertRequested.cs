using ECommerceApp.Application.Messaging;

namespace ECommerceApp.Application.Presale.Checkout.Messages
{
    public record CheckoutReservationRevertRequested(string UserId) : IMessage;
}