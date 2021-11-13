using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CouponUsedRepository : GenericRepository<CouponUsed>, ICouponUsedRepository
    {
        public CouponUsedRepository(Context context) : base(context)
        {
        }

        public IQueryable<CouponUsed> GetAllCouponsUsed()
        {
            return _context.CouponUsed;
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

        public void UpdateCouponUsed(CouponUsed couponUsed)
        {
            _context.Attach(couponUsed);
            _context.Entry(couponUsed).Property("CouponId").IsModified = true;
            _context.Entry(couponUsed).Property("OrderId").IsModified = true;
            _context.SaveChanges();
        }
    }
}
