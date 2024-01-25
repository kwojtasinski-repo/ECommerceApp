using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CouponRepository : GenericRepository<Coupon>, ICouponRepository
    {
        public CouponRepository(Context context) : base(context)
        {
        }

        public void DeleteCoupon(int couponId)
        {
            var coupon = _context.Coupons.Find(couponId);

            if (coupon != null)
            {
                _context.Coupons.Remove(coupon);
                _context.SaveChanges();
            }
        }

        public int AddCoupon(Coupon coupon)
        {
            _context.Coupons.Add(coupon);
            _context.SaveChanges();
            return coupon.Id;
        }

        public IQueryable<Coupon> GetCouponsByCouponTypeId(int couponTypeId)
        {
            return _context.Coupons.Where(c => c.CouponTypeId == couponTypeId);
        }

        public Coupon GetCouponById(int couponId)
        {
            var coupon = _context.Coupons
                .Include(inc => inc.Type)
                .Include(inc => inc.CouponUsed).ThenInclude(inc => inc.Order)
                .FirstOrDefault(c => c.Id == couponId);
            return coupon;
        }

        public IQueryable<Coupon> GetAllCoupons()
        {
            return _context.Coupons;
        }

        public void UpdateCoupon(Coupon coupon)
        {
            _context.Attach(coupon);
            _context.Entry(coupon).Property("Code").IsModified = true;
            _context.Entry(coupon).Property("Discount").IsModified = true;
            _context.Entry(coupon).Property("Description").IsModified = true;
            _context.SaveChanges();
        }
    }
}
