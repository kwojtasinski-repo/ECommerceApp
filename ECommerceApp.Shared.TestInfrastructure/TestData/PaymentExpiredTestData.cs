using ECommerceApp.Application.Sales.Payments.Messages;
using System;

namespace ECommerceApp.Shared.TestInfrastructure.TestData
{
    public static class PaymentExpiredTestData
    {
        public static PaymentExpired Create(int orderId = 1, int paymentId = 1)
            => new(PaymentId: paymentId, OrderId: orderId, OccurredAt: DateTime.UtcNow);
    }
}
