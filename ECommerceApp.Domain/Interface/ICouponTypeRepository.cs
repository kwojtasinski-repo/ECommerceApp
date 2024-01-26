using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponTypeRepository : IGenericRepository<CouponType>
    {
        void DeleteCouponType(int couponTypeId);
        int AddCouponType(CouponType couponType);
        CouponType GetCouponTypeById(int couponTypeId);
        List<CouponType> GetAllCouponTypes(int pageSize, int pageNo, string searchString);
        void UpdateCouponType(CouponType couponType);
        int GetCountBySearchString(string searchString);
        List<CouponType> GetAllCouponTypes();
    }
}
