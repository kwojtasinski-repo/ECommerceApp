using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class CouponUsed
    {
        public int Id { get; set; }
        public int CouponId { get; set; }
        public virtual Coupon Coupon { get; set; }
        public int OrderId { get; set; } // OrderId for order discount relation 1:1
        public virtual Order Order { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } 
    }
}
