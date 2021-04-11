using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Order : BaseEntity
    {
        public int Number { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Cost { get; set; }
        public DateTime Ordered { get; set; }
        public DateTime? Delivered { get; set; }
        public bool IsDelivered { get; set; }
        public int? CouponUsedId { get; set; }
        public virtual CouponUsed CouponUsed { get; set; } // 1:1 Order CouponUsed on one order can be discount
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public string UserId { get; set; }
        public IdentityUser User { get; set; }
        public int? PaymentId { get; set; } // 1:1 Order Payment
        public bool IsPaid { get; set; }
        public virtual Payment Payment { get; set; }
        public int? RefundId { get; set; } // 1:1 Order Refund
        public virtual Refund Refund { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } // 1:Many relation
    }
}
