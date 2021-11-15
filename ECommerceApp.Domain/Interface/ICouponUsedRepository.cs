using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponUsedRepository : IGenericRepository<CouponUsed>
    {
        void DeleteCouponUsed(int couponUsedId);
        int AddCouponUsed(CouponUsed couponUsed);
        CouponUsed GetCouponUsedById(int couponUsedId);
        IQueryable<CouponUsed> GetAllCouponsUsed();
        void UpdateCouponUsed(CouponUsed couponUsed);
    }
}
