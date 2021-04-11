using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerceApp.Domain.Model
{
    public class Coupon : BaseEntity
    {
        public string Code { get; set; }
        public int Discount { get; set; }
        public string Description { get; set; }
        public int CouponTypeId { get; set; } // 1:Many CouponType Coupon
        public CouponType Type { get; set; }
        public int? CouponUsedId { get; set; } // 1:1 Coupon CouponUsed can be null
        public virtual CouponUsed CouponUsed { get; set; }
    }
}
