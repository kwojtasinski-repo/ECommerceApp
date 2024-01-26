using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CouponTypeRepository : GenericRepository<CouponType>, ICouponTypeRepository
    {
        public CouponTypeRepository(Context context) : base(context)
        {
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

        public void UpdateCouponType(CouponType couponType)
        {
            _context.Attach(couponType);
            _context.Entry(couponType).Property("Type").IsModified = true;
            _context.SaveChanges();
        }

        public List<CouponType> GetAllCouponTypes(int pageSize, int pageNo, string searchString)
        {
            return _context.CouponTypes
                        .Where(coupon => coupon.Type.StartsWith(searchString))
                        .Skip(pageSize * (pageNo - 1)).Take(pageSize)
                        .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.CouponTypes
                        .Where(coupon => coupon.Type.StartsWith(searchString))
                        .Count();
        }

        public List<CouponType> GetAllCouponTypes()
        {
            return _context.CouponTypes.ToList();
        }
    }
}
