using ECommerceApp.Application.Messaging;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Fulfillment.Messages
{
    public record ShipmentPartiallyDelivered(
        int ShipmentId,
        int OrderId,
        IReadOnlyList<ShipmentLineItem> DeliveredItems,
        IReadOnlyList<ShipmentLineItem> FailedItems,
        DateTime OccurredAt) : IMessage;
}
