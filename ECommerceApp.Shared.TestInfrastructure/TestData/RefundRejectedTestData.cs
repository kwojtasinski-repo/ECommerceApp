using ECommerceApp.Application.Sales.Fulfillment.Messages;
using System;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class RefundRejectedTestData
    {
        public static RefundRejected Create(int orderId = 1, int refundId = 1)
            => new(RefundId: refundId, OrderId: orderId, OccurredAt: DateTime.UtcNow);
    }
}
