using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.CouponUsed;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Coupons
{
    public interface ICouponUsedService : IAbstractService<CouponUsedVm, ICouponUsedRepository, CouponUsed>
    {
        int AddCouponUsed(CouponUsedVm couponUsedVm);
        bool DeleteCouponUsed(int id);
        ListForCouponUsedVm GetAllCouponsUsed(int pageSize, int pageNo, string searchString);
        List<CouponUsedVm> GetAllCouponsUsed();
        CouponUsedDetailsVm GetCouponUsedDetail(int id);
        CouponUsedVm GetCouponUsed(int id);
        bool UpdateCouponUsed(CouponUsedVm couponUsedVm);
    }
}
