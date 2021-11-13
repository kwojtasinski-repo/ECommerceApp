using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.Order;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ICouponService : IAbstractService<CouponVm, ICouponRepository, Coupon>
    {
        int AddCoupon(CouponVm coupon);
        void UpdateCoupon(CouponVm coupon);
        void DeleteCoupon(int id);
        ListForCouponVm GetAllCoupons(int pageSize, int pageNo, string searchString);
        CouponVm GetCoupon(int id);
        CouponDetailsVm GetCouponDetail(int id);
        IEnumerable<CouponVm> GetAllCoupons(Expression<Func<Coupon, bool>> expression);
    }
}
