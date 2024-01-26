using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CouponUsedRepository : GenericRepository<CouponUsed>, ICouponUsedRepository
    {
        public CouponUsedRepository(Context context) : base(context)
        {
        }

        public List<CouponUsed> GetAllCouponsUsed()
        {
            return _context.CouponUsed.ToList();
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

        public List<CouponUsed> GetAllCouponsUsed(int pageSize, int pageNo)
        {
            return _context.CouponUsed
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCount()
        {
            return _context.CouponUsed.Count();
        }
    }
}
