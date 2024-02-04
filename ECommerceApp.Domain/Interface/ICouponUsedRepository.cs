using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponUsedRepository : IGenericRepository<CouponUsed>
    {
        void DeleteCouponUsed(int couponUsedId);
        int AddCouponUsed(CouponUsed couponUsed);
        CouponUsed GetCouponUsedById(int couponUsedId);
        List<CouponUsed> GetAllCouponsUsed();
        void UpdateCouponUsed(CouponUsed couponUsed);
        List<CouponUsed> GetAllCouponsUsed(int pageSize, int pageNo);
        int GetCount();
        bool ExistsById(int id);
    }
}
