using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
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

        public Coupon GetCouponById(int couponId)
        {
            var coupon = _context.Coupons
                .Include(inc => inc.Type)
                .Include(inc => inc.CouponUsed).ThenInclude(inc => inc.Order)
                .FirstOrDefault(c => c.Id == couponId);
            return coupon;
        }

        public void UpdateCoupon(Coupon coupon)
        {
            _context.Attach(coupon);
            _context.Entry(coupon).Property("Code").IsModified = true;
            _context.Entry(coupon).Property("Discount").IsModified = true;
            _context.Entry(coupon).Property("Description").IsModified = true;
            _context.SaveChanges();
        }

        public List<Coupon> GetAllCoupons(string searchString)
        {
            return _context.Coupons
                        .Where(coupon => coupon.Code.StartsWith(searchString))
                        .ToList();
        }

        public List<Coupon> GetAllCoupons(int pageSize, int pageNo, string searchString)
        {
            return _context.Coupons
                    .Where(it => it.Code.StartsWith(searchString))
                    .Skip(pageSize * (pageNo - 1))
                    .Take(pageSize)
                    .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Coupons
                    .Where(it => it.Code.StartsWith(searchString))
                    .Count();
        }

        public List<Coupon> GetNotUsedCoupons()
        {
            return _context.Coupons.Where(c => !c.CouponUsedId.HasValue).ToList();
        }

        public List<Coupon> GetAllCoupons()
        {
            return _context.Coupons.ToList();
        }

        public Coupon GetByCouponUsed(int couponUsedId)
        {
            return _context.Coupons
                           .FirstOrDefault(c => c.CouponUsedId == couponUsedId);
        }

        public bool ExistsByCode(string code)
        {
            return _context.Coupons
                           .AsNoTracking()
                           .Any(c => c.Code.ToLower() ==  code.ToLower());
        }

        public Coupon GetByCode(string promoCode)
        {
            return _context.Coupons
                           .FirstOrDefault(c => c.Code.ToLower() == promoCode.ToLower());
        }

        public bool IsUnique(int id, string code)
        {
            return _context.Coupons
                           .AsNoTracking()
                           .Any(c => c.Id != id && c.Code.ToLower() == code.ToLower());
        }

        public bool ExistsById(int id)
        {
            return _context.Coupons
                           .AsNoTracking()
                           .Any(c => c.Id == id);
        }
    }
}
