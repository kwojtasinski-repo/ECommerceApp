using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record ShipmentFailed(
        int ShipmentId,
        int OrderId,
        IReadOnlyList<ShipmentLineItem> Items,
        DateTime OccurredAt) : IMessage;
}
