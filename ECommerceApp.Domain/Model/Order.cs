using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Domain.Model
{
    public class Order : BaseEntity
    {
        public string Number { get; set; }
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public virtual CouponUsed CouponUsed { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public int? PaymentId { get; set; }
        public bool IsPaid { get; set; }
        public virtual Payment Payment { get; set; }
        public int? RefundId { get; set; }
        public virtual Refund Refund { get; set; }
        public int CurrencyId { get; set; }
        public Currency Currency { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }

        public void CalculateCost()
        {
            if (OrderItems is null || !OrderItems.Any())
            {
                Cost = 0;
                return;
            }

            var cost = 0M;
            var discount = (1 - (CouponUsed?.Coupon?.Discount / 100M) ?? 1);
            foreach (var orderItem in OrderItems)
            {
                cost += orderItem.Item.Cost * orderItem.ItemOrderQuantity * discount;
            }
            Cost = cost;
        }
    }
}
