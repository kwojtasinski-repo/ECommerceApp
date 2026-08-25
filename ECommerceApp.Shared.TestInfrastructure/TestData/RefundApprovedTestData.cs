using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class RefundApprovedTestData
    {
        public static RefundApproved Create(
            int orderId = 1,
            int refundId = 1,
            int productId = 10,
            int quantity = 2,
            bool includeItem = true)
        {
            return new RefundApproved(
                RefundId: refundId,
                OrderId: orderId,
                Items: includeItem
                    ? new List<RefundApprovedItem> { new(ProductId: productId, Quantity: quantity) }
                    : new List<RefundApprovedItem>(),
                OccurredAt: DateTime.UtcNow);
        }
    }
}
