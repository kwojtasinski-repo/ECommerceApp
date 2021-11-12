using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponUsedRepository : IGenericRepository<CouponUsed>
    {
        void DeleteCouponUsed(int couponId);
        int AddCouponUsed(CouponUsed coupon);
        CouponUsed GetCouponUsedById(int couponId);
        IQueryable<CouponUsed> GetAllCouponsUsed();
        void UpdateCouponUsed(CouponUsed couponUsed);
    }
}
