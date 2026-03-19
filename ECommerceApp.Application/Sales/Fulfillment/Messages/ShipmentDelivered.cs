using ECommerceApp.Application.Messaging;
using System;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record ShipmentDelivered(
        int ShipmentId,
        int OrderId,
        DateTime OccurredAt) : IMessage;
}
