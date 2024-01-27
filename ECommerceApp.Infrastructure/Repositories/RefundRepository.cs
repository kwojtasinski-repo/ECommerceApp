using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class RefundRepository : GenericRepository<Refund>, IRefundRepository
    {
        public RefundRepository(Context context) : base(context)
        {
        }

        public int AddRefund(Refund refund)
        {
            _context.Refunds.Add(refund);
            _context.SaveChanges();
            return refund.Id;
        }

        public void DeleteRefund(int refundId)
        {
            var refund = _context.Refunds.Find(refundId);

            if (refund != null)
            {
                _context.Refunds.Remove(refund);
                _context.SaveChanges();
            }
        }

        public bool ExistsByReason(string reasonRefund)
        {
            return _context.Refunds
                           .AsNoTracking()
                           .Any(r => string.Equals(r.Reason, reasonRefund,
                                StringComparison.OrdinalIgnoreCase));
        }

        public List<Refund> GetAllRefunds()
        {
            return _context.Refunds.ToList();
        }

        public List<Refund> GetAllRefunds(int pageSize, int pageNo, string searchString)
        {
            return _context.Refunds
                           .Where(r => r.Reason.StartsWith(searchString)
                                || r.RefundDate.ToString().StartsWith(searchString))
                           .Skip(pageSize * (pageNo - 1))
                           .Take(pageSize)
                           .ToList();
        }

        public int GetCountBySearchString(string searchString)
        {
            return _context.Refunds
                           .Where(r => r.Reason.StartsWith(searchString)
                                || r.RefundDate.ToString().StartsWith(searchString))
                           .Count();
        }

        public Refund GetRefundById(int refundId)
        {
            var refund = _context.Refunds.Where(p => p.Id == refundId).FirstOrDefault();
            return refund;
        }

        public void UpdateRefund(Refund refund)
        {
            _context.Refunds.Update(refund);
            _context.SaveChanges();
        }
    }
}
