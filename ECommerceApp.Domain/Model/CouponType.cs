using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class CouponType
    {
        public int Id { get; set; }
        public string Type { get; set; } // Type Coupon for only one Order; for only one Item
        
        public ICollection<Coupon> Coupons { get; set; } // 1:Many CouponType Coupon
    }
}
