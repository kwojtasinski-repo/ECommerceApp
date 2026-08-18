using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Presale.Checkout.Messages
{
    public record CheckoutReservationAvailabilityDropped(
        int ProductId,
        int AvailableQuantity,
        DateTime OccurredAt) : IMessage;
}