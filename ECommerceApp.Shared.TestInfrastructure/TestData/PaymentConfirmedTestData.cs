using ECommerceApp.Application.Sales.Payments.Messages;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class PaymentConfirmedTestData
    {
        public static PaymentConfirmed Create(
            int orderId = 1,
            int paymentId = 1,
            int productId = 10,
            int quantity = 2,
            bool includeItem = true)
        {
            return new PaymentConfirmed(
                PaymentId: paymentId,
                OrderId: orderId,
                Items: includeItem
                    ? new List<PaymentConfirmedItem> { new(ProductId: productId, Quantity: quantity) }
                    : new List<PaymentConfirmedItem>(),
                OccurredAt: DateTime.UtcNow);
        }
    }
}
