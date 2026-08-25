using ECommerceApp.Application.Sales.Orders.Messages;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class OrderCancelledTestData
    {
        public static OrderCancelled Create(
            int orderId = 1,
            int productId = 10,
            int quantity = 2,
            bool includeItem = true)
        {
            return new OrderCancelled(
                OrderId: orderId,
                Items: includeItem
                    ? new List<OrderCancelledItem> { new(ProductId: productId, Quantity: quantity) }
                    : new List<OrderCancelledItem>(),
                OccurredAt: DateTime.UtcNow);
        }
    }
}
