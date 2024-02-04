using ECommerceApp.Domain.Model;
using System.Collections.Generic;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponRepository : IGenericRepository<Coupon>
    {
        void DeleteCoupon(int couponId);
        int AddCoupon(Coupon coupon);
        Coupon GetCouponById(int couponId);
        List<Coupon> GetAllCoupons();
        void UpdateCoupon(Coupon coupon);
        List<Coupon> GetAllCoupons(string searchString);
        List<Coupon> GetAllCoupons(int pageSize, int pageNo, string searchString);
        int GetCountBySearchString(string searchString);
        List<Coupon> GetNotUsedCoupons();
        Coupon GetByCouponUsed(int couponUsedId);
        bool ExistsByCode(string code);
        Coupon GetByCode(string promoCode);
        bool IsUnique(int id, string code);
        bool ExistsById(int id);
    }
}
