using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ICouponUsedService : IAbstractService<CouponUsedVm, ICouponUsedRepository, CouponUsed>
    {
        int AddCouponUsed(CouponUsedVm couponUsedVm);
        void DeleteCouponUsed(int id);
        ListForCouponUsedVm GetAllCouponsUsed(int pageSize, int pageNo, string searchString);
        IQueryable<CouponUsedVm> GetAllCouponsUsed();
        CouponUsedDetailsVm GetCouponUsedDetail(int id);
        CouponUsedVm GetCouponUsed(int id);
        void UpdateCouponUsed(CouponUsedVm couponUsedVm);
        IEnumerable<CouponVm> GetAllCouponsUsed(Expression<Func<CouponUsed, bool>> expression);
    }
}
