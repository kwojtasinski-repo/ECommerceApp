using ECommerceApp.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Sales.Fulfillment
{
    public class Refund
    {
        public RefundId Id { get; private set; } = default!;
        public int OrderId { get; private set; }
        public string Reason { get; private set; } = default!;
        public bool OnWarranty { get; private set; }
        public RefundStatus Status { get; private set; }
        public DateTime RequestedAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }

        private readonly List<RefundItem> _items = new();
        public IReadOnlyList<RefundItem> Items => _items.AsReadOnly();

        private Refund() { }

        public static Refund Create(int orderId, string reason, bool onWarranty, IEnumerable<RefundItem> items)
        {
            if (orderId <= 0)
            {
                throw new DomainException("OrderId must be positive.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new DomainException("Reason is required.");
            }

            var itemList = items?.ToList() ?? throw new DomainException("Items are required.");
            if (!itemList.Any())
            {
                throw new DomainException("At least one refund item is required.");
            }

            var refund = new Refund
            {
                OrderId = orderId,
                Reason = reason,
                OnWarranty = onWarranty,
                Status = RefundStatus.Requested,
                RequestedAt = DateTime.UtcNow
            };
            refund._items.AddRange(itemList);
            return refund;
        }

        public void Approve()
        {
            if (Status != RefundStatus.Requested)
            {
                throw new DomainException($"Cannot approve refund — current status is '{Status}'.");
            }

            Status = RefundStatus.Approved;
            ProcessedAt = DateTime.UtcNow;
        }

        public void Reject()
        {
            if (Status != RefundStatus.Requested)
            {
                throw new DomainException($"Cannot reject refund — current status is '{Status}'.");
            }

            Status = RefundStatus.Rejected;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
