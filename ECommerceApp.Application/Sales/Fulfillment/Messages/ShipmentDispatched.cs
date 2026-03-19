using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record ShipmentDispatched(
        int ShipmentId,
        int OrderId,
        string TrackingNumber,
        DateTime OccurredAt) : IMessage;
}
