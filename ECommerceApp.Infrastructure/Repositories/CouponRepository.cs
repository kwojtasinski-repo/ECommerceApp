using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
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

        public IQueryable<CouponType> GetAllCouponsTypes()
        {
            return _context.CouponTypes;
        }

        public CouponType GetCouponTypeById(int couponTypeId)
        {
            var coupontType = _context.CouponTypes.FirstOrDefault(c => c.Id == couponTypeId);
            return coupontType;
        }

        public void DeleteCouponType(int couponTypeId)
        {
            var couponType = _context.CouponTypes.Find(couponTypeId);

            if (couponType != null)
            {
                _context.CouponTypes.Remove(couponType);
                _context.SaveChanges();
            }
        }

        public int AddCouponType(CouponType couponType)
        {
            _context.CouponTypes.Add(couponType);
            _context.SaveChanges();
            return couponType.Id;
        }

        public IQueryable<CouponUsed> GetAllCouponsUsed()
        {
            return _context.CouponUsed;
        }

        public IQueryable<CouponUsed> GetAllCouponsUsedType(int couponTypeId)
        {
            return _context.CouponUsed
                .Include(c => c.Coupon).ThenInclude(c => c.Type)
                .Where(c => c.Coupon.Type.Id == couponTypeId);
        }

        public CouponUsed GetCouponUsedById(int couponUsedId)
        {
            var couponUsed = _context.CouponUsed.FirstOrDefault(c => c.Id == couponUsedId);
            return couponUsed;
        }

        public void DeleteCouponUsed(int couponUsedId)
        {
            var couponUsed = _context.CouponUsed.Find(couponUsedId);

            if (couponUsed != null)
            {
                _context.CouponUsed.Remove(couponUsed);
                _context.SaveChanges();
            }
        }

        public int AddCouponUsed(CouponUsed couponUsed)
        {
            _context.CouponUsed.Add(couponUsed);
            _context.SaveChanges();
            return couponUsed.Id;
        }

        public void UpdateCoupon(Coupon coupon)
        {
            _context.Attach(coupon);
            _context.Entry(coupon).Property("Code").IsModified = true;
            _context.Entry(coupon).Property("Discount").IsModified = true;
            _context.Entry(coupon).Property("Description").IsModified = true;
            _context.SaveChanges();
        }

        public void UpdateCouponType(CouponType couponType)
        {
            _context.Attach(couponType);
            _context.Entry(couponType).Property("Type").IsModified = true;
            _context.SaveChanges();
        }

        public void UpdateCouponUsed(CouponUsed couponUsed)
        {
            _context.Attach(couponUsed);
            _context.Entry(couponUsed).Property("CouponId").IsModified = true;
            _context.Entry(couponUsed).Property("OrderId").IsModified = true;
            _context.SaveChanges();
        }

        public IQueryable<Order> GetAllOrders()
        {
            return _context.Orders;
        }
    }
}
