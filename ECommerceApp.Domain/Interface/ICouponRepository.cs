using ECommerceApp.Domain.Model;
using System.Linq;

namespace ECommerceApp.Domain.Interface
{
    public interface ICouponRepository : IGenericRepository<Coupon>
    {
        void DeleteCoupon(int couponId);
        int AddCoupon(Coupon coupon);
        IQueryable<Coupon> GetCouponsByCouponTypeId(int couponTypeId);
        Coupon GetCouponById(int couponId);
        IQueryable<Coupon> GetAllCoupons();
        void UpdateCoupon(Coupon coupon);
    }
}
