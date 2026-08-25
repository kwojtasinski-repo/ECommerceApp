using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class ShipmentPartiallyDeliveredTestData
    {
        public static ShipmentPartiallyDelivered Create(
            int orderId = 1,
            int shipmentId = 1,
            int productId = 10,
            int quantity = 2)
        {
            return new ShipmentPartiallyDelivered(
                ShipmentId: shipmentId,
                OrderId: orderId,
                DeliveredItems: new List<ShipmentLineItem> { new(ProductId: productId, Quantity: quantity) },
                FailedItems: new List<ShipmentLineItem>(),
                OccurredAt: DateTime.UtcNow);
        }
    }
}
