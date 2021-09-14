using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponRepository : IGenericRepository<Coupon>
    {
        void DeleteCoupon(int couponId);
        int AddCoupon(Coupon coupon);
        IQueryable<Coupon> GetCouponsByCouponTypeId(int couponTypeId);
        Coupon GetCouponById(int couponId);
        IQueryable<Coupon> GetAllCoupons();
        IQueryable<CouponType> GetAllCouponsTypes();
        CouponType GetCouponTypeById(int couponTypeId);
        void DeleteCouponType(int couponTypeId);
        int AddCouponType(CouponType couponType);
        IQueryable<CouponUsed> GetAllCouponsUsed();
        IQueryable<CouponUsed> GetAllCouponsUsedType(int couponTypeId);
        CouponUsed GetCouponUsedById(int couponUsedId);
        void DeleteCouponUsed(int couponUsedId);
        int AddCouponUsed(CouponUsed couponUsed);
        void UpdateCoupon(Coupon coupon);
        void UpdateCouponType(CouponType couponType);
        void UpdateCouponUsed(CouponUsed couponUsed);
        IQueryable<Order> GetAllOrders();
    }
}
