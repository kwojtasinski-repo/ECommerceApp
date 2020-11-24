using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CouponRepository : ICouponRepository
    {
        private readonly Context _context;
        public CouponRepository(Context context)
        {
            _context = context;
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
            var coupons = _context.Coupons.Where(c => c.CouponTypeId == couponTypeId);
            return coupons;
        }

        public Coupon GetCouponById(int couponId)
        {
            var coupon = _context.Coupons.FirstOrDefault(c => c.Id == couponId);
            return coupon;
        }
    }
}
