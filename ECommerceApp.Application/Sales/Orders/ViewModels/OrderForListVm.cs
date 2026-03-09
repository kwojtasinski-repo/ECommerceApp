using System;

namespace ECommerceApp.Application.Sales.Orders.ViewModels
{
    public sealed class OrderForListVm
    {
        public int Id { get; init; }
        public string Number { get; init; } = default!;
        public decimal Cost { get; init; }
        public DateTime Ordered { get; init; }
        public bool IsDelivered { get; init; }
        public bool IsPaid { get; init; }
        public int CustomerId { get; init; }
        public int CurrencyId { get; init; }
    }
}
