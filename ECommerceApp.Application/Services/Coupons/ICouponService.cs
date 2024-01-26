using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Coupons
{
    public interface ICouponService : IAbstractService<CouponVm, ICouponRepository, Coupon>
    {
        int AddCoupon(CouponVm coupon);
        void UpdateCoupon(CouponVm coupon);
        void DeleteCoupon(int id);
        ListForCouponVm GetAllCoupons(int pageSize, int pageNo, string searchString);
        ListForCouponVm GetAllCoupons();
        CouponVm GetCoupon(int id);
        CouponDetailsVm GetCouponDetail(int id);
        void DeleteCouponUsed(int couponId, int couponUsedId);
        void AddCouponUsed(int couponId, int couponUsedId);
        CouponVm GetCouponByCode(string promoCode);
        List<CouponVm> GetAllCouponsNotUsed();
        CouponVm GetByCouponUsed(int couponUsedId);
    }
}
