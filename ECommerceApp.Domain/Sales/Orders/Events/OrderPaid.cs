using System;

namespace ECommerceApp.Domain.Sales.Orders.Events
{
    public record OrderPaid(int OrderId, int PaymentId, DateTime OccurredAt);
}
