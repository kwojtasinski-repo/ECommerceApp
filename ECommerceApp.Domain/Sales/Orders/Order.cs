using ECommerceApp.Domain.Sales.Orders.Events;
using ECommerceApp.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Sales.Orders
{
    public class Order
    {
        public OrderId Id { get; private set; }
        public string Number { get; private set; } = default!;
        public decimal Cost { get; private set; }
        public DateTime Ordered { get; private set; }
        public DateTime? Delivered { get; private set; }
        public bool IsDelivered { get; private set; }
        public bool IsPaid { get; private set; }
        public int? DiscountPercent { get; private set; }
        public int CustomerId { get; private set; }
        public int CurrencyId { get; private set; }
        public string UserId { get; private set; } = default!;
        public int? PaymentId { get; private set; }
        public int? RefundId { get; private set; }
        public int? CouponUsedId { get; private set; }

        private readonly List<OrderItem> _orderItems = new();
        public IReadOnlyList<OrderItem> OrderItems => _orderItems.AsReadOnly();

        private Order() { }

        public static Order Create(int customerId, int currencyId, string userId, string number, decimal cost = 0m)
        {
            if (customerId <= 0)
                throw new DomainException("CustomerId must be positive.");
            if (currencyId <= 0)
                throw new DomainException("CurrencyId must be positive.");
            if (string.IsNullOrWhiteSpace(userId))
                throw new DomainException("UserId is required.");
            if (string.IsNullOrWhiteSpace(number))
                throw new DomainException("Order number is required.");
            if (cost < 0)
                throw new DomainException("Cost cannot be negative.");

            return new Order
            {
                Id = new OrderId(0),
                CustomerId = customerId,
                CurrencyId = currencyId,
                UserId = userId,
                Number = number,
                Cost = cost,
                Ordered = DateTime.UtcNow
            };
        }

        public void AddItem(OrderItem item)
        {
            if (item is null)
                throw new DomainException("OrderItem cannot be null.");
            _orderItems.Add(item);
        }

        public void Update(int customerId, int currencyId)
        {
            if (customerId <= 0)
                throw new DomainException("CustomerId must be positive.");
            if (currencyId <= 0)
                throw new DomainException("CurrencyId must be positive.");
            CustomerId = customerId;
            CurrencyId = currencyId;
        }

        public void CalculateCost()
        {
            var discountRate = DiscountPercent.HasValue
                ? 1m - DiscountPercent.Value / 100m
                : 1m;
            Cost = _orderItems.Sum(i => i.UnitCost * i.Quantity * discountRate);
        }

        public OrderPaid MarkAsPaid(int paymentId)
        {
            if (IsPaid)
                throw new DomainException($"Order '{Id?.Value}' is already paid.");
            if (paymentId <= 0)
                throw new DomainException("PaymentId must be positive.");

            IsPaid = true;
            PaymentId = paymentId;
            return new OrderPaid(Id.Value, paymentId, DateTime.UtcNow);
        }

        public OrderDelivered MarkAsDelivered()
        {
            if (!IsPaid)
                throw new DomainException($"Order '{Id?.Value}' is not paid.");
            if (IsDelivered)
                throw new DomainException($"Order '{Id?.Value}' is already delivered.");

            IsDelivered = true;
            Delivered = DateTime.UtcNow;
            return new OrderDelivered(Id.Value, DateTime.UtcNow);
        }

        public void AssignCoupon(int couponUsedId, int discountPercent)
        {
            if (couponUsedId <= 0)
                throw new DomainException("CouponUsedId must be positive.");
            if (discountPercent < 0 || discountPercent > 100)
                throw new DomainException("DiscountPercent must be between 0 and 100.");

            CouponUsedId = couponUsedId;
            DiscountPercent = discountPercent;

            foreach (var item in _orderItems)
            {
                item.ApplyCoupon(couponUsedId);
            }

            CalculateCost();
        }

        public void RemoveCoupon()
        {
            CouponUsedId = null;
            DiscountPercent = null;

            foreach (var item in _orderItems)
            {
                item.RemoveCoupon();
            }

            CalculateCost();
        }

        public void AssignRefund(int refundId)
        {
            if (refundId <= 0)
                throw new DomainException("RefundId must be positive.");

            RefundId = refundId;

            foreach (var item in _orderItems)
            {
                item.AssignRefund(refundId);
            }
        }

        public void RemoveRefund()
        {
            RefundId = null;

            foreach (var item in _orderItems)
            {
                item.RemoveRefund();
            }
        }
    }
}
