using ECommerceApp.Domain.Sales.Orders;
using System;
using System.Collections.Generic;

namespace ECommerceApp.Application.Sales.Orders.ViewModels
{
    public sealed class OrderItemVm
    {
        public int Id { get; init; }
        public int ItemId { get; init; }
        public int Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public int? CouponUsedId { get; init; }
        public string? ProductName { get; init; }
        public string? ImageFileName { get; init; }
    }

    public sealed class OrderCustomerVm
    {
        public string FirstName { get; init; } = default!;
        public string LastName { get; init; } = default!;
        public string Email { get; init; } = default!;
        public string PhoneNumber { get; init; } = default!;
        public bool IsCompany { get; init; }
        public string? CompanyName { get; init; }
        public string? Nip { get; init; }
        public string Street { get; init; } = default!;
        public string BuildingNumber { get; init; } = default!;
        public string? FlatNumber { get; init; }
        public string ZipCode { get; init; } = default!;
        public string City { get; init; } = default!;
        public string Country { get; init; } = default!;
    }

    public sealed class OrderEventVm
    {
        public string EventType { get; init; } = default!;
        public DateTime OccurredAt { get; init; }
    }

    public sealed class OrderDetailsVm
    {
        public int Id { get; init; }
        public string Number { get; init; } = default!;
        public decimal Cost { get; init; }
        public DateTime Ordered { get; init; }
        public OrderStatus Status { get; init; }
        public int CustomerId { get; init; }
        public int CurrencyId { get; init; }
        public string UserId { get; init; } = default!;
        public int? CouponUsedId { get; init; }
        public int? DiscountPercent { get; init; }
        public OrderCustomerVm? Customer { get; init; }
        public IReadOnlyList<OrderItemVm> OrderItems { get; init; } = Array.Empty<OrderItemVm>();
        public IReadOnlyList<OrderEventVm> Events { get; init; } = Array.Empty<OrderEventVm>();
    }
}
