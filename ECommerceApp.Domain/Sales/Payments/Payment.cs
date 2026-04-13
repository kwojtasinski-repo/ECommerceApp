using ECommerceApp.Domain.Sales.Payments.Events;
using ECommerceApp.Domain.Shared;
using System;

namespace ECommerceApp.Domain.Sales.Payments
{
    public class Payment
    {
        public PaymentId Id { get; private set; } = default!;
        public Guid PaymentId { get; private set; }
        public PaymentOrderId OrderId { get; private set; } = default!;
        public string UserId { get; private set; } = default!;
        public decimal TotalAmount { get; private set; }
        public int CurrencyId { get; private set; }
        public PaymentStatus Status { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? ConfirmedAt { get; private set; }
        public string? TransactionRef { get; private set; }
        public byte[] RowVersion { get; private set; } = default!;

        private Payment() { }

        public static Payment Create(
            PaymentOrderId orderId,
            decimal totalAmount,
            int currencyId,
            DateTime expiresAt,
            string userId)
        {
            if (totalAmount < 0)
            {
                throw new DomainException("TotalAmount cannot be negative.");
            }
            if (currencyId <= 0)
            {
                throw new DomainException("CurrencyId must be positive.");
            }
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new DomainException("UserId is required.");
            }

            return new Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = orderId,
                UserId = userId,
                TotalAmount = totalAmount,
                CurrencyId = currencyId,
                Status = PaymentStatus.Pending,
                ExpiresAt = expiresAt
            };
        }

        public static Payment Create(
            PaymentId paymentId,
            PaymentOrderId orderId,
            decimal totalAmount,
            int currencyId,
            DateTime expiresAt,
            string userId)
        {
            if (paymentId is null || paymentId.Value <= 0)
            {
                throw new DomainException("PaymentId must be positive.");
            }
            if (orderId is null || orderId.Value <= 0)
            {
                throw new DomainException("OrderId must be positive.");
            }
            if (totalAmount < 0)
            {
                throw new DomainException("TotalAmount cannot be negative.");
            }
            if (currencyId <= 0)
            {
                throw new DomainException("CurrencyId must be positive.");
            }
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new DomainException("UserId is required.");
            }

            return new Payment
            {
                Id = paymentId,
                PaymentId = Guid.NewGuid(),
                OrderId = orderId,
                UserId = userId,
                TotalAmount = totalAmount,
                CurrencyId = currencyId,
                Status = PaymentStatus.Pending,
                ExpiresAt = expiresAt
            };
        }

        public PaymentConfirmedEvent Confirm(string? transactionRef = null)
        {
            if (Status != PaymentStatus.Pending)
            {
                throw new DomainException($"Cannot confirm payment — current status is '{Status}'.");
            }

            Status = PaymentStatus.Confirmed;
            ConfirmedAt = DateTime.UtcNow;
            TransactionRef = transactionRef;
            return new PaymentConfirmedEvent(Id.Value, OrderId.Value, DateTime.UtcNow);
        }

        public PaymentExpiredEvent Expire()
        {
            if (Status != PaymentStatus.Pending)
            {
                throw new DomainException($"Cannot expire payment — current status is '{Status}'.");
            }

            Status = PaymentStatus.Expired;
            return new PaymentExpiredEvent(Id.Value, OrderId.Value, DateTime.UtcNow);
        }

        public RefundIssuedEvent IssueRefund(int productId, int quantity)
        {
            if (Status != PaymentStatus.Confirmed)
            {
                throw new DomainException($"Cannot refund payment — current status is '{Status}'.");
            }
            if (quantity <= 0)
            {
                throw new DomainException("Refund quantity must be positive.");
            }

            Status = PaymentStatus.Refunded;
            return new RefundIssuedEvent(Id.Value, OrderId.Value, productId, quantity, DateTime.UtcNow);
        }

        public PaymentRefundedEvent Refund(int refundId)
        {
            if (Status != PaymentStatus.Confirmed)
            {
                throw new DomainException($"Cannot refund payment — current status is '{Status}'.");
            }

            Status = PaymentStatus.Refunded;
            return new PaymentRefundedEvent(Id.Value, OrderId.Value, refundId, DateTime.UtcNow);
        }

        public void Cancel()
        {
            if (Status != PaymentStatus.Pending)
            {
                throw new DomainException($"Cannot cancel payment — current status is '{Status}'.");
            }

            Status = PaymentStatus.Cancelled;
        }
    }
}
