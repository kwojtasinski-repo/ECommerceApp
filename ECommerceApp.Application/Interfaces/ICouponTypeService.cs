using ECommerceApp.Application.ViewModels.Coupon;
using ECommerceApp.Application.ViewModels.CouponType;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ICouponTypeService : IAbstractService<CouponTypeVm, ICouponTypeRepository, CouponType>
    {
        int AddCouponType(CouponTypeVm couponTypeVm);
        void DeleteCouponType(int id);
        ListForCouponTypeVm GetAllCouponsTypes(int pageSize, int pageNo, string searchString);
        CouponTypeDetailsVm GetCouponTypeDetail(int id);
        CouponTypeVm GetCouponType(int id);
        void UpdateCouponType(CouponTypeVm couponTypeVm);
        IEnumerable<CouponTypeVm> GetAllCouponsTypes(Expression<Func<CouponType, bool>> expression);
    }
}
