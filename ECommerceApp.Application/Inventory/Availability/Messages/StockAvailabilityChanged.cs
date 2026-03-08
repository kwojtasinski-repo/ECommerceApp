using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Inventory.Availability.Messages
{
    public record StockAvailabilityChanged(
        int ProductId,
        int AvailableQuantity,
        DateTime OccurredAt) : IMessage;
}
