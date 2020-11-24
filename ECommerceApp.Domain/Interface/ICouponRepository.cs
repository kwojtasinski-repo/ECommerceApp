using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponRepository
    {
        void DeleteCoupon(int couponId);
        int AddCoupon(Coupon coupon);
        IQueryable<Coupon> GetCouponsByCouponTypeId(int couponTypeId);
        Coupon GetCouponById(int couponId);
    }
}
