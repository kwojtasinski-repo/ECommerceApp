using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class ShipmentDeliveredTestData
    {
        public static ShipmentDelivered Create(
            int orderId = 1,
            int shipmentId = 1,
            int productId = 10,
            int quantity = 2)
        {
            return new ShipmentDelivered(
                ShipmentId: shipmentId,
                OrderId: orderId,
                Items: new List<ShipmentLineItem> { new(ProductId: productId, Quantity: quantity) },
                OccurredAt: DateTime.UtcNow);
        }
    }
}
