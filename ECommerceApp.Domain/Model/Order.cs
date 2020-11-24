using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Order
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public double Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public virtual CouponUsed CouponUsed { get; set; } // 1:1 Order CouponUsed on one order can be discount
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public virtual Payment Payment { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public virtual Refund Refund { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } // 1:Many relation

    }
}
