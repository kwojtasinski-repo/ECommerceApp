using System.Collections.Generic;

namespace ECommerceApp.Domain.Model
{
    public class CouponUsed : BaseEntity
    {
        public int CouponId { get; set; }
        public virtual Coupon Coupon { get; set; }
        public int OrderId { get; set; } // OrderId for order discount relation 1:1
        public virtual Order Order { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } 
    }
}
