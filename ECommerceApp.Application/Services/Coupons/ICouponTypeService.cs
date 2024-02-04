using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.CouponType;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Coupons
{
    public interface ICouponTypeService : IAbstractService<CouponTypeVm, ICouponTypeRepository, CouponType>
    {
        int AddCouponType(CouponTypeVm couponTypeVm);
        bool DeleteCouponType(int id);
        ListForCouponTypeVm GetAllCouponsTypes(int pageSize, int pageNo, string searchString);
        CouponTypeDetailsVm GetCouponTypeDetail(int id);
        CouponTypeVm GetCouponType(int id);
        bool UpdateCouponType(CouponTypeVm couponTypeVm);
        IEnumerable<CouponTypeVm> GetAllCouponsTypes();
    }
}
