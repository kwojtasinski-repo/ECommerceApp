using System;

namespace ECommerceApp.Domain.Sales.Orders.Events
{
    public record OrderDelivered(int OrderId, DateTime OccurredAt);
}
