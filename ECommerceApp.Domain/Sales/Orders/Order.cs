using ECommerceApp.Domain.Sales.Orders.Events.Payloads;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ECommerceApp.Domain.Sales.Orders
{
    public class Order
    {
        public OrderId Id { get; private set; }
        public OrderNumber Number { get; private set; } = default!;
        public decimal Cost { get; private set; }
        public DateTime Ordered { get; private set; }
        public int? DiscountPercent { get; private set; }
        public int CustomerId { get; private set; }
        public int CurrencyId { get; private set; }
        public OrderUserId UserId { get; private set; }
        public int? CouponUsedId { get; private set; }
        public OrderStatus Status { get; private set; }
        public OrderCustomer Customer { get; private set; } = default!;

        private readonly List<OrderItem> _orderItems = new();
        public IReadOnlyList<OrderItem> OrderItems => _orderItems.AsReadOnly();

        private readonly List<OrderEvent> _events = new();
        public IReadOnlyList<OrderEvent> Events => _events.AsReadOnly();

        private Order() { }

        public static Order Create(
            int customerId,
            int currencyId,
            OrderUserId userId,
            OrderNumber number,
            OrderCustomer customer)
        {
            if (customerId <= 0)
                throw new DomainException("CustomerId must be positive.");
            if (currencyId <= 0)
                throw new DomainException("CurrencyId must be positive.");
            if (userId is null || string.IsNullOrWhiteSpace(userId.Value))
                throw new DomainException("UserId is required.");
            if (number is null)
                throw new DomainException("Order number is required.");
            if (customer is null)
                throw new DomainException("OrderCustomer is required.");

            var order = new Order
            {
                CustomerId = customerId,
                CurrencyId = currencyId,
                UserId = userId,
                Number = number,
                Customer = customer,
                Ordered = DateTime.UtcNow,
                Status = OrderStatus.Placed
            };

            order.AppendEvent(OrderEventType.OrderPlaced);
            return order;
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
            Cost = _orderItems.Sum(i => i.UnitCost.Amount * i.Quantity * discountRate);
        }

        public void ConfirmPayment(int paymentId)
        {
            if (Status != OrderStatus.Placed)
                throw new DomainException($"Order '{Id?.Value}' cannot confirm payment — current status is '{Status}'.");
            if (paymentId <= 0)
                throw new DomainException("PaymentId must be positive.");

            Status = OrderStatus.PaymentConfirmed;
            AppendEvent(OrderEventType.OrderPaymentConfirmed, new PaymentConfirmedPayload(paymentId));
        }

        public void Fulfill()
        {
            if (Status is not (OrderStatus.PaymentConfirmed or OrderStatus.PartiallyFulfilled))
                throw new DomainException($"Order '{Id?.Value}' cannot be fulfilled — current status is '{Status}'.");

            Status = OrderStatus.Fulfilled;
            AppendEvent(OrderEventType.OrderFulfilled);
        }

        public void Cancel(string reason)
        {
            if (Status != OrderStatus.Placed)
                throw new DomainException($"Order '{Id?.Value}' cannot be cancelled — current status is '{Status}'.");

            Status = OrderStatus.Cancelled;
            AppendEvent(OrderEventType.OrderCancelled, new OrderCancelledPayload(reason));
        }

        public void ExpirePayment()
        {
            if (Status != OrderStatus.Placed)
                throw new DomainException($"Order '{Id?.Value}' cannot expire — current status is '{Status}'.");

            Status = OrderStatus.Cancelled;
            AppendEvent(OrderEventType.OrderPaymentExpired);
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
                item.ApplyCoupon(couponUsedId);

            CalculateCost();
            AppendEvent(OrderEventType.CouponApplied, new CouponAppliedPayload(couponUsedId, discountPercent));
        }

        public void RemoveCoupon()
        {
            if (CouponUsedId is null)
                throw new DomainException($"Order '{Id?.Value}' has no coupon to remove.");

            var removedId = CouponUsedId.Value;
            CouponUsedId = null;
            DiscountPercent = null;

            foreach (var item in _orderItems)
                item.RemoveCoupon();

            CalculateCost();
            AppendEvent(OrderEventType.CouponRemoved, new CouponRemovedPayload(removedId));
        }

        public void AssignRefund(int refundId)
        {
            if (refundId <= 0)
                throw new DomainException("RefundId must be positive.");

            AppendEvent(OrderEventType.RefundAssigned, new RefundAssignedPayload(refundId));
        }

        public void RemoveRefund()
        {
            AppendEvent(OrderEventType.RefundRemoved);
        }

        public void AdjustPrice(decimal newCost)
        {
            if (newCost < 0)
                throw new DomainException("Cost cannot be negative.");

            Cost = newCost;
            AppendEvent(OrderEventType.PriceAdjusted);
        }

        public void RecordShipmentDispatched()
        {
            if (Status != OrderStatus.PaymentConfirmed)
                return;

            AppendEvent(OrderEventType.ShipmentDispatched);
        }

        public void MarkAsPartiallyFulfilled()
        {
            if (Status is not (OrderStatus.PaymentConfirmed or OrderStatus.PartiallyFulfilled))
                return;

            Status = OrderStatus.PartiallyFulfilled;
            AppendEvent(OrderEventType.PartiallyFulfilled);
        }

        public void RecordShipmentFailure()
        {
            AppendEvent(OrderEventType.ShipmentFailed);
        }

        private void AppendEvent<T>(OrderEventType type, T payload)
            => _events.Add(new OrderEvent(Id ?? new OrderId(0), type,
                JsonSerializer.Serialize(payload)));

        private void AppendEvent(OrderEventType type)
            => _events.Add(new OrderEvent(Id ?? new OrderId(0), type, null));
    }
}
